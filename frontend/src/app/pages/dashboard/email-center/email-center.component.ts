import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ApiService, LanguageService, TokenService } from '../../../core/services';

interface EmailValidationState {
  valid: boolean;
  errors: string[];
  normalizedEmails?: string[];
}

@Component({
  selector: 'app-email-center',
  standalone: true,
  imports: [CommonModule, FormsModule, ProgressSpinnerModule, ToastModule],
  providers: [MessageService],
  styles: [`
    :host { display: block; }

    :host-context(html.dark) .email-center-page section,
    :host-context(html.dark) .email-center-page article,
    :host-context(html.dark) .email-center-page .rounded-3xl,
    :host-context(html.dark) .email-center-page .rounded-2xl {
      background-color: rgba(15, 23, 42, 0.9) !important;
      border-color: rgba(100, 116, 139, 0.35) !important;
      color: #e2e8f0 !important;
    }

    :host-context(html.dark) .email-center-page .bg-white,
    :host-context(html.dark) .email-center-page .bg-slate-50 {
      background-color: rgba(15, 23, 42, 0.9) !important;
    }

    :host-context(html.dark) .email-center-page section:first-child {
      background-image: linear-gradient(135deg, rgba(15, 23, 42, 0.96), rgba(6, 78, 59, 0.38)) !important;
    }

    :host-context(html.dark) .email-center-page .bg-slate-100 {
      background-color: rgba(30, 41, 59, 0.85) !important;
      color: #e2e8f0 !important;
    }

    :host-context(html.dark) .email-center-page .bg-amber-50 {
      background-color: rgba(120, 53, 15, 0.25) !important;
      border-color: rgba(251, 191, 36, 0.4) !important;
    }

    :host-context(html.dark) .email-center-page .bg-rose-50 {
      background-color: rgba(136, 19, 55, 0.25) !important;
      border-color: rgba(251, 113, 133, 0.4) !important;
    }

    :host-context(html.dark) .email-center-page .text-slate-900 {
      color: #e2e8f0 !important;
    }

    :host-context(html.dark) .email-center-page .text-slate-600,
    :host-context(html.dark) .email-center-page .text-slate-500 {
      color: #94a3b8 !important;
    }

    :host-context(html.dark) .email-center-page .text-teal-700,
    :host-context(html.dark) .email-center-page .text-emerald-700 {
      color: #2dd4bf !important;
    }

    :host-context(html.dark) .email-center-page .text-amber-900 {
      color: #fcd34d !important;
    }

    :host-context(html.dark) .email-center-page .text-rose-700 {
      color: #fda4af !important;
    }

    :host-context(html.dark) .email-center-page input,
    :host-context(html.dark) .email-center-page select,
    :host-context(html.dark) .email-center-page textarea {
      background-color: rgba(2, 6, 23, 0.7) !important;
      border-color: rgba(100, 116, 139, 0.45) !important;
      color: #e2e8f0 !important;
    }

    :host-context(html.dark) .email-center-page .border-slate-100,
    :host-context(html.dark) .email-center-page .border-slate-200,
    :host-context(html.dark) .email-center-page .border-slate-300 {
      border-color: rgba(100, 116, 139, 0.35) !important;
    }

    :host-context(html.dark) .email-center-page .border-rose-200 {
      border-color: rgba(251, 113, 133, 0.45) !important;
    }

    :host-context(html.dark) .email-center-page .border-amber-200 {
      border-color: rgba(251, 191, 36, 0.45) !important;
    }

    :host-context(html.dark) .email-center-page table thead tr,
    :host-context(html.dark) .email-center-page table tbody tr {
      border-color: rgba(100, 116, 139, 0.35) !important;
    }

    .email-center-page.rtl {
      direction: rtl;
      text-align: right;
    }
  `],
  template: `
    <p-toast />
    <div class="email-center-page space-y-6 min-w-0 app-wrap-safe" [class.rtl]="langService.isRtl()">
      <section class="rounded-[28px] border border-slate-200 bg-[linear-gradient(135deg,_#f8fafc,_#ecfeff)] p-6">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div class="text-xs font-bold uppercase tracking-[0.2em] text-teal-700">{{ txt('مركز البريد', 'Email Center') }}</div>
            <h1 class="mt-2 text-3xl font-black text-slate-900">{{ txt('إدارة الطوابير والحسابات وقواعد الانتهاء', 'Queue, accounts, and expiry rules') }}</h1>
            <p class="mt-2 max-w-3xl text-sm text-slate-600">{{ txt('استخدم هذه الصفحة لإدارة مرسلي SMTP، وجدولة رسائل البريد الفردية، وإشعارات انتهاء الاشتراك.', 'Use this dashboard to manage SMTP senders, queue one-off emails, and schedule subscription-expiry notifications.') }}</p>
          </div>

          @if (isSuperAdmin()) {
            <div class="rounded-2xl border border-slate-200 bg-white p-1">
              <button type="button" class="rounded-xl px-4 py-2 text-sm font-semibold" [class.bg-teal-600]="scope() === 'company'" [class.text-white]="scope() === 'company'" (click)="changeScope('company')">{{ txt('الشركة', 'Company') }}</button>
              <button type="button" class="rounded-xl px-4 py-2 text-sm font-semibold" [class.bg-slate-900]="scope() === 'critical'" [class.text-white]="scope() === 'critical'" (click)="changeScope('critical')">{{ txt('حرِج', 'Critical') }}</button>
            </div>
          }
        </div>
      </section>

      @if (loading()) {
        <div class="flex justify-center py-12"><p-progressSpinner [style]="{ width: '40px', height: '40px' }" strokeWidth="4" /></div>
      } @else {
        <section class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          @for (card of cards(); track card.label) {
            <div class="rounded-3xl border border-slate-200 bg-white p-5">
              <div class="text-xs font-bold uppercase tracking-[0.18em] text-slate-500">{{ card.label }}</div>
              <div class="mt-3 text-3xl font-black text-slate-900">{{ card.value }}</div>
              <div class="mt-2 text-xs text-slate-500">{{ card.note }}</div>
            </div>
          }
        </section>

        <section class="grid gap-6" [class.xl:grid-cols-2]="!canManageExpiryRules()" [class.xl:grid-cols-3]="canManageExpiryRules()">
          <article class="rounded-3xl border border-slate-200 bg-white p-5 xl:col-span-1">
            <div class="mb-4 flex items-center justify-between gap-3">
              <div>
                <h2 class="text-xl font-bold text-slate-900">{{ txt('إضافة بريد إلى الطابور', 'Queue Email') }}</h2>
                <p class="mt-1 text-xs text-slate-500">{{ txt('مطلوب: الحساب، المستلمون، العنوان، والمحتوى.', 'Required: account, recipients, subject, and body.') }}</p>
              </div>
              <button type="button" class="text-sm font-semibold text-slate-500" (click)="resetCompose()">{{ txt('إعادة ضبط', 'Reset') }}</button>
            </div>
            <div class="space-y-3">
              <select [(ngModel)]="compose.emailAccountId" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
                <option [ngValue]="0">{{ txt('اختر حسابًا', 'Select account') }}</option>
                @for (account of accounts(); track account.emailAccountId) {
                  <option [ngValue]="account.emailAccountId">{{ account.name }} - {{ account.fromAddress }}</option>
                }
              </select>
              <input [(ngModel)]="compose.toCsv" type="text" [placeholder]="txt('المستلمون مفصولون بفاصلة', 'Recipients comma separated')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <input [(ngModel)]="compose.subject" type="text" maxlength="300" [placeholder]="txt('عنوان الرسالة', 'Subject')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <textarea [(ngModel)]="compose.body" rows="7" maxlength="20000" [placeholder]="txt('محتوى البريد الإلكتروني', 'Email body')" class="w-full rounded-3xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm"></textarea>
              <div class="grid gap-3 md:grid-cols-2">
                <input [(ngModel)]="compose.category" type="text" maxlength="100" [placeholder]="txt('التصنيف', 'Category')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
                <input [(ngModel)]="compose.priority" type="number" min="0" max="1000" [placeholder]="txt('الأولوية', 'Priority')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              </div>
              <input [(ngModel)]="compose.scheduledAtLocal" type="datetime-local" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <label class="inline-flex items-center gap-2 text-sm text-slate-600">
                <input [(ngModel)]="compose.isBodyHtml" type="checkbox" class="h-4 w-4" />
                {{ txt('إرسال بصيغة HTML', 'Send as HTML') }}
              </label>
              <label class="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-dashed border-slate-300 px-4 py-2 text-sm font-semibold text-slate-600">
                <input type="file" class="hidden" multiple (change)="onFilesSelected($event)" />
                {{ txt('إضافة مرفقات', 'Add attachments') }}
              </label>
              @if (draftAttachments().length > 0) {
                <div class="flex flex-wrap gap-2">
                  @for (attachment of draftAttachments(); track attachment.fileName + attachment.sizeBytes) {
                    <button type="button" class="rounded-full bg-slate-100 px-3 py-2 text-xs font-semibold text-slate-700" (click)="removeAttachment(attachment.fileName)">{{ attachment.fileName }} - {{ formatBytes(attachment.sizeBytes) }}</button>
                  }
                </div>
              }
              @if (!isComposeValid()) {
                <div class="rounded-2xl border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
                  @for (error of composeValidation().errors; track error) {
                    <div>{{ error }}</div>
                  }
                </div>
              }
              <button type="button" class="w-full rounded-2xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white transition disabled:cursor-not-allowed disabled:opacity-50" [disabled]="!isComposeValid()" (click)="queueEmail()">{{ txt('إضافة إلى الطابور', 'Queue email') }}</button>
            </div>
          </article>

          <article class="rounded-3xl border border-slate-200 bg-white p-5">
            <div class="mb-4 flex items-center justify-between gap-3">
              <div>
                <h2 class="text-xl font-bold text-slate-900">{{ txt('حسابات SMTP', 'SMTP Accounts') }}</h2>
                <p class="mt-1 text-xs text-slate-500">{{ txt('احفظ بيانات SMTP المكتملة فقط. الحساب الافتراضي يجب أن يظل نشطًا.', 'Save only complete SMTP credentials. Default accounts must stay active.') }}</p>
              </div>
              <button type="button" class="text-sm font-semibold text-slate-500" (click)="resetAccount()">{{ txt('جديد', 'New') }}</button>
            </div>
            <div class="space-y-2">
              @for (account of accounts(); track account.emailAccountId) {
                <button type="button" class="w-full rounded-2xl border border-slate-200 px-3 py-3 text-start" (click)="editAccount(account)">
                  <div class="font-semibold text-slate-900">{{ account.name }}</div>
                  <div class="text-xs text-slate-500">{{ account.fromAddress }} - {{ account.smtpHost }}:{{ account.smtpPort }}</div>
                </button>
              }
            </div>
            <div class="mt-4 space-y-3 border-t border-slate-100 pt-4">
              <input [(ngModel)]="accountForm.name" type="text" maxlength="120" [placeholder]="txt('اسم الحساب', 'Account name')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <input [(ngModel)]="accountForm.fromAddress" type="email" maxlength="200" [placeholder]="txt('بريد المرسل', 'From address')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <input [(ngModel)]="accountForm.replyToAddress" type="email" maxlength="200" [placeholder]="txt('بريد الرد (اختياري)', 'Reply-to address (optional)')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <input [(ngModel)]="accountForm.fromName" type="text" maxlength="150" [placeholder]="txt('اسم المرسل', 'From name')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <input [(ngModel)]="accountForm.smtpHost" type="text" maxlength="200" [placeholder]="txt('مضيف SMTP', 'SMTP host')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <div class="grid gap-3 md:grid-cols-2">
                <input [(ngModel)]="accountForm.smtpPort" type="number" min="1" max="65535" [placeholder]="txt('المنفذ', 'Port')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
                <input [(ngModel)]="accountForm.username" type="text" maxlength="200" [placeholder]="txt('اسم المستخدم', 'Username')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              </div>
              <input [(ngModel)]="accountForm.password" type="password" maxlength="500" [placeholder]="accountForm.emailAccountId && accountForm.hasPassword ? txt('اتركه فارغًا للاحتفاظ بكلمة المرور الحالية', 'Leave blank to keep current password') : txt('كلمة المرور', 'Password')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <div class="flex flex-wrap items-center gap-4 text-sm text-slate-600">
                <label class="inline-flex items-center gap-2"><input [(ngModel)]="accountForm.enableSsl" type="checkbox" class="h-4 w-4" /> SSL</label>
                <label class="inline-flex items-center gap-2"><input [(ngModel)]="accountForm.isDefault" type="checkbox" class="h-4 w-4" /> {{ txt('افتراضي', 'Default') }}</label>
                <label class="inline-flex items-center gap-2"><input [(ngModel)]="accountForm.isActive" type="checkbox" class="h-4 w-4" /> {{ txt('نشط', 'Active') }}</label>
              </div>
              @if (!isAccountValid()) {
                <div class="rounded-2xl border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
                  @for (error of accountValidation().errors; track error) {
                    <div>{{ error }}</div>
                  }
                </div>
              }
              <div class="flex flex-wrap gap-3">
                <button type="button" class="rounded-2xl bg-teal-600 px-4 py-3 text-sm font-semibold text-white transition disabled:cursor-not-allowed disabled:opacity-50" [disabled]="!isAccountValid()" (click)="saveAccount()">{{ accountForm.emailAccountId ? txt('تحديث', 'Update') : txt('إنشاء', 'Create') }}</button>
                @if (accountForm.emailAccountId) {
                  <button type="button" class="rounded-2xl border border-rose-200 px-4 py-3 text-sm font-semibold text-rose-700" (click)="deleteAccount(accountForm.emailAccountId)">{{ txt('حذف', 'Delete') }}</button>
                }
              </div>
            </div>
          </article>

          @if (canManageExpiryRules()) {
          <article class="rounded-3xl border border-slate-200 bg-white p-5">
            <div class="mb-4 flex items-center justify-between gap-3">
              <div>
                <h2 class="text-xl font-bold text-slate-900">{{ txt('قواعد الانتهاء', 'Expiry Rules') }}</h2>
                <p class="mt-1 text-xs text-slate-500">{{ txt('تظهر فقط للمشرف العام. تُنفذ القاعدة مرة واحدة عند دخول الشركة نافذة الانتهاء.', 'Visible only to super admins. The rule fires once when a company enters the expiry window.') }}</p>
              </div>
              <button type="button" class="text-sm font-semibold text-slate-500" (click)="resetRule()">{{ txt('جديد', 'New') }}</button>
            </div>
            <div class="space-y-2">
              @for (rule of rules(); track rule.emailNotificationRuleId) {
                <button type="button" class="w-full rounded-2xl border border-slate-200 px-3 py-3 text-start" (click)="editRule(rule)">
                  <div class="font-semibold text-slate-900">{{ rule.name }}</div>
                  <div class="text-xs text-slate-500">{{ rule.emailAccountName }} - {{ rule.recipientMode }} - {{ rule.leadTimeDays }} {{ txt('يوم', 'day(s)') }}</div>
                </button>
              }
            </div>
            <div class="mt-4 space-y-3 border-t border-slate-100 pt-4">
              <input [(ngModel)]="ruleForm.name" type="text" maxlength="150" [placeholder]="txt('اسم القاعدة', 'Rule name')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <select [(ngModel)]="ruleForm.emailAccountId" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
                <option [ngValue]="0">{{ txt('اختر حسابًا', 'Select account') }}</option>
                @for (account of accounts(); track account.emailAccountId) {
                  <option [ngValue]="account.emailAccountId">{{ account.name }}</option>
                }
              </select>
              <div class="grid gap-3 md:grid-cols-2">
                <input [(ngModel)]="ruleForm.category" type="text" maxlength="100" [placeholder]="txt('التصنيف', 'Category')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
                <input [(ngModel)]="ruleForm.leadTimeDays" type="number" min="0" max="365" [placeholder]="txt('مدة الإشعار المسبق', 'Lead time')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              </div>
              <select [(ngModel)]="ruleForm.recipientMode" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
                <option value="COMPANY_ADMINS">{{ txt('مدراء الشركة', 'Company admins') }}</option>
                <option value="COMPANY_EMAIL">{{ txt('بريد الشركة', 'Company email') }}</option>
                <option value="CUSTOM">{{ txt('مستلمون مخصصون', 'Custom recipients') }}</option>
              </select>
              @if (ruleForm.recipientMode === 'CUSTOM') {
                <input [(ngModel)]="ruleForm.recipientsCsv" type="text" [placeholder]="txt('المستلمون المخصصون مفصولون بفاصلة', 'Custom recipients comma separated')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              }
              <input [(ngModel)]="ruleForm.subjectTemplate" type="text" maxlength="300" [placeholder]="txt('قالب العنوان', 'Subject template')" class="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm" />
              <textarea [(ngModel)]="ruleForm.bodyTemplate" rows="5" maxlength="20000" [placeholder]="txt('استخدم متغيرات الانتهاء مثل companyName و daysUntilExpiry', 'Use supported expiry tokens like companyName and daysUntilExpiry')" class="w-full rounded-3xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm"></textarea>
              <div class="flex flex-wrap items-center gap-4 text-sm text-slate-600">
                <label class="inline-flex items-center gap-2"><input [(ngModel)]="ruleForm.isBodyHtml" type="checkbox" class="h-4 w-4" /> HTML</label>
                <label class="inline-flex items-center gap-2"><input [(ngModel)]="ruleForm.isActive" type="checkbox" class="h-4 w-4" /> {{ txt('نشط', 'Active') }}</label>
              </div>
              @if (!isRuleValid()) {
                <div class="rounded-2xl border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
                  @for (error of ruleValidation().errors; track error) {
                    <div>{{ error }}</div>
                  }
                </div>
              }
              <div class="flex flex-wrap gap-3">
                <button type="button" class="rounded-2xl bg-slate-900 px-4 py-3 text-sm font-semibold text-white transition disabled:cursor-not-allowed disabled:opacity-50" [disabled]="!isRuleValid()" (click)="saveRule()">{{ ruleForm.emailNotificationRuleId ? txt('تحديث', 'Update') : txt('إنشاء', 'Create') }}</button>
                @if (ruleForm.emailNotificationRuleId) {
                  <button type="button" class="rounded-2xl border border-rose-200 px-4 py-3 text-sm font-semibold text-rose-700" (click)="deleteRule(ruleForm.emailNotificationRuleId)">{{ txt('حذف', 'Delete') }}</button>
                }
              </div>
            </div>
          </article>
          }
        </section>

        <section class="rounded-3xl border border-slate-200 bg-white p-5">
          <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 class="text-xl font-bold text-slate-900">{{ txt('البريد الإلكتروني في الطابور', 'Queued Emails') }}</h2>
              <p class="text-sm text-slate-500">{{ txt('مراجعة الرسائل المعلقة والمرسلة والفاشلة والملغاة.', 'Review pending, sent, failed, and canceled emails.') }}</p>
            </div>
            <div class="flex flex-wrap gap-2">
              <select [(ngModel)]="queueFilter.status" class="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm">
                <option value="">{{ txt('كل الحالات', 'All statuses') }}</option>
                <option value="PENDING">{{ txt('معلّق', 'Pending') }}</option>
                <option value="RETRY">{{ txt('إعادة محاولة', 'Retry') }}</option>
                <option value="SENT">{{ txt('تم الإرسال', 'Sent') }}</option>
                <option value="FAILED">{{ txt('فشل', 'Failed') }}</option>
                <option value="CANCELED">{{ txt('أُلغي', 'Canceled') }}</option>
              </select>
              <input [(ngModel)]="queueFilter.search" type="text" [placeholder]="txt('بحث', 'Search')" class="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm" />
              <button type="button" class="rounded-xl bg-teal-600 px-4 py-2 text-sm font-semibold text-white" (click)="loadQueue()">{{ txt('تحديث', 'Refresh') }}</button>
            </div>
          </div>
          <div class="overflow-x-auto">
            <table class="min-w-full text-sm" [class.text-left]="!langService.isRtl()" [class.text-right]="langService.isRtl()">
              <thead>
                <tr class="border-b border-slate-200 text-xs uppercase tracking-[0.16em] text-slate-500">
                  <th class="py-3 pe-4">{{ txt('العنوان', 'Subject') }}</th>
                  <th class="py-3 pe-4">{{ txt('المستلمون', 'Recipients') }}</th>
                  <th class="py-3 pe-4">{{ txt('الحالة', 'Status') }}</th>
                  <th class="py-3 pe-4">{{ txt('مجدول', 'Scheduled') }}</th>
                  <th class="py-3">{{ txt('الإجراءات', 'Actions') }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of queueItems(); track item.emailQueueItemId) {
                  <tr class="border-b border-slate-100 align-top">
                    <td class="py-4 pe-4">
                      <div class="font-semibold text-slate-900">{{ item.subject }}</div>
                      <div class="mt-1 text-xs text-slate-500">{{ item.emailAccountName }} / {{ item.category }} / {{ item.triggerType }}</div>
                      @if (item.lastError) {
                        <div class="mt-2 rounded-2xl bg-rose-50 px-3 py-2 text-xs text-rose-700">{{ item.lastError }}</div>
                      }
                    </td>
                    <td class="py-4 pe-4 text-xs text-slate-600">{{ (item.to || []).join(', ') }}</td>
                    <td class="py-4 pe-4">
                      <span class="rounded-full px-2.5 py-1 text-[11px] font-bold uppercase tracking-[0.16em]" [class]="statusClass(item.status)">{{ item.status }}</span>
                      <div class="mt-2 text-xs text-slate-500">{{ txt('عدد المحاولات', 'Retries') }}: {{ item.retryCount }}</div>
                    </td>
                    <td class="py-4 pe-4 text-xs text-slate-600">
                      <div>{{ formatDate(item.scheduledAtUtc) }}</div>
                      @if (item.sentAtUtc) {
                        <div class="mt-1 text-emerald-700">{{ txt('تم الإرسال', 'Sent') }} {{ formatDate(item.sentAtUtc) }}</div>
                      }
                    </td>
                    <td class="py-4">
                      <div class="flex flex-wrap gap-2">
                        <button type="button" class="rounded-xl border border-slate-200 px-3 py-2 text-xs font-semibold text-slate-700" (click)="retryQueueItem(item.emailQueueItemId)" [disabled]="item.status === 'SENT'">{{ txt('إعادة محاولة', 'Retry') }}</button>
                        <button type="button" class="rounded-xl border border-rose-200 px-3 py-2 text-xs font-semibold text-rose-700" (click)="cancelQueueItem(item.emailQueueItemId)" [disabled]="item.status === 'SENT' || item.status === 'CANCELED'">{{ txt('إلغاء', 'Cancel') }}</button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (queueItems().length === 0) {
            <div class="py-8 text-center text-sm text-slate-500">{{ txt('لا توجد رسائل بريد في الطابور.', 'No queued emails found.') }}</div>
          }
        </section>
      }
    </div>
  `,
})
export class EmailCenterComponent implements OnInit {
  private static readonly maxAttachmentCount = 10;
  private static readonly maxAttachmentBytes = 5 * 1024 * 1024;
  private static readonly maxAttachmentTotalBytes = 10 * 1024 * 1024;

