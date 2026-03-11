import { Component, inject, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MenuItem } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TranslateModule } from '@ngx-translate/core';
import { AutomationRule, AutomationRuleUpsertRequest } from '../../../core/models';
import { ApiService, PermissionService } from '../../../core/services';

@Component({
  selector: 'app-automation',
  standalone: true,
  imports: [ProgressSpinnerModule, MenuModule, ToggleSwitchModule, FormsModule, TranslateModule],
  template: `
    <div class="space-y-6">
      <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'automation.title' | translate }}</h1>
          <p class="mt-1 text-sm text-slate-500">{{ 'automation.subtitle' | translate }}</p>
        </div>
        @if (perm.has('automationCreate')) {
          <button
            (click)="openCreateForm()"
            class="flex items-center gap-2 rounded-xl bg-[var(--app-primary)] px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-[var(--app-primary-strong)]">
            <i class="pi pi-bolt !text-[18px]"></i>
            {{ 'automation.addRule' | translate }}
          </button>
        }
      </div>

      @if (loading()) {
        <div class="flex justify-center py-12">
          <p-progressSpinner [style]="{'width':'32px','height':'32px'}" strokeWidth="4" />
        </div>
      } @else if (rules().length === 0) {
        <div class="rounded-2xl border border-slate-200 bg-white py-12 text-center text-slate-400 dark:border-slate-700/50 dark:bg-slate-800/50">
          <i class="pi pi-bolt mb-2 !text-[48px]"></i>
          <p>{{ 'automation.noRules' | translate }}</p>
        </div>
      } @else {
        <div class="space-y-3">
          @for (rule of rules(); track rule.automationRuleId) {
            <div class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-700/50 dark:bg-slate-800/50">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-4">
                  <div
                    class="flex h-10 w-10 items-center justify-center rounded-xl"
                    [class.bg-emerald-100]="rule.isActive"
                    [class.bg-slate-100]="!rule.isActive"
                    [class.dark:bg-emerald-900/30]="rule.isActive"
                    [class.dark:bg-slate-700]="!rule.isActive">
                    <i
                      class="pi pi-bolt !text-[20px]"
                      [class.text-emerald-600]="rule.isActive"
                      [class.text-slate-400]="!rule.isActive"></i>
                  </div>
                  <div>
                    <h3 class="font-bold text-slate-900 dark:text-white">{{ rule.name }}</h3>
                    <p class="mt-0.5 text-xs text-slate-500">
                      {{ 'automation.trigger' | translate }}:
                      @if (rule.triggerType === 'any') {
                        <span class="font-medium text-emerald-600">{{ 'automation.anyMessage' | translate }}</span>
                      } @else {
                        <span class="font-medium text-emerald-600">{{ rule.triggerType }}</span>
                        <code class="rounded bg-slate-100 px-1.5 py-0.5 text-[11px] dark:bg-slate-700">{{ rule.triggerValue }}</code>
                      }
                    </p>
                  </div>
                </div>
                <div class="flex items-center gap-3">
                  <span class="text-xs text-slate-400">{{ rule.triggerCount }} {{ 'automation.triggered' | translate }}</span>
                  @if (perm.has('automationEdit')) {
                    <p-toggleSwitch [ngModel]="rule.isActive" (ngModelChange)="toggleRule(rule)" />
                  }
                  @if (perm.has('automationEdit') || perm.has('automationDelete')) {
                    <button
                      (click)="openRuleMenu($event, rule)"
                      class="rounded-lg p-1 hover:bg-slate-100 dark:hover:bg-slate-700">
                      <i class="pi pi-ellipsis-v !text-[18px] text-slate-400"></i>
                    </button>
                  }
                </div>
              </div>

              @if (rule.description) {
                <p class="mt-3 ps-14 text-sm text-slate-500">{{ rule.description }}</p>
              }

              <div class="mt-3 flex items-center gap-4 ps-14 text-xs text-slate-400">
                <span>{{ 'automation.responseType' | translate }}: <span class="font-medium">{{ rule.responseType }}</span></span>
                <span>{{ 'automation.priority' | translate }}: <span class="font-medium">{{ rule.priority }}</span></span>
              </div>
            </div>
          }
        </div>
      }

      @if (showForm()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" (click)="showForm.set(false)">
          <div class="w-full max-w-lg space-y-4 rounded-2xl bg-white p-6 dark:bg-slate-800" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-bold text-slate-900 dark:text-white">
              {{ (editingRule() ? 'automation.editRule' : 'automation.addRule') | translate }}
            </h3>

            <div class="space-y-3">
              <input
                [(ngModel)]="formData.name"
                [placeholder]="'automation.ruleName' | translate"
                class="w-full rounded-xl border-0 bg-slate-100 px-4 py-2.5 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-700 dark:text-white" />

              <textarea
                [(ngModel)]="formData.description"
                [placeholder]="'automation.ruleDescription' | translate"
                rows="2"
                class="w-full resize-none rounded-xl border-0 bg-slate-100 px-4 py-2.5 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-700 dark:text-white"></textarea>

              <div class="grid grid-cols-2 gap-3">
                <select
                  [(ngModel)]="formData.triggerType"
                  (ngModelChange)="onTriggerTypeChange($event)"
                  class="w-full rounded-xl border-0 bg-slate-100 px-4 py-2.5 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-700 dark:text-white">
                  <option value="any">{{ 'automation.anyMessage' | translate }}</option>
                  <option value="keyword">{{ 'automation.keyword' | translate }}</option>
                  <option value="contains">{{ 'automation.contains' | translate }}</option>
                  <option value="exact">{{ 'automation.exact' | translate }}</option>
                  <option value="regex">{{ 'automation.regex' | translate }}</option>
                </select>

                @if (requiresTriggerValue()) {
                  <input
                    [(ngModel)]="formData.triggerValue"
                    [placeholder]="'automation.triggerValue' | translate"
                    class="w-full rounded-xl border-0 bg-slate-100 px-4 py-2.5 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-700 dark:text-white" />
                } @else {
                  <div class="flex items-center rounded-xl bg-emerald-50 px-4 py-2.5 text-sm text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-200">
                    {{ 'automation.triggerValueNotRequired' | translate }}
                  </div>
                }
              </div>

              <div class="grid grid-cols-2 gap-3">
                <select
                  [(ngModel)]="formData.responseType"
                  class="w-full rounded-xl border-0 bg-slate-100 px-4 py-2.5 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-700 dark:text-white">
                  <option value="text">{{ 'automation.textResponse' | translate }}</option>
                  <option value="template">{{ 'automation.templateResponse' | translate }}</option>
                </select>

                <input
                  [(ngModel)]="formData.priority"
                  type="number"
                  [placeholder]="'automation.priority' | translate"
                  class="w-full rounded-xl border-0 bg-slate-100 px-4 py-2.5 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-700 dark:text-white" />
              </div>

              @if (formData.responseType === 'template') {
                <div class="grid grid-cols-2 gap-3">
                  <input
                    [(ngModel)]="formData.templateName"
                    [placeholder]="'campaigns.templateName' | translate"
                    class="w-full rounded-xl border-0 bg-slate-100 px-4 py-2.5 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-700 dark:text-white" />
                  <input
                    [(ngModel)]="formData.languageCode"
                    [placeholder]="'campaigns.language' | translate"
                    class="w-full rounded-xl border-0 bg-slate-100 px-4 py-2.5 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-700 dark:text-white" />
                </div>
              }

              <textarea
                [(ngModel)]="formData.responseValue"
                [placeholder]="'automation.responseValue' | translate"
                rows="3"
                class="w-full resize-none rounded-xl border-0 bg-slate-100 px-4 py-2.5 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-emerald-500 dark:bg-slate-700 dark:text-white"></textarea>
            </div>

            <div class="flex justify-end gap-2 pt-2">
              <button
                (click)="showForm.set(false)"
                class="rounded-xl px-4 py-2 text-sm text-slate-600 transition-colors hover:bg-slate-100 dark:hover:bg-slate-700">
                {{ 'common.cancel' | translate }}
              </button>
              <button
                (click)="saveRule()"
                [disabled]="!canSaveRule()"
                class="rounded-xl bg-[var(--app-primary)] px-4 py-2 text-sm text-white transition-colors hover:bg-[var(--app-primary-strong)] disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-[var(--app-primary)]">
                {{ 'common.save' | translate }}
              </button>
            </div>
          </div>
        </div>
      }

      <p-menu #ruleMenu [model]="ruleMenuItems" [popup]="true" appendTo="body" />
    </div>
  `,
})
export class AutomationComponent implements OnInit {
  private api = inject(ApiService);
  readonly perm = inject(PermissionService);

