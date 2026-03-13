import { Component, inject, OnInit, signal } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageService } from 'primeng/api';
import { ApiService } from '../../../core/services';
import { WhatsAppAccount, WhatsAppAccountUpsertRequest } from '../../../core/models';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-instances',
  standalone: true,
  imports: [
    FormsModule, SlicePipe, ButtonModule, InputTextModule, ToastModule,
    ProgressSpinnerModule, ToggleSwitchModule,
  ],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="space-y-6 min-w-0 app-wrap-safe">
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">API Instances</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">Manage your WhatsApp Business API accounts</p>
        </div>
        <button pButton (click)="showForm.set(!showForm())"
          class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700 w-full sm:!w-auto">
          @if (showForm()) { <i class="pi pi-times"></i> } @else { <i class="pi pi-plus"></i> }
          {{ showForm() ? 'Cancel' : 'Add Instance' }}
        </button>
      </div>

      <!-- Create Form -->
      @if (showForm()) {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 space-y-4">
          <h3 class="text-lg font-semibold text-slate-900 dark:text-white">{{ editingId() ? 'Edit' : 'New' }} WhatsApp Account</h3>
          <div class="grid lg:grid-cols-2 gap-4">
            <div class="flex flex-col gap-1">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Business Account ID (WABA ID)</label>
              <input pInputText [(ngModel)]="form.businessAccountId" name="businessAccountId" class="w-full">
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Display Name</label>
              <input pInputText [(ngModel)]="form.name" name="name" class="w-full">
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Access Token</label>
              <input pInputText [(ngModel)]="form.accessToken" name="accessToken" type="password" class="w-full">
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Verify Token (Webhook)</label>
              <input pInputText [(ngModel)]="form.verifyToken" name="verifyToken" class="w-full">
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">App Secret (optional)</label>
              <input pInputText [(ngModel)]="form.appSecret" name="appSecret" type="password" class="w-full">
            </div>
          </div>
          <div class="flex items-center gap-4">
            <div class="flex items-center gap-2"><p-toggleSwitch [(ngModel)]="form.isDefault" /><span class="text-sm text-slate-700 dark:text-slate-300">Default Account</span></div>
            <div class="flex items-center gap-2"><p-toggleSwitch [(ngModel)]="form.isActive" /><span class="text-sm text-slate-700 dark:text-slate-300">Active</span></div>
          </div>
          <button pButton (click)="onSave()" [disabled]="saving()"
            class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700">
            @if (saving()) { <p-progressSpinner [style]="{'width':'18px','height':'18px'}" strokeWidth="4" class="inline-block mr-2" /> }
            {{ editingId() ? 'Update' : 'Create' }}
          </button>
        </div>
      }

      <!-- List -->
      @if (loading()) {
        <div class="flex justify-center py-16"><p-progressSpinner [style]="{'width':'36px','height':'36px'}" strokeWidth="4" /></div>
      } @else if (accounts().length === 0) {
        <div class="text-center py-20 bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50">
          <i class="pi pi-server text-[48px] text-slate-300 dark:text-slate-600 mb-3"></i>
          <h3 class="text-lg font-semibold text-slate-700 dark:text-slate-300 mb-1">No API instances yet</h3>
          <p class="text-sm text-slate-500 dark:text-slate-400">Create your first WhatsApp Business API instance to get started.</p>
        </div>
      } @else {
        <div class="grid gap-4">
          @for (account of accounts(); track account.whatsAppAccountId) {
            <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 hover:shadow-lg transition-shadow">
              <div class="flex items-start justify-between">
                <div class="flex items-center gap-4">
                  <div class="w-12 h-12 rounded-xl flex items-center justify-center"
                    [class]="account.isActive ? 'bg-emerald-100 dark:bg-emerald-900/40' : 'bg-slate-100 dark:bg-slate-700'">
                    <i [class]="'pi pi-server ' + (account.isActive ? 'text-emerald-600' : 'text-slate-400')"></i>
                  </div>
                  <div>
                    <div class="flex items-center gap-2">
                      <h3 class="font-semibold text-slate-900 dark:text-white">{{ account.name }}</h3>
                      @if (account.isDefault) {
                        <span class="text-xs px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-400 font-medium">Default</span>
                      }
                    </div>
                    <p class="text-xs text-slate-500 mt-0.5">WABA: {{ account.whatsAppAccountId }}</p>
                  </div>
                </div>
                <div class="flex items-center gap-2">
                  <span class="w-2 h-2 rounded-full" [class]="account.isActive ? 'bg-emerald-500' : 'bg-slate-400'"></span>
                  <span class="text-xs font-medium" [class]="account.isActive ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-500'">
                    {{ account.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </div>
              </div>

              <div class="grid grid-cols-2 xl:grid-cols-4 gap-4 mt-4 pt-4 border-t border-slate-100 dark:border-slate-700/30">
                <div>
                  <div class="text-xs text-slate-500 mb-1">Business Account ID</div>
                  <div class="text-sm font-mono text-slate-700 dark:text-slate-300 truncate">{{ account.businessAccountId }}</div>
                </div>
                <div>
                  <div class="text-xs text-slate-500 mb-1">Verify Token</div>
                  <div class="text-sm font-mono text-slate-700 dark:text-slate-300 truncate">{{ account.verifyToken }}</div>
                </div>
                <div>
                  <div class="text-xs text-slate-500 mb-1">Webhook URL</div>
                  <div class="text-sm font-mono text-slate-700 dark:text-slate-300 truncate">{{ webhookUrl }}</div>
                </div>
                <div>
                  <div class="text-xs text-slate-500 mb-1">Created</div>
                  <div class="text-sm text-slate-700 dark:text-slate-300">{{ account.createdAtUtc | slice:0:10 }}</div>
                </div>
              </div>

              <div class="flex flex-wrap gap-2 mt-4">
                <button pButton [outlined]="true" (click)="onEdit(account)" class="!rounded-lg !text-sm">
                  <i class="pi pi-pencil"></i> Edit
                </button>
                <button pButton [outlined]="true" (click)="onDelete(account)" class="!rounded-lg !text-sm !text-red-600 !border-red-200">
                  <i class="pi pi-trash"></i> Delete
                </button>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class InstancesComponent implements OnInit {
  private api = inject(ApiService);
  private messageService = inject(MessageService);

  accounts = signal<WhatsAppAccount[]>([]);
  loading = signal(true);
  saving = signal(false);
  showForm = signal(false);
  editingId = signal<string | null>(null);

  form: WhatsAppAccountUpsertRequest = this.emptyForm();
  webhookUrl = this.buildWebhookUrlFromApiBase();

  ngOnInit(): void {
    this.loadAccounts();
  }

  loadAccounts(): void {
    this.api.get<WhatsAppAccount[]>('/whatsapp-accounts').subscribe({
      next: (data) => { this.accounts.set(data); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  onSave(): void {
    this.saving.set(true);
    const obs = this.editingId()
      ? this.api.put<WhatsAppAccount>(`/whatsapp-accounts/${this.editingId()}`, this.form)
      : this.api.post<WhatsAppAccount>('/whatsapp-accounts', this.form);

    obs.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.editingId.set(null);
        this.form = this.emptyForm();
        this.messageService.add({severity: 'success', summary: 'Account saved!', life: 3000});
        this.loadAccounts();
      },
      error: (err) => {
        this.saving.set(false);
        this.messageService.add({severity: 'error', summary: err.error?.message || 'Failed to save', life: 4000});
      },
    });
  }

  onEdit(account: WhatsAppAccount): void {
    this.editingId.set(account.whatsAppAccountId);
    this.form = {
      businessAccountId: account.businessAccountId,
      name: account.name,
      accessToken: account.accessToken,
      verifyToken: account.verifyToken,
      appSecret: account.appSecret,
      isDefault: account.isDefault,
      isActive: account.isActive,
    };
    this.showForm.set(true);
  }

  onDelete(account: WhatsAppAccount): void {
    if (!confirm(`Delete instance "${account.name}"?`)) return;
    this.api.delete(`/whatsapp-accounts/${account.whatsAppAccountId}`).subscribe({
      next: () => {
        this.messageService.add({severity: 'success', summary: 'Account deleted', life: 3000});
        this.loadAccounts();
      },
      error: (err) => this.messageService.add({severity: 'error', summary: err.error?.message || 'Failed to delete', life: 4000}),
    });
  }

  private emptyForm(): WhatsAppAccountUpsertRequest {
    return { businessAccountId: '', name: '', accessToken: '', verifyToken: '', appSecret: '', isDefault: false, isActive: true };
  }

  private buildWebhookUrlFromApiBase(): string {
    const apiUrl = environment.apiUrl.trim();
    const normalizedApiPath = apiUrl.replace(/\/+$/, '').replace(/\/api$/, '/api');

    if (/^https?:\/\//i.test(normalizedApiPath)) {
      return normalizedApiPath.replace(/\/api$/, '') + '/api/webhook';
    }

    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    const relativePath = normalizedApiPath.startsWith('/') ? normalizedApiPath : `/${normalizedApiPath}`;
    return `${origin}${relativePath.replace(/\/api$/, '')}/api/webhook`;
  }
}
