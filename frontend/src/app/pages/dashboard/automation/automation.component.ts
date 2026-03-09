import { Component, inject, OnInit, signal, ViewChild } from '@angular/core';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MenuModule, Menu } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService, PermissionService } from '../../../core/services';
import { AutomationRule, AutomationRuleUpsertRequest } from '../../../core/models';
import { ApiResponse } from '../../../core/models';

@Component({
  selector: 'app-automation',
  standalone: true,
  imports: [ProgressSpinnerModule, MenuModule, ToggleSwitchModule, FormsModule, TranslateModule],
  template: `
    <div class="space-y-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'automation.title' | translate }}</h1>
          <p class="text-sm text-slate-500 mt-1">{{ 'automation.subtitle' | translate }}</p>
        </div>
        @if (perm.has('automationCreate')) {
        <button
          (click)="openCreateForm()"
          class="flex items-center gap-2 px-4 py-2 bg-emerald-500 hover:bg-emerald-600 text-white rounded-xl text-sm font-medium transition-colors">
          <i class="pi pi-bolt !text-[18px]"></i>
          {{ 'automation.addRule' | translate }}
        </button>
        }
      </div>

      <!-- Rules List -->
      @if (loading()) {
        <div class="flex justify-center py-12"><p-progressSpinner [style]="{'width':'32px','height':'32px'}" strokeWidth="4" /></div>
      } @else if (rules().length === 0) {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 text-center py-12 text-slate-400">
          <i class="pi pi-bolt !text-[48px] mb-2"></i>
          <p>{{ 'automation.noRules' | translate }}</p>
        </div>
      } @else {
        <div class="space-y-3">
          @for (rule of rules(); track rule.automationRuleId) {
            <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-5">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-4">
                  <div class="w-10 h-10 rounded-xl flex items-center justify-center"
                    [class.bg-emerald-100]="rule.isActive" [class.bg-slate-100]="!rule.isActive"
                    [class.dark:bg-emerald-900/30]="rule.isActive" [class.dark:bg-slate-700]="!rule.isActive">
                    <i class="pi pi-bolt !text-[20px]" [class.text-emerald-600]="rule.isActive" [class.text-slate-400]="!rule.isActive"></i>
                  </div>
                  <div>
                    <h3 class="font-bold text-slate-900 dark:text-white">{{ rule.name }}</h3>
                    <p class="text-xs text-slate-500 mt-0.5">
                      {{ 'automation.trigger' | translate }}: <span class="font-medium text-emerald-600">{{ rule.triggerType }}</span>
                      → <code class="bg-slate-100 dark:bg-slate-700 px-1.5 py-0.5 rounded text-[11px]">{{ rule.triggerValue }}</code>
                    </p>
                  </div>
                </div>
                <div class="flex items-center gap-3">
                  <span class="text-xs text-slate-400">{{ rule.triggerCount }} {{ 'automation.triggered' | translate }}</span>
                  @if (perm.has('automationEdit')) {
                  <p-toggleSwitch
                    [ngModel]="rule.isActive"
                    (ngModelChange)="toggleRule(rule)" />
                  }
                  @if (perm.has('automationEdit') || perm.has('automationDelete')) {
                  <button (click)="openRuleMenu($event, rule)" class="p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700">
                    <i class="pi pi-ellipsis-v !text-[18px] text-slate-400"></i>
                  </button>
                  }
                </div>
              </div>

              @if (rule.description) {
                <p class="text-sm text-slate-500 mt-3 ps-14">{{ rule.description }}</p>
              }

              <div class="flex items-center gap-4 mt-3 ps-14 text-xs text-slate-400">
                <span>{{ 'automation.responseType' | translate }}: <span class="font-medium">{{ rule.responseType }}</span></span>
                <span>{{ 'automation.priority' | translate }}: <span class="font-medium">{{ rule.priority }}</span></span>
              </div>
            </div>
          }
        </div>
      }

      <!-- Create/Edit Modal -->
      @if (showForm()) {
        <div class="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4" (click)="showForm.set(false)">
          <div class="bg-white dark:bg-slate-800 rounded-2xl w-full max-w-lg p-6 space-y-4" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-bold text-slate-900 dark:text-white">
              {{ (editingRule() ? 'automation.editRule' : 'automation.addRule') | translate }}
            </h3>
            <div class="space-y-3">
              <input [(ngModel)]="formData.name" [placeholder]="'automation.ruleName' | translate"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              <textarea [(ngModel)]="formData.description" [placeholder]="'automation.ruleDescription' | translate" rows="2"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white resize-none"></textarea>

              <div class="grid grid-cols-2 gap-3">
                <select [(ngModel)]="formData.triggerType"
                  class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white">
                  <option value="keyword">{{ 'automation.keyword' | translate }}</option>
                  <option value="contains">{{ 'automation.contains' | translate }}</option>
                  <option value="exact">{{ 'automation.exact' | translate }}</option>
                  <option value="regex">{{ 'automation.regex' | translate }}</option>
                </select>
                <input [(ngModel)]="formData.triggerValue" [placeholder]="'automation.triggerValue' | translate"
                  class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              </div>

              <div class="grid grid-cols-2 gap-3">
                <select [(ngModel)]="formData.responseType"
                  class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white">
                  <option value="text">{{ 'automation.textResponse' | translate }}</option>
                  <option value="template">{{ 'automation.templateResponse' | translate }}</option>
                </select>
                <input [(ngModel)]="formData.priority" type="number" [placeholder]="'automation.priority' | translate"
                  class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              </div>

              <textarea [(ngModel)]="formData.responseValue" [placeholder]="'automation.responseValue' | translate" rows="3"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white resize-none"></textarea>
            </div>
            <div class="flex justify-end gap-2 pt-2">
              <button (click)="showForm.set(false)" class="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-xl transition-colors">
                {{ 'common.cancel' | translate }}
              </button>
              <button (click)="saveRule()" class="px-4 py-2 text-sm bg-emerald-500 hover:bg-emerald-600 text-white rounded-xl transition-colors">
                {{ 'common.save' | translate }}
              </button>
            </div>
          </div>
        </div>
      }

      <p-menu #ruleMenu [model]="ruleMenuItems" [popup]="true" />
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
    name: '', triggerType: 'keyword', triggerValue: '', responseType: 'text', responseValue: '', priority: 0,
  };

  ngOnInit(): void {
    this.loadRules();
  }

  loadRules(): void {
    this.api.get<AutomationRule[]>('/automation').subscribe({
      next: (r) => { this.rules.set(r ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openRuleMenu(event: Event, rule: AutomationRule): void {
    this.ruleMenuItems = [];
    if (this.perm.has('automationEdit')) {
      this.ruleMenuItems.push({ label: 'Edit', icon: 'pi pi-pencil', command: () => this.editRule(rule) });
    }
    if (this.perm.has('automationDelete')) {
      this.ruleMenuItems.push({ label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteRule(rule) });
    }
    if (this.ruleMenuItems.length === 0) return;
    this.ruleMenu.toggle(event);
  }

  openCreateForm(): void {
    this.editingRule.set(null);
    this.formData = { name: '', triggerType: 'keyword', triggerValue: '', responseType: 'text', responseValue: '', priority: 0 };
    this.showForm.set(true);
  }

  editRule(r: AutomationRule): void {
    this.editingRule.set(r);
    this.formData = {
      name: r.name, description: r.description ?? undefined, triggerType: r.triggerType, triggerValue: r.triggerValue,
      responseType: r.responseType, responseValue: r.responseValue, templateName: r.templateName ?? undefined,
      languageCode: r.languageCode ?? undefined, priority: r.priority,
    };
    this.showForm.set(true);
  }

  saveRule(): void {
    const editing = this.editingRule();
    const obs$ = editing
      ? this.api.put(`/automation/${editing.automationRuleId}`, this.formData)
      : this.api.post('/automation', this.formData);

    obs$.subscribe({
      next: () => { this.showForm.set(false); this.loadRules(); },
    });
  }

  deleteRule(r: AutomationRule): void {
    if (!confirm('Delete this rule?')) return;
    this.api.delete(`/automation/${r.automationRuleId}`).subscribe({ next: () => this.loadRules() });
  }

  toggleRule(r: AutomationRule): void {
    this.api.post(`/automation/${r.automationRuleId}/toggle`).subscribe({ next: () => this.loadRules() });
  }
}