  private readonly api = inject(ApiService);
  protected readonly langService = inject(LanguageService);
  private readonly messageService = inject(MessageService);
  private readonly tokenService = inject(TokenService);

  protected readonly loading = signal(true);
  protected readonly scope = signal<'company' | 'critical'>(this.tokenService.isSuperAdmin() ? 'critical' : 'company');
  protected readonly dashboard = signal<any>(null);
  protected readonly queueItems = signal<any[]>([]);
  protected readonly accounts = signal<any[]>([]);
  protected readonly rules = signal<any[]>([]);
  protected readonly draftAttachments = signal<any[]>([]);
  protected readonly attachmentError = signal<string | null>(null);
  protected readonly isSuperAdmin = computed(() => this.tokenService.isSuperAdmin());
  protected readonly canManageExpiryRules = computed(() => this.isSuperAdmin() && this.scope() === 'critical');
  protected readonly cards = computed(() => {
    const d = this.dashboard();
    if (!d) return [];
    return [
      {
        label: this.txt('الحسابات', 'Accounts'),
        value: d.activeAccountCount,
        note: this.scope() === 'critical'
          ? this.txt('مجموعة إرسال عامة', 'Global sender set')
          : this.txt('مجموعة إرسال خاصة بالشركة', 'Tenant sender set'),
      },
      { label: this.txt('القواعد', 'Rules'), value: d.activeRuleCount, note: this.txt('إشعارات مجدولة نشطة', 'Active scheduled notifications') },
      { label: this.txt('معلّق', 'Pending'), value: d.pendingCount, note: this.langService.isRtl() ? `مجدول مسبقًا: ${d.scheduledCount}` : `${d.scheduledCount} scheduled ahead` },
      { label: this.txt('مرسل / فاشل', 'Sent / Failed'), value: `${d.sentCount} / ${d.failedCount}`, note: this.langService.isRtl() ? `مستحق خلال 24 ساعة: ${d.dueInNext24Hours}` : `${d.dueInNext24Hours} due in the next 24h` },
    ];
  });

