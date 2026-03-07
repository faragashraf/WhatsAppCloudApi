import { Component, inject, OnInit, signal } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatTableModule } from '@angular/material/table';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ApiService } from '../../../core/services';
import { WhatsAppAccount, WhatsAppAccountUpsertRequest } from '../../../core/models';

@Component({
  selector: 'app-instances',
  standalone: true,
  imports: [
    FormsModule, SlicePipe, MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule,
    MatTableModule, MatDialogModule, MatSnackBarModule, MatProgressSpinnerModule,
    MatChipsModule, MatSlideToggleModule,
  ],
  template: `
    <div class="space-y-6">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">API Instances</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">Manage your WhatsApp Business API accounts</p>
        </div>
        <button mat-flat-button (click)="showForm.set(!showForm())"
          class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700">
          <mat-icon>{{ showForm() ? 'close' : 'add' }}</mat-icon>
          {{ showForm() ? 'Cancel' : 'Add Instance' }}
        </button>
      </div>

      <!-- Create Form -->
      @if (showForm()) {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 space-y-4">
          <h3 class="text-lg font-semibold text-slate-900 dark:text-white">{{ editingId() ? 'Edit' : 'New' }} WhatsApp Account</h3>
          <div class="grid md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline">
              <mat-label>Business Account ID (WABA ID)</mat-label>
              <input matInput [(ngModel)]="form.businessAccountId" name="businessAccountId">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Display Name</mat-label>
              <input matInput [(ngModel)]="form.name" name="name">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Access Token</mat-label>
              <input matInput [(ngModel)]="form.accessToken" name="accessToken" type="password">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Verify Token (Webhook)</mat-label>
              <input matInput [(ngModel)]="form.verifyToken" name="verifyToken">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>App Secret (optional)</mat-label>
              <input matInput [(ngModel)]="form.appSecret" name="appSecret" type="password">
            </mat-form-field>
          </div>
          <div class="flex items-center gap-4">
            <mat-slide-toggle [(ngModel)]="form.isDefault">Default Account</mat-slide-toggle>
            <mat-slide-toggle [(ngModel)]="form.isActive">Active</mat-slide-toggle>
          </div>
          <button mat-flat-button (click)="onSave()" [disabled]="saving()"
            class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700">
            @if (saving()) { <mat-spinner diameter="18" class="!inline-block mr-2"></mat-spinner> }
            {{ editingId() ? 'Update' : 'Create' }}
          </button>
        </div>
      }

      <!-- List -->
      @if (loading()) {
        <div class="flex justify-center py-16"><mat-spinner diameter="36"></mat-spinner></div>
      } @else if (accounts().length === 0) {
        <div class="text-center py-20 bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50">
          <mat-icon class="!text-[48px] text-slate-300 dark:text-slate-600 mb-3">router</mat-icon>
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
                    <mat-icon [class]="account.isActive ? '!text-emerald-600' : '!text-slate-400'">router</mat-icon>
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

              <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mt-4 pt-4 border-t border-slate-100 dark:border-slate-700/30">
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

              <div class="flex gap-2 mt-4">
                <button mat-stroked-button (click)="onEdit(account)" class="!rounded-lg !text-sm">
                  <mat-icon class="!text-[16px]">edit</mat-icon> Edit
                </button>
                <button mat-stroked-button (click)="onDelete(account)" class="!rounded-lg !text-sm !text-red-600 !border-red-200">
                  <mat-icon class="!text-[16px]">delete</mat-icon> Delete
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
  private snackBar = inject(MatSnackBar);

  accounts = signal<WhatsAppAccount[]>([]);
  loading = signal(true);
  saving = signal(false);
  showForm = signal(false);
  editingId = signal<string | null>(null);

  form: WhatsAppAccountUpsertRequest = this.emptyForm();
  webhookUrl = window.location.origin.replace(/:\d+$/, '') + ':7118/api/webhook';

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
        this.snackBar.open('Account saved!', 'Close', { duration: 3000 });
        this.loadAccounts();
      },
      error: (err) => {
        this.saving.set(false);
        this.snackBar.open(err.error?.message || 'Failed to save', 'Close', { duration: 4000 });
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
        this.snackBar.open('Account deleted', 'Close', { duration: 3000 });
        this.loadAccounts();
      },
      error: (err) => this.snackBar.open(err.error?.message || 'Failed to delete', 'Close', { duration: 4000 }),
    });
  }

  private emptyForm(): WhatsAppAccountUpsertRequest {
    return { businessAccountId: '', name: '', accessToken: '', verifyToken: '', appSecret: '', isDefault: false, isActive: true };
  }
}
