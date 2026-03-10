import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService } from '../../../core/services/api.service';
import { CompanyService } from '../../../core/services/company.service';
import { WhatsAppPhoneNumber, WhatsAppTemplate } from '../../../core/models';

interface SendTextRequest {
  to: string;
  body: string;
  previewUrl?: boolean;
  phoneNumberId?: string;
}

interface SendTemplateRequest {
  to: string;
  templateName: string;
  languageCode: string;
  components?: unknown[];
  phoneNumberId?: string;
}

@Component({
  selector: 'app-send-message',
  standalone: true,
  imports: [
    FormsModule,
    ButtonModule,
    ProgressSpinnerModule,
    ToastModule,
    SelectModule,
    TranslateModule,
  ],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="space-y-8 max-w-3xl">
      <!-- Header -->
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'sendMessage.title' | translate }}</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'sendMessage.subtitle' | translate }}</p>
      </div>

      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 space-y-6">
        <!-- Message Type Selector -->
        <div>
          <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">{{ 'sendMessage.messageType' | translate }}</label>
          <div class="flex gap-3">
            <button (click)="messageType.set('text')"
              class="flex-1 py-3 px-4 rounded-xl border-2 transition-all text-sm font-medium flex items-center justify-center gap-2"
              [class]="messageType() === 'text'
                ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-900/20 text-emerald-700 dark:text-emerald-400'
                : 'border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-400 hover:border-slate-300'">
              <i class="pi pi-comments !text-[18px]"></i>
              {{ 'sendMessage.textMessage' | translate }}
            </button>
            <button (click)="messageType.set('template')"
              class="flex-1 py-3 px-4 rounded-xl border-2 transition-all text-sm font-medium flex items-center justify-center gap-2"
              [class]="messageType() === 'template'
                ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-900/20 text-emerald-700 dark:text-emerald-400'
                : 'border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-400 hover:border-slate-300'">
              <i class="pi pi-file !text-[18px]"></i>
              {{ 'sendMessage.templateMessage' | translate }}
            </button>
          </div>
        </div>

        <!-- Phone Number Selector -->
        @if (phoneNumbers().length > 0) {
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'sendMessage.fromNumber' | translate }}</label>
            <select [(ngModel)]="selectedPhoneId"
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none">
              @for (phone of phoneNumbers(); track phone.whatsAppPhoneNumberId) {
                <option [value]="phone.phoneNumberId">{{ phone.displayPhoneNumber }} {{ phone.verifiedName ? '(' + phone.verifiedName + ')' : '' }}</option>
              }
            </select>
          </div>
        }

        <!-- Recipient -->
        <div>
          <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
            {{ 'sendMessage.recipient' | translate }} <span class="text-red-500">*</span>
          </label>
          <input type="tel" [(ngModel)]="recipient" placeholder="+201xxxxxxxxx" dir="ltr"
            class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none"
            [class.!border-red-400]="submitted() && !recipient" />
          @if (submitted() && !recipient) {
            <p class="text-xs text-red-500 mt-1">{{ 'settings.fieldRequired' | translate }}</p>
          }
        </div>

        <!-- Text Message Fields -->
        @if (messageType() === 'text') {
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
              {{ 'sendMessage.messageContent' | translate }} <span class="text-red-500">*</span>
            </label>
            <textarea [(ngModel)]="messageBody" rows="4"
              placeholder="{{ 'sendMessage.messagePlaceholder' | translate }}"
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none resize-none"
              [class.!border-red-400]="submitted() && !messageBody">
            </textarea>
            @if (submitted() && !messageBody) {
              <p class="text-xs text-red-500 mt-1">{{ 'settings.fieldRequired' | translate }}</p>
            }
          </div>
        }

        <!-- Template Message Fields — Smart Picker -->
        @if (messageType() === 'template') {
          <!-- Template Selector -->
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
              {{ 'sendMessage.selectTemplate' | translate }} <span class="text-red-500">*</span>
            </label>
            @if (templatesLoading()) {
              <div class="flex items-center gap-2 py-3 text-slate-400"><p-progressSpinner [style]="{'width':'18px','height':'18px'}" strokeWidth="4" /> <span class="text-sm">{{ 'common.loading' | translate }}</span></div>
            } @else {
              <p-select
                [options]="approvedTemplates()"
                [(ngModel)]="selectedTemplate"
                (ngModelChange)="onTemplateSelected($event)"
                optionLabel="name"
                [placeholder]="'sendMessage.selectTemplatePlaceholder' | translate"
                [filter]="true"
                filterBy="name"
                [showClear]="true"
                styleClass="w-full"
              >
                <ng-template pTemplate="item" let-tpl>
                  <div class="flex items-center gap-3 py-1">
                    <div class="flex-1 min-w-0">
                      <div class="text-sm font-semibold text-slate-800 dark:text-white">{{ tpl.name }}</div>
                      <div class="flex items-center gap-2 mt-0.5">
                        <span class="text-[10px] px-1.5 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400 font-medium">{{ tpl.status }}</span>
                        <span class="text-[10px] text-slate-400">{{ tpl.language }}</span>
                        <span class="text-[10px] text-slate-400">{{ tpl.category }}</span>
                      </div>
                    </div>
                  </div>
                </ng-template>
              </p-select>
            }
            @if (submitted() && !selectedTemplate) {
              <p class="text-xs text-red-500 mt-1">{{ 'settings.fieldRequired' | translate }}</p>
            }
          </div>

          <!-- Template Preview -->
          @if (selectedTemplate) {
            <div class="space-y-4">
              <!-- WhatsApp-style preview card -->
              <div>
                <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-2">{{ 'sendMessage.templatePreview' | translate }}</label>
                <div class="max-w-[320px] bg-[#d9fdd3] dark:bg-emerald-900/70 rounded-lg p-3 shadow-sm space-y-1.5">
                  @for (comp of selectedTemplate.components; track $index) {
                    @switch (comp.type) {
                      @case ('HEADER') {
                        @if (comp.format === 'TEXT') {
                          <p class="text-sm font-bold text-slate-900 dark:text-white">{{ comp.text }}</p>
                        } @else if (comp.format) {
                          <div class="h-24 rounded-md bg-emerald-100 dark:bg-emerald-800/40 flex items-center justify-center">
                            <i class="pi !text-[20px] text-emerald-400" [class]="comp.format === 'IMAGE' ? 'pi-image' : comp.format === 'VIDEO' ? 'pi-video' : 'pi-file'"></i>
                          </div>
                        }
                      }
                      @case ('BODY') {
                        <p class="text-[13px] text-slate-800 dark:text-slate-200 whitespace-pre-wrap leading-relaxed">{{ comp.text }}</p>
                      }
                      @case ('FOOTER') {
                        <p class="text-[11px] text-slate-500 dark:text-slate-400 italic">{{ comp.text }}</p>
                      }
                      @case ('BUTTONS') {
                        <div class="border-t border-emerald-300/30 pt-2 mt-2 space-y-1">
                          @for (btn of comp.buttons; track $index) {
                            <div class="text-center text-[12px] text-blue-600 dark:text-blue-400 font-medium py-1">{{ btn.text }}</div>
                          }
                        </div>
                      }
                    }
                  }
                </div>
              </div>

              <!-- Template Variables -->
              @if (templateVariables().length > 0) {
                <div>
                  <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">{{ 'sendMessage.templateVariables' | translate }}</label>
                  <div class="space-y-3">
                    @for (v of templateVariables(); track v.index) {
                      <div>
                        <label class="text-xs font-medium text-slate-500 dark:text-slate-400 mb-1 block">{{ v.label }}</label>
                        <input type="text" [(ngModel)]="v.value" [placeholder]="v.placeholder"
                          class="w-full px-4 py-2 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none text-sm" />
                      </div>
                    }
                  </div>
                </div>
              }

              <!-- Auto-filled info -->
              <div class="flex items-center gap-4 text-xs text-slate-400">
                <span><i class="pi pi-globe me-1"></i> {{ selectedTemplate.language }}</span>
                <span><i class="pi pi-tag me-1"></i> {{ selectedTemplate.category }}</span>
              </div>
            </div>
          }
        }

        <!-- Success/Error Messages -->
        @if (successMsg()) {
          <div class="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800/40 flex items-center gap-2">
            <i class="pi pi-check-circle !text-emerald-500 !text-[18px]"></i>
            <span class="text-sm text-emerald-700 dark:text-emerald-300">{{ successMsg() }}</span>
          </div>
        }

        @if (errorMsg()) {
          <div class="p-3 rounded-xl bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 flex items-center gap-2">
            <i class="pi pi-times-circle !text-red-500 !text-[18px]"></i>
            <span class="text-sm text-red-700 dark:text-red-300">{{ errorMsg() }}</span>
          </div>
        }

        <!-- Send Button -->
        <div class="flex items-center gap-3 pt-2">
          <button pButton (click)="onSend()" [disabled]="sending()"
            class="!bg-[#25D366] !text-white !rounded-xl hover:!bg-[#128C7E] !px-8 !py-2.5">
            @if (sending()) {
              <p-progressSpinner [style]="{'width':'18px','height':'18px'}" strokeWidth="4" class="!inline-block mr-2" />
            }
            <i class="pi pi-send !text-[18px]"></i>
            {{ 'sendMessage.send' | translate }}
          </button>
          <button pButton [outlined]="true" (click)="resetForm()" class="!rounded-xl !text-sm">
            {{ 'sendMessage.reset' | translate }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class SendMessageComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly companyService = inject(CompanyService);
  private readonly messageService = inject(MessageService);

  // State
  messageType = signal<'text' | 'template'>('text');
  phoneNumbers = signal<WhatsAppPhoneNumber[]>([]);
  sending = signal(false);
  submitted = signal(false);
  successMsg = signal<string | null>(null);
  errorMsg = signal<string | null>(null);

  // Templates
  templates = signal<WhatsAppTemplate[]>([]);
  templatesLoading = signal(false);
  selectedTemplate: WhatsAppTemplate | null = null;

  approvedTemplates = computed(() =>
    this.templates().filter(t => t.status?.toUpperCase() === 'APPROVED')
  );

  templateVariables = signal<{ index: number; label: string; placeholder: string; value: string }[]>([]);

  // Form fields
  selectedPhoneId = '';
  recipient = '';
  messageBody = '';

  ngOnInit(): void {
    this.companyService.loadPhoneNumbers().subscribe((phones) => {
      this.phoneNumbers.set(phones);
      if (phones.length > 0) {
        this.selectedPhoneId = phones[0].phoneNumberId;
      }
    });
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.templatesLoading.set(true);
    this.api.get<any>('/whatsapp/templates').subscribe({
      next: (result) => {
        const data = result?.data || result || [];
        this.templates.set(Array.isArray(data) ? data : []);
        this.templatesLoading.set(false);
      },
      error: () => this.templatesLoading.set(false),
    });
  }

  onTemplateSelected(tpl: WhatsAppTemplate | null): void {
    if (!tpl) {
      this.templateVariables.set([]);
      return;
    }

    // Extract {{N}} variables from BODY component
    const bodyComp = tpl.components.find(c => c.type === 'BODY');
    const bodyText = bodyComp?.text || '';
    const matches = bodyText.match(/\{\{(\d+)\}\}/g) || [];
    const uniqueVars = [...new Set(matches)].sort();

    const vars = uniqueVars.map(v => {
      const idx = parseInt(v.replace(/[{}]/g, ''), 10);
      return {
        index: idx,
        label: `Variable {{${idx}}}`,
        placeholder: `Value for {{${idx}}}`,
        value: '',
      };
    });

    // Also check HEADER for variables
    const headerComp = tpl.components.find(c => c.type === 'HEADER' && c.format === 'TEXT');
    if (headerComp?.text) {
      const headerMatches = headerComp.text.match(/\{\{(\d+)\}\}/g) || [];
      for (const m of headerMatches) {
        const idx = parseInt(m.replace(/[{}]/g, ''), 10);
        if (!vars.some(v => v.index === idx)) {
          vars.unshift({
            index: idx,
            label: `Header Variable {{${idx}}}`,
            placeholder: `Header value for {{${idx}}}`,
            value: '',
          });
        }
      }
    }

    this.templateVariables.set(vars);
  }

  onSend(): void {
    this.submitted.set(true);
    this.successMsg.set(null);
    this.errorMsg.set(null);

    if (!this.recipient.trim()) return;

    if (this.messageType() === 'text') {
      if (!this.messageBody.trim()) return;
      this.sendText();
    } else {
      if (!this.selectedTemplate) return;
      this.sendTemplate();
    }
  }

  private sendText(): void {
    this.sending.set(true);
    const body: SendTextRequest = {
      to: this.recipient.replace(/\s+/g, ''),
      body: this.messageBody,
      previewUrl: false,
      phoneNumberId: this.selectedPhoneId || undefined,
    };

    this.api.postRaw<unknown>('/whatsapp/messages/text', body).subscribe({
      next: (res) => {
        this.sending.set(false);
        if (res.success) {
          this.successMsg.set('Message sent successfully!');
          this.messageService.add({ severity: 'success', summary: '✅ Message sent!', life: 3000 });
          this.resetForm();
        } else {
          this.errorMsg.set(res.message || 'Failed to send message.');
        }
      },
      error: (err) => {
        this.sending.set(false);
        this.errorMsg.set(err.error?.message || err.message || 'Failed to send message.');
      },
    });
  }

  private sendTemplate(): void {
    if (!this.selectedTemplate) return;
    this.sending.set(true);

    // Build components array from variables
    const vars = this.templateVariables();
    const components: unknown[] = [];

    // Body parameters
    const bodyVars = vars.filter(v => v.value.trim());
    if (bodyVars.length > 0) {
      components.push({
        type: 'body',
        parameters: bodyVars.map(v => ({ type: 'text', text: v.value })),
      });
    }

    const body: SendTemplateRequest = {
      to: this.recipient.replace(/\s+/g, ''),
      templateName: this.selectedTemplate.name,
      languageCode: this.selectedTemplate.language || 'en_US',
      components: components.length > 0 ? components : undefined,
      phoneNumberId: this.selectedPhoneId || undefined,
    };

    this.api.postRaw<unknown>('/whatsapp/messages/template', body).subscribe({
      next: (res) => {
        this.sending.set(false);
        if (res.success) {
          this.successMsg.set('Template message sent successfully!');
          this.messageService.add({ severity: 'success', summary: '✅ Template sent!', life: 3000 });
          this.resetForm();
        } else {
          this.errorMsg.set(res.message || 'Failed to send template.');
        }
      },
      error: (err) => {
        this.sending.set(false);
        this.errorMsg.set(err.error?.message || err.message || 'Failed to send template.');
      },
    });
  }

  resetForm(): void {
    this.submitted.set(false);
    this.successMsg.set(null);
    this.errorMsg.set(null);
    this.recipient = '';
    this.messageBody = '';
    this.selectedTemplate = null;
    this.templateVariables.set([]);
  }
}