  protected readonly queueFilter = { status: '', search: '' };
  protected compose = this.newCompose();
  protected accountForm = this.newAccount();
  protected ruleForm = this.newRule();

  ngOnInit(): void { this.loadAll(); }

  protected changeScope(scope: 'company' | 'critical'): void { this.scope.set(scope); this.resetAccount(); this.resetRule(); this.loadAll(); }
  protected resetCompose(): void { this.compose = this.newCompose(); this.draftAttachments.set([]); this.attachmentError.set(null); }
  protected resetAccount(): void { this.accountForm = this.newAccount(); }
  protected resetRule(): void { this.ruleForm = this.newRule(); }

  protected editAccount(account: any): void {
    this.accountForm = { ...account, password: '', hasPassword: !!account.hasPassword, replyToAddress: account.replyToAddress ?? '' };
  }

  protected editRule(rule: any): void {
    this.ruleForm = { ...rule, recipientsCsv: (rule.recipients ?? []).join(', ') };
  }

  protected txt(ar: string, en: string): string {
    this.langService.currentLang();
    return this.langService.isRtl() ? ar : en;
  }

  protected formatBytes(v: number): string { return v < 1024 * 1024 ? `${(v / 1024).toFixed(1)} KB` : `${(v / 1024 / 1024).toFixed(2)} MB`; }
  protected removeAttachment(fileName: string): void { this.draftAttachments.update(a => a.filter(x => x.fileName !== fileName)); this.attachmentError.set(this.validateAttachments(this.draftAttachments())); }
  protected formatDate(v: string | null): string {
    if (!v) {
      return '-';
    }

    const date = new Date(v);
    if (Number.isNaN(date.getTime())) {
      return '-';
    }

    const locale = this.langService.isRtl() ? 'ar-EG' : 'en-US';
    return date.toLocaleString(locale);
  }
  protected statusClass(status: string): string { return ({ SENT: 'bg-emerald-100 text-emerald-700', FAILED: 'bg-rose-100 text-rose-700', RETRY: 'bg-amber-100 text-amber-700', CANCELED: 'bg-slate-200 text-slate-700', PENDING: 'bg-sky-100 text-sky-700' } as Record<string, string>)[status] ?? 'bg-slate-200 text-slate-700'; }
  protected composeValidation(): EmailValidationState {
    const errors: string[] = [];
    const recipients = this.analyzeEmailCsv(this.compose.toCsv);

    if (!this.isPositiveId(this.compose.emailAccountId)) errors.push(this.txt('اختر حساب بريد صالح.', 'Select a valid email account.'));
    if (recipients.tokens.length === 0) errors.push(this.txt('أضف مستلمًا واحدًا على الأقل.', 'Add at least one recipient.'));
    else if (recipients.invalid.length > 0) errors.push(this.txt(`صحح عناوين البريد غير الصالحة: ${recipients.invalid.join(', ')}`, `Fix invalid recipient emails: ${recipients.invalid.join(', ')}`));
    if (!this.compose.subject?.trim()) errors.push(this.txt('عنوان الرسالة مطلوب.', 'Subject is required.'));
    if ((this.compose.subject ?? '').trim().length > 300) errors.push(this.txt('عنوان الرسالة لا يجب أن يتجاوز 300 حرف.', 'Subject cannot exceed 300 characters.'));
    if (!this.compose.body?.trim()) errors.push(this.txt('محتوى الرسالة مطلوب.', 'Body is required.'));
    if ((this.compose.body ?? '').length > 20000) errors.push(this.txt('محتوى الرسالة لا يجب أن يتجاوز 20000 حرف.', 'Body cannot exceed 20000 characters.'));
    if ((this.compose.category ?? '').trim().length > 100) errors.push(this.txt('التصنيف لا يجب أن يتجاوز 100 حرف.', 'Category cannot exceed 100 characters.'));
    if (!Number.isInteger(this.compose.priority) || this.compose.priority < 0 || this.compose.priority > 1000) errors.push(this.txt('الأولوية يجب أن تكون بين 0 و1000.', 'Priority must be between 0 and 1000.'));
    if (this.compose.scheduledAtLocal && Number.isNaN(new Date(this.compose.scheduledAtLocal).getTime())) errors.push(this.txt('أدخل تاريخ جدولة صالح.', 'Enter a valid schedule date.'));
    if (this.attachmentError()) errors.push(this.attachmentError() as string);

    return { valid: errors.length === 0, errors, normalizedEmails: recipients.valid };
  }

