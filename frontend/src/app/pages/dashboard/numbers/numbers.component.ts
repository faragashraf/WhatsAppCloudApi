import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ApiService } from '../../../core/services';
import { WhatsAppPhoneNumber, PhoneNumberUpsertRequest } from '../../../core/models';

@Component({
  selector: 'app-numbers',
  standalone: true,
  imports: [
    FormsModule, MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule,
    MatSnackBarModule, MatProgressSpinnerModule, MatSlideToggleModule,
  ],
  template: `
    <div class="space-y-6">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">Phone Numbers</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">Manage your WhatsApp phone numbers</p>
        </div>
        <button mat-flat-button (click)="showForm.set(!showForm())"
          class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700">
          <mat-icon>{{ showForm() ? 'close' : 'add' }}</mat-icon>
          {{ showForm() ? 'Cancel' : 'Add Number' }}
        </button>
      </div>

      <!-- Search -->
      <mat-form-field appearance="outline" class="w-full max-w-sm">
        <mat-label>Search numbers</mat-label>
        <input matInput [(ngModel)]="searchQuery" (ngModelChange)="filterNumbers()" placeholder="Search by name or number...">
        <mat-icon matPrefix class="!text-slate-400 mr-2">search</mat-icon>
      </mat-form-field>

      <!-- Create Form -->
      @if (showForm()) {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 space-y-4">
          <h3 class="text-lg font-semibold text-slate-900 dark:text-white">{{ editingId() ? 'Edit' : 'Add' }} Phone Number</h3>
          <div class="grid md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline">
              <mat-label>WhatsApp Account ID</mat-label>
              <input matInput [(ngModel)]="form.whatsAppAccountId" name="whatsAppAccountId">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Phone Number ID (Meta)</mat-label>
              <input matInput [(ngModel)]="form.phoneNumberId" name="phoneNumberId">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Display Phone Number</mat-label>
              <input matInput [(ngModel)]="form.displayPhoneNumber" name="displayPhoneNumber">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Verified Name</mat-label>
              <input matInput [(ngModel)]="form.verifiedName" name="verifiedName">
            </mat-form-field>
          </div>
          <div class="flex items-center gap-4">
            <mat-slide-toggle [(ngModel)]="form.isDefault">Default</mat-slide-toggle>
            <mat-slide-toggle [(ngModel)]="form.isActive">Active</mat-slide-toggle>
          </div>
          <button mat-flat-button (click)="onSave()" [disabled]="saving()"
            class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700">
            @if (saving()) { <mat-spinner diameter="18" class="!inline-block mr-2"></mat-spinner> }
            {{ editingId() ? 'Update' : 'Create' }}
          </button>
        </div>
      }

      <!-- Table -->
      @if (loading()) {
        <div class="flex justify-center py-16"><mat-spinner diameter="36"></mat-spinner></div>
      } @else if (filteredNumbers().length === 0) {
        <div class="text-center py-20 bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50">
          <mat-icon class="!text-[48px] text-slate-300 dark:text-slate-600 mb-3">phone_iphone</mat-icon>
          <h3 class="text-lg font-semibold text-slate-700 dark:text-slate-300 mb-1">No phone numbers</h3>
          <p class="text-sm text-slate-500">Add your first WhatsApp phone number to start sending messages.</p>
        </div>
      } @else {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 overflow-hidden">
          <div class="overflow-x-auto">
            <table class="w-full">
              <thead>
                <tr class="border-b border-slate-200 dark:border-slate-700/50">
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Number</th>
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Display Name</th>
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Status</th>
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Verified</th>
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Default</th>
                  <th class="text-right text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (phone of filteredNumbers(); track phone.whatsAppPhoneNumberId) {
                  <tr class="border-b border-slate-100 dark:border-slate-700/20 hover:bg-slate-50 dark:hover:bg-slate-800/30 transition-colors">
                    <td class="px-6 py-4">
                      <span class="text-sm font-mono font-medium text-slate-900 dark:text-white">{{ phone.phoneNumberId }}</span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="text-sm text-slate-700 dark:text-slate-300">{{ phone.displayPhoneNumber }}</span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="inline-flex items-center gap-1.5 text-xs font-medium px-2.5 py-1 rounded-full"
                        [class]="phone.isActive ? 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400' : 'bg-slate-100 dark:bg-slate-700 text-slate-600'">
                        <span class="w-1.5 h-1.5 rounded-full" [class]="phone.isActive ? 'bg-emerald-500' : 'bg-slate-400'"></span>
                        {{ phone.isActive ? 'Active' : 'Inactive' }}
                      </span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="text-sm text-slate-600 dark:text-slate-400">{{ phone.verifiedName || '—' }}</span>
                    </td>
                    <td class="px-6 py-4">
                      @if (phone.isDefault) {
                        <span class="text-xs px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-400 font-medium">Default</span>
                      }
                    </td>
                    <td class="px-6 py-4 text-right">
                      <button mat-icon-button (click)="onEdit(phone)" class="!text-slate-400 hover:!text-emerald-600">
                        <mat-icon class="!text-[18px]">edit</mat-icon>
                      </button>
                      <button mat-icon-button (click)="onDelete(phone)" class="!text-slate-400 hover:!text-red-600">
                        <mat-icon class="!text-[18px]">delete</mat-icon>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </div>
  `,
})
export class NumbersComponent implements OnInit {
  private api = inject(ApiService);
  private snackBar = inject(MatSnackBar);

