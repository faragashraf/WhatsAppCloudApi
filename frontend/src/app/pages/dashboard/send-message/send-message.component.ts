import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService } from '../../../core/services/api.service';
import { CompanyService } from '../../../core/services/company.service';
import { WhatsAppPhoneNumber } from '../../../core/models';

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

        <!-- Template Message Fields -->
        @if (messageType() === 'template') {
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
              {{ 'sendMessage.templateName' | translate }} <span class="text-red-500">*</span>
            </label>
            <input type="text" [(ngModel)]="templateName" placeholder="e.g. hello_world"
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none"
              [class.!border-red-400]="submitted() && !templateName" />
            @if (submitted() && !templateName) {
              <p class="text-xs text-red-500 mt-1">{{ 'settings.fieldRequired' | translate }}</p>
            }
          </div>

          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
              {{ 'sendMessage.languageCode' | translate }}
            </label>
            <input type="text" [(ngModel)]="languageCode" placeholder="en_US"
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
          </div>

          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
              {{ 'sendMessage.templateParams' | translate }}
              <span class="text-xs text-slate-400 ml-1">({{ 'sendMessage.templateParamsHint' | translate }})</span>
            </label>
            <textarea [(ngModel)]="templateParams" rows="3" placeholder='[{"type":"body","parameters":[{"type":"text","text":"John"}]}]'
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none resize-none font-mono text-sm">
            </textarea>
          </div>
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

  // Form fields
  selectedPhoneId = '';
  recipient = '';
  messageBody = '';
  templateName = '';
  languageCode = 'en_US';
  templateParams = '';

  ngOnInit(): void {
    this.companyService.loadPhoneNumbers().subscribe((phones) => {
      this.phoneNumbers.set(phones);
      if (phones.length > 0) {
        this.selectedPhoneId = phones[0].phoneNumberId;
      }
    });
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
      if (!this.templateName.trim()) return;
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
    this.sending.set(true);
    let components: unknown[] = [];
    if (this.templateParams.trim()) {
      try {
        components = JSON.parse(this.templateParams);
      } catch {
        this.sending.set(false);
        this.errorMsg.set('Invalid JSON in template parameters.');
        return;
      }
    }

    const body: SendTemplateRequest = {
      to: this.recipient.replace(/\s+/g, ''),
      templateName: this.templateName,
      languageCode: this.languageCode || 'en_US',
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
    this.templateName = '';
    this.templateParams = '';
  }
}