  protected isComposeValid(): boolean { return this.composeValidation().valid; }

  protected accountValidation(): EmailValidationState {
    const errors: string[] = [];

    if (!this.accountForm.name?.trim()) errors.push(this.txt('اسم الحساب مطلوب.', 'Account name is required.'));
    if (!this.accountForm.fromAddress?.trim()) errors.push(this.txt('بريد المرسل مطلوب.', 'From address is required.'));
    else if (!this.isValidEmail(this.accountForm.fromAddress)) errors.push(this.txt('بريد المرسل غير صالح.', 'From address must be a valid email.'));
    if (this.accountForm.replyToAddress?.trim() && !this.isValidEmail(this.accountForm.replyToAddress)) errors.push(this.txt('بريد الرد غير صالح.', 'Reply-to address must be a valid email.'));
    if (!this.accountForm.fromName?.trim()) errors.push(this.txt('اسم المرسل مطلوب.', 'From name is required.'));
    if (!this.isValidSmtpHost(this.accountForm.smtpHost)) errors.push(this.txt('مضيف SMTP غير صالح.', 'SMTP host must be a valid host name or URL.'));
    if (!Number.isInteger(this.accountForm.smtpPort) || this.accountForm.smtpPort < 1 || this.accountForm.smtpPort > 65535) errors.push(this.txt('منفذ SMTP يجب أن يكون بين 1 و65535.', 'SMTP port must be between 1 and 65535.'));
    if (!this.accountForm.username?.trim()) errors.push(this.txt('اسم مستخدم SMTP مطلوب.', 'SMTP username is required.'));
    if (this.requiresAccountPassword() && !this.accountForm.password?.trim()) errors.push(this.txt('كلمة مرور SMTP مطلوبة.', 'SMTP password is required.'));
    if (this.accountForm.isDefault && !this.accountForm.isActive) errors.push(this.txt('الحساب الافتراضي يجب أن يبقى نشطًا.', 'Default SMTP accounts must stay active.'));

    return { valid: errors.length === 0, errors };
  }