  numbers = signal<WhatsAppPhoneNumber[]>([]);
  filteredNumbers = signal<WhatsAppPhoneNumber[]>([]);
  loading = signal(true);
  saving = signal(false);
  showForm = signal(false);
  editingId = signal<number | null>(null);
  searchQuery = '';

  form: PhoneNumberUpsertRequest = this.emptyForm();

  ngOnInit(): void { this.loadNumbers(); }

  loadNumbers(): void {
    this.api.get<WhatsAppPhoneNumber[]>('/phone-numbers').subscribe({
      next: (data) => {
        this.numbers.set(data);
        this.filteredNumbers.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  filterNumbers(): void {
    const q = this.searchQuery.toLowerCase();
    this.filteredNumbers.set(
      this.numbers().filter((n) =>
        n.displayPhoneNumber.toLowerCase().includes(q) ||
        n.phoneNumberId.toLowerCase().includes(q) ||
        (n.verifiedName?.toLowerCase().includes(q) ?? false)
      ),
    );
  }

  onSave(): void {
    this.saving.set(true);
    const obs = this.editingId()
      ? this.api.put<WhatsAppPhoneNumber>(`/phone-numbers/${this.editingId()}`, this.form)
      : this.api.post<WhatsAppPhoneNumber>('/phone-numbers', this.form);
    obs.subscribe({
      next: () => {
        this.saving.set(false); this.showForm.set(false); this.editingId.set(null);
        this.form = this.emptyForm();
        this.snackBar.open('Phone number saved!', 'Close', { duration: 3000 });
        this.loadNumbers();
      },
      error: (err) => {
        this.saving.set(false);
        this.snackBar.open(err.error?.message || 'Failed to save', 'Close', { duration: 4000 });
      },
    });
  }

  onEdit(phone: WhatsAppPhoneNumber): void {
    this.editingId.set(phone.whatsAppPhoneNumberId);
    this.form = {
      whatsAppAccountId: phone.whatsAppAccountId,
      phoneNumberId: phone.phoneNumberId,
      displayPhoneNumber: phone.displayPhoneNumber,
      verifiedName: phone.verifiedName,
      isDefault: phone.isDefault,
      isActive: phone.isActive,
    };
    this.showForm.set(true);
  }

  onDelete(phone: WhatsAppPhoneNumber): void {
    if (!confirm(`Delete number "${phone.displayPhoneNumber}"?`)) return;
    this.api.delete(`/phone-numbers/${phone.whatsAppPhoneNumberId}`).subscribe({
      next: () => { this.snackBar.open('Deleted', 'Close', { duration: 3000 }); this.loadNumbers(); },
      error: (err) => this.snackBar.open(err.error?.message || 'Failed', 'Close', { duration: 4000 }),
    });
  }

  private emptyForm(): PhoneNumberUpsertRequest {
    return { whatsAppAccountId: '', phoneNumberId: '', displayPhoneNumber: '', verifiedName: '', isDefault: false, isActive: true };
  }
}