  loading = signal(true);
  rules = signal<AutomationRule[]>([]);
  showForm = signal(false);
  editingRule = signal<AutomationRule | null>(null);

  @ViewChild('ruleMenu') ruleMenu!: Menu;
  ruleMenuItems: MenuItem[] = [];

  formData: AutomationRuleUpsertRequest = {
    name: '',
    triggerType: 'keyword',
    triggerValue: '',
    responseType: 'text',
    responseValue: '',
    priority: 0,
    isActive: true,
  };

  ngOnInit(): void {
    this.loadRules();
  }

  loadRules(): void {
    this.api.get<AutomationRule[]>('/automation').subscribe({
      next: (rules) => {
        this.rules.set(rules ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openRuleMenu(event: MouseEvent, rule: AutomationRule): void {
    event.stopPropagation();
    this.ruleMenuItems = [];

    if (this.perm.has('automationEdit')) {
      this.ruleMenuItems.push({ label: 'Edit', icon: 'pi pi-pencil', command: () => this.editRule(rule) });
    }

    if (this.perm.has('automationDelete')) {
      this.ruleMenuItems.push({ label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteRule(rule) });
    }

    if (this.ruleMenuItems.length === 0) {
      return;
    }

    this.ruleMenu.toggle(event);
  }

  openCreateForm(): void {
    this.editingRule.set(null);
    this.formData = {
      name: '',
      triggerType: 'keyword',
      triggerValue: '',
      responseType: 'text',
      responseValue: '',
      languageCode: 'en_US',
      priority: 0,
      isActive: true,
    };
    this.showForm.set(true);
  }

  editRule(rule: AutomationRule): void {
    this.editingRule.set(rule);
    this.formData = {
      name: rule.name,
      description: rule.description ?? undefined,
      triggerType: rule.triggerType,
      triggerValue: rule.triggerType === 'any' ? '' : rule.triggerValue,
      responseType: rule.responseType,
      responseValue: rule.responseValue,
      templateName: rule.templateName ?? undefined,
      languageCode: rule.languageCode ?? 'en_US',
      priority: rule.priority,
      isActive: rule.isActive,
    };
    this.showForm.set(true);
  }

  saveRule(): void {
    if (!this.canSaveRule()) {
      return;
    }

    const editing = this.editingRule();
    const payload = this.buildRulePayload();
    const request$ = editing
      ? this.api.put(`/automation/${editing.automationRuleId}`, payload)
      : this.api.post('/automation', payload);

    request$.subscribe({
      next: () => {
        this.showForm.set(false);
        this.loadRules();
      },
    });
  }

  deleteRule(rule: AutomationRule): void {
    if (!confirm('Delete this rule?')) {
      return;
    }

    this.api.delete(`/automation/${rule.automationRuleId}`).subscribe({
      next: () => this.loadRules(),
    });
  }

  toggleRule(rule: AutomationRule): void {
    this.api.post(`/automation/${rule.automationRuleId}/toggle`).subscribe({
      next: () => this.loadRules(),
    });
  }

  requiresTriggerValue(): boolean {
    return this.formData.triggerType !== 'any';
  }

  canSaveRule(): boolean {
    const name = this.formData.name?.trim();
    const priority = Number(this.formData.priority);
    const responseType = this.formData.responseType;
    const responseValue = this.formData.responseValue?.trim();
    const templateName = this.formData.templateName?.trim();

    if (!name || Number.isNaN(priority) || priority < 0 || priority > 10000) {
      return false;
    }

    if (this.requiresTriggerValue() && !this.formData.triggerValue?.trim()) {
      return false;
    }

    if (responseType === 'template') {
      return !!templateName;
    }

    if (responseType !== 'text') {
      return false;
    }

    return !!responseValue;
  }

  onTriggerTypeChange(triggerType: string): void {
    this.formData.triggerType = triggerType;

    if (triggerType === 'any' || this.formData.triggerValue === '*') {
      this.formData.triggerValue = '';
    }
  }

  private buildRulePayload(): AutomationRuleUpsertRequest {
    const responseType = this.formData.responseType;
    const templateName = this.formData.templateName?.trim();
    const responseValue = this.formData.responseValue?.trim();
    const triggerType = this.formData.triggerType;

    return {
      ...this.formData,
      name: this.formData.name.trim(),
      description: this.formData.description?.trim() || undefined,
      triggerValue: triggerType === 'any' ? '*' : this.formData.triggerValue.trim(),
      responseValue: responseType === 'template'
        ? (responseValue || (templateName ? `Template: ${templateName}` : ''))
        : (responseValue || ''),
      templateName: responseType === 'template' ? templateName || undefined : undefined,
      languageCode: responseType === 'template'
        ? (this.formData.languageCode?.trim() || 'en_US')
        : undefined,
      isActive: this.formData.isActive ?? true,
    };
  }
}