  protected isAccountValid(): boolean { return this.accountValidation().valid; }

  protected ruleValidation(): EmailValidationState {
    const errors: string[] = [];
    const recipients = this.analyzeEmailCsv(this.ruleForm.recipientsCsv);

    if (!this.canManageExpiryRules()) errors.push(this.txt('فقط المشرف العام يمكنه إدارة قواعد الانتهاء.', 'Only super admins can manage expiry rules.'));
    if (!this.ruleForm.name?.trim()) errors.push(this.txt('اسم القاعدة مطلوب.', 'Rule name is required.'));
    if (!this.isPositiveId(this.ruleForm.emailAccountId)) errors.push(this.txt('اختر حساب بريد صالح.', 'Select a valid email account.'));
    if ((this.ruleForm.category ?? '').trim().length > 100) errors.push(this.txt('التصنيف لا يجب أن يتجاوز 100 حرف.', 'Category cannot exceed 100 characters.'));
    if (!Number.isInteger(this.ruleForm.leadTimeDays) || this.ruleForm.leadTimeDays < 0 || this.ruleForm.leadTimeDays > 365) errors.push(this.txt('مدة الإشعار يجب أن تكون بين 0 و365 يومًا.', 'Lead time must be between 0 and 365 days.'));
    if (!['COMPANY_ADMINS', 'COMPANY_EMAIL', 'CUSTOM'].includes(this.ruleForm.recipientMode)) errors.push(this.txt('وضع المستلمين غير صالح.', 'Recipient mode is invalid.'));
    if (this.ruleForm.recipientMode === 'CUSTOM') {
      if (recipients.tokens.length === 0) errors.push(this.txt('أضف مستلمًا مخصصًا واحدًا على الأقل.', 'Add at least one custom recipient.'));
      else if (recipients.invalid.length > 0) errors.push(this.txt(`صحح عناوين البريد المخصصة غير الصالحة: ${recipients.invalid.join(', ')}`, `Fix invalid custom recipient emails: ${recipients.invalid.join(', ')}`));
    }
    if (!this.ruleForm.subjectTemplate?.trim()) errors.push(this.txt('قالب العنوان مطلوب.', 'Subject template is required.'));
    if ((this.ruleForm.subjectTemplate ?? '').trim().length > 300) errors.push(this.txt('قالب العنوان لا يجب أن يتجاوز 300 حرف.', 'Subject template cannot exceed 300 characters.'));
    if (!this.ruleForm.bodyTemplate?.trim()) errors.push(this.txt('قالب المحتوى مطلوب.', 'Body template is required.'));
    if ((this.ruleForm.bodyTemplate ?? '').length > 20000) errors.push(this.txt('قالب المحتوى لا يجب أن يتجاوز 20000 حرف.', 'Body template cannot exceed 20000 characters.'));

    return { valid: errors.length === 0, errors, normalizedEmails: recipients.valid };
  }

  protected isRuleValid(): boolean { return this.ruleValidation().valid; }

  protected loadAll(): void {
    this.loading.set(true);
    this.loadDashboard();
    this.loadQueue();
    this.loadAccounts();
    if (this.canManageExpiryRules()) {
      this.loadRules(() => this.loading.set(false));
      return;
    }

    this.rules.set([]);
    this.loading.set(false);
  }

  protected loadDashboard(): void {
    this.api.get<any>(`${this.basePath()}/dashboard`).subscribe({
      next: data => this.dashboard.set(data),
      error: err => this.handleError(err, this.txt('تعذر تحميل لوحة البريد.', 'Failed to load dashboard.')),
    });
  }

  protected loadQueue(): void {
    const params: Record<string, string> = {};
    if (this.queueFilter.status) params['status'] = this.queueFilter.status;
    if (this.queueFilter.search) params['search'] = this.queueFilter.search;
    this.api.get<any>(`${this.basePath()}/queue`, params).subscribe({
      next: data => this.queueItems.set(data.items ?? []),
      error: err => this.handleError(err, this.txt('تعذر تحميل طابور البريد.', 'Failed to load queue.')),
    });
  }

  protected loadAccounts(): void {
    this.api.get<any[]>(`${this.basePath()}/accounts`).subscribe({
      next: data => this.accounts.set(data),
      error: err => this.handleError(err, this.txt('تعذر تحميل الحسابات.', 'Failed to load accounts.')),
    });
  }

  protected loadRules(onDone?: () => void): void {
    this.api.get<any[]>(`${this.basePath()}/rules`).subscribe({
      next: data => { this.rules.set(data); onDone?.(); },
      error: err => { this.handleError(err, this.txt('تعذر تحميل القواعد.', 'Failed to load rules.')); onDone?.(); },
    });
  }

  protected queueEmail(): void {
    const validation = this.composeValidation();
    if (!validation.valid) {
      this.toast('error', validation.errors[0] || this.txt('أكمل الحقول المطلوبة أولًا.', 'Complete the required fields first.'));
      return;
    }

    const payload = {
      emailAccountId: this.compose.emailAccountId,
      to: validation.normalizedEmails ?? [],
      subject: this.compose.subject.trim(),
      body: this.compose.body,
      category: this.compose.category?.trim() || 'manual',
      priority: this.compose.priority,
      isBodyHtml: this.compose.isBodyHtml,
      scheduledAtUtc: this.compose.scheduledAtLocal ? new Date(this.compose.scheduledAtLocal).toISOString() : null,
      attachments: this.draftAttachments().map(a => ({ fileName: a.fileName, contentType: a.contentType, contentBase64: a.contentBase64 })),
    };
    this.api.post<any>(`${this.basePath()}/queue`, payload).subscribe({
      next: () => { this.toast('success', this.txt('تمت إضافة البريد إلى الطابور.', 'Email queued.')); this.resetCompose(); this.loadDashboard(); this.loadQueue(); },
      error: err => this.handleError(err, this.txt('تعذر إضافة البريد إلى الطابور.', 'Failed to queue email.')),
    });
  }

  protected saveAccount(): void {
    const validation = this.accountValidation();
    if (!validation.valid) {
      this.toast('error', validation.errors[0] || this.txt('أكمل الحقول المطلوبة أولًا.', 'Complete the required fields first.'));
      return;
    }

    const payload = {
      name: this.accountForm.name.trim(),
      fromAddress: this.accountForm.fromAddress.trim(),
      replyToAddress: this.accountForm.replyToAddress?.trim() || null,
      fromName: this.accountForm.fromName.trim(),
      smtpHost: this.normalizeHost(this.accountForm.smtpHost),
      smtpPort: this.accountForm.smtpPort,
      enableSsl: this.accountForm.enableSsl,
      username: this.accountForm.username.trim(),
      password: this.accountForm.password?.trim() || null,
      isDefault: this.accountForm.isDefault,
      isActive: this.accountForm.isActive,
    };
    const request = this.accountForm.emailAccountId
      ? this.api.put<any>(`${this.basePath()}/accounts/${this.accountForm.emailAccountId}`, payload)
      : this.api.post<any>(`${this.basePath()}/accounts`, payload);
    request.subscribe({
      next: () => { this.toast('success', this.txt('تم حفظ الحساب.', 'Account saved.')); this.resetAccount(); this.loadDashboard(); this.loadAccounts(); },
      error: err => this.handleError(err, this.txt('تعذر حفظ الحساب.', 'Failed to save account.')),
    });
  }

  protected deleteAccount(id: number): void {
    this.api.delete<any>(`${this.basePath()}/accounts/${id}`).subscribe({
      next: () => { this.toast('success', this.txt('تم حذف الحساب.', 'Account deleted.')); this.resetAccount(); this.loadDashboard(); this.loadAccounts(); },
      error: err => this.handleError(err, this.txt('تعذر حذف الحساب.', 'Failed to delete account.')),
    });
  }

  protected saveRule(): void {
    const validation = this.ruleValidation();
    if (!validation.valid) {
      this.toast('error', validation.errors[0] || this.txt('أكمل الحقول المطلوبة أولًا.', 'Complete the required fields first.'));
      return;
    }

    const payload = {
      name: this.ruleForm.name.trim(),
      emailAccountId: this.ruleForm.emailAccountId,
      category: this.ruleForm.category?.trim() || 'subscription',
      triggerType: 'COMPANY_SUBSCRIPTION_EXPIRY',
      leadTimeDays: this.ruleForm.leadTimeDays,
      recipientMode: this.ruleForm.recipientMode,
      recipients: this.ruleForm.recipientMode === 'CUSTOM' ? validation.normalizedEmails ?? [] : [],
      subjectTemplate: this.ruleForm.subjectTemplate.trim(),
      bodyTemplate: this.ruleForm.bodyTemplate,
      isBodyHtml: this.ruleForm.isBodyHtml,
      isActive: this.ruleForm.isActive,
    };
    const request = this.ruleForm.emailNotificationRuleId
      ? this.api.put<any>(`${this.basePath()}/rules/${this.ruleForm.emailNotificationRuleId}`, payload)
      : this.api.post<any>(`${this.basePath()}/rules`, payload);
    request.subscribe({
      next: () => { this.toast('success', this.txt('تم حفظ القاعدة.', 'Rule saved.')); this.resetRule(); this.loadDashboard(); this.loadRules(); },
      error: err => this.handleError(err, this.txt('تعذر حفظ القاعدة.', 'Failed to save rule.')),
    });
  }

  protected deleteRule(id: number): void {
    this.api.delete<any>(`${this.basePath()}/rules/${id}`).subscribe({
      next: () => { this.toast('success', this.txt('تم حذف القاعدة.', 'Rule deleted.')); this.resetRule(); this.loadDashboard(); this.loadRules(); },
      error: err => this.handleError(err, this.txt('تعذر حذف القاعدة.', 'Failed to delete rule.')),
    });
  }

  protected cancelQueueItem(id: number): void {
    this.api.post<any>(`${this.basePath()}/queue/${id}/cancel`, {}).subscribe({
      next: () => { this.toast('success', this.txt('تم إلغاء البريد الموجود في الطابور.', 'Queued email canceled.')); this.loadDashboard(); this.loadQueue(); },
      error: err => this.handleError(err, this.txt('تعذر إلغاء البريد الموجود في الطابور.', 'Failed to cancel queued email.')),
    });
  }

  protected retryQueueItem(id: number): void {
    this.api.post<any>(`${this.basePath()}/queue/${id}/retry`, {}).subscribe({
      next: () => { this.toast('success', this.txt('تمت إعادة تهيئة البريد للمحاولة مرة أخرى.', 'Queued email reset for retry.')); this.loadDashboard(); this.loadQueue(); },
      error: err => this.handleError(err, this.txt('تعذر إعادة محاولة البريد الموجود في الطابور.', 'Failed to retry queued email.')),
    });
  }

  protected onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (files.length === 0) return;

    Promise.all(files.map(file => this.fileToAttachment(file)))
      .then(values => {
        const next = [...this.draftAttachments(), ...values];
        const attachmentError = this.validateAttachments(next);
        if (attachmentError) {
          this.attachmentError.set(attachmentError);
          this.toast('error', attachmentError);
          return;
        }

        this.attachmentError.set(null);
        this.draftAttachments.set(next);
      })
      .catch(() => this.toast('error', this.txt('تعذر قراءة المرفق.', 'Failed to read attachment.')))
      .finally(() => { input.value = ''; });
  }

  private handleError(err: any, fallback: string): void {
    this.loading.set(false);
    this.toast('error', err?.error?.message || fallback);
  }

  private toast(severity: 'success' | 'error', summary: string): void {
    this.messageService.add({ severity, summary, life: 3000 });
  }

  private basePath(): string { return this.scope() === 'critical' ? '/super-admin/email' : '/email'; }
  private isPositiveId(value: unknown): value is number { return typeof value === 'number' && Number.isInteger(value) && value > 0; }
  private requiresAccountPassword(): boolean { return !this.accountForm.emailAccountId || !this.accountForm.hasPassword; }
  private isValidEmail(value: string): boolean { const candidate = value?.trim(); return !!candidate && /^[^\s@]+@[^\s@]+\.[^\s@]+$/i.test(candidate); }
  private analyzeEmailCsv(value: string): { tokens: string[]; valid: string[]; invalid: string[] } {
    const tokens = value.split(',').map(x => x.trim()).filter(Boolean);
    const valid: string[] = [];
    const invalid: string[] = [];

    for (const token of tokens) {
      if (!this.isValidEmail(token)) {
        invalid.push(token);
        continue;
      }

      const normalized = token.toLowerCase();
      if (!valid.includes(normalized)) valid.push(normalized);
    }

    return { tokens, valid, invalid };
  }
  private normalizeHost(value: string): string {
    const trimmed = value?.trim() ?? '';
    if (!trimmed) return '';

    try {
      const url = trimmed.includes('://') ? new URL(trimmed) : new URL(`smtp://${trimmed}`);
      return url.hostname.trim();
    } catch {
      return trimmed.replace(/^https?:\/\//i, '').replace(/\/+$/, '').trim();
    }
  }
  private isValidSmtpHost(value: string): boolean {
    const normalized = this.normalizeHost(value);
    return !!normalized && !/\s/.test(normalized) && /^[a-z0-9.-]+$/i.test(normalized);
  }
  private validateAttachments(attachments: Array<{ fileName: string; sizeBytes: number }>): string | null {
    if (attachments.length > EmailCenterComponent.maxAttachmentCount) {
      return this.txt(
        `الحد الأقصى للمرفقات هو ${EmailCenterComponent.maxAttachmentCount}.`,
        `A maximum of ${EmailCenterComponent.maxAttachmentCount} attachments is allowed.`,
      );
    }

    let totalBytes = 0;
    for (const attachment of attachments) {
      totalBytes += attachment.sizeBytes;
      if (attachment.sizeBytes > EmailCenterComponent.maxAttachmentBytes) {
        return this.txt(
          `المرفق "${attachment.fileName}" يتجاوز حد 5 ميجابايت.`,
          `Attachment "${attachment.fileName}" exceeds the 5 MB limit.`,
        );
      }
    }

    if (totalBytes > EmailCenterComponent.maxAttachmentTotalBytes) {
      return this.txt(
        'إجمالي حجم المرفقات يتجاوز حد 10 ميجابايت.',
        'Combined attachment size exceeds the 10 MB limit.',
      );
    }
    return null;
  }

  private async fileToAttachment(file: File): Promise<any> {
    const dataUrl = await new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result ?? ''));
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(file);
    });
    return { fileName: file.name, contentType: file.type || 'application/octet-stream', contentBase64: dataUrl.split(',')[1] ?? '', sizeBytes: file.size };
  }

  private newCompose() { return { emailAccountId: 0, toCsv: '', subject: '', body: '', category: 'manual', priority: 100, isBodyHtml: false, scheduledAtLocal: '' }; }
  private newAccount() { return { emailAccountId: 0, name: '', fromAddress: 'noreply@botglobalservice.com', replyToAddress: '', fromName: 'Bot Global Service', smtpHost: 'mail.botglobalservice.com', smtpPort: 587, username: 'noreply@botglobalservice.com', password: '', hasPassword: false, enableSsl: true, isDefault: true, isActive: true }; }
  private newRule() {
    return {
      emailNotificationRuleId: 0,
      name: '',
      emailAccountId: 0,
      category: 'subscription',
      leadTimeDays: 7,
      recipientMode: 'COMPANY_ADMINS',
      recipientsCsv: '',
      subjectTemplate: this.langService.isRtl()
        ? 'ينتهي اشتراك {{companyName}} بعد {{daysUntilExpiry}} يوم.'
        : 'Subscription expires in {{daysUntilExpiry}} day(s) for {{companyName}}',
      bodyTemplate: this.langService.isRtl()
        ? 'ستنتهي خدمة الشركة {{companyName}} في {{expiryDate}}. تواصل عبر {{companyEmail}} قبل انقطاع الخدمة.'
        : 'Company {{companyName}} is due to expire on {{expiryDate}}. Contact {{companyEmail}} before service interruption.',
      isBodyHtml: false,
      isActive: true
    };
  }
}
