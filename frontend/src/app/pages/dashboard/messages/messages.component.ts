import { Component, inject, OnInit, signal } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { ApiService } from '../../../core/services';
import { Message } from '../../../core/models';

@Component({
  selector: 'app-messages',
  standalone: true,
  imports: [
    FormsModule, SlicePipe, MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule,
    MatProgressSpinnerModule, MatSelectModule,
  ],
  template: `
    <div class="space-y-6">
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">Message Logs</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">View all sent and received messages</p>
      </div>

      <!-- Filters -->
      <div class="flex flex-wrap gap-3">
        <mat-form-field appearance="outline" class="!w-64">
          <mat-label>Search</mat-label>
          <input matInput [(ngModel)]="searchQuery" (ngModelChange)="applyFilters()" placeholder="Recipient number...">
          <mat-icon matPrefix class="!text-slate-400 mr-2">search</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline" class="!w-40">
          <mat-label>Status</mat-label>
          <mat-select [(ngModel)]="statusFilter" (ngModelChange)="applyFilters()">
            <mat-option value="">All</mat-option>
            <mat-option value="SENT">Sent</mat-option>
            <mat-option value="PENDING">Pending</mat-option>
            <mat-option value="FAILED">Failed</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="!w-40">
          <mat-label>Type</mat-label>
          <mat-select [(ngModel)]="typeFilter" (ngModelChange)="applyFilters()">
            <mat-option value="">All</mat-option>
            <mat-option value="TEXT">Text</mat-option>
            <mat-option value="TEMPLATE">Template</mat-option>
            <mat-option value="IMAGE">Image</mat-option>
            <mat-option value="DOCUMENT">Document</mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-16"><mat-spinner diameter="36"></mat-spinner></div>
      } @else if (filteredMessages().length === 0) {
        <div class="text-center py-20 bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50">
          <mat-icon class="!text-[48px] text-slate-300 dark:text-slate-600 mb-3">message</mat-icon>
          <h3 class="text-lg font-semibold text-slate-700 dark:text-slate-300 mb-1">No messages</h3>
          <p class="text-sm text-slate-500">Messages will appear here once you start sending.</p>
        </div>
      } @else {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 overflow-hidden">
          <div class="overflow-x-auto">
            <table class="w-full">
              <thead>
                <tr class="border-b border-slate-200 dark:border-slate-700/50">
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Recipient</th>
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Type</th>
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Status</th>
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Timestamp</th>
                  <th class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">Error</th>
                </tr>
              </thead>
              <tbody>
                @for (msg of paginatedMessages(); track msg.messageId) {
                  <tr class="border-b border-slate-100 dark:border-slate-700/20 hover:bg-slate-50 dark:hover:bg-slate-800/30 transition-colors">
                    <td class="px-6 py-4">
                      <span class="text-sm font-mono font-medium text-slate-900 dark:text-white">{{ msg.toNumber }}</span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="text-xs px-2.5 py-1 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 font-medium">
                        {{ msg.messageType }}
                      </span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="inline-flex items-center gap-1.5 text-xs font-medium px-2.5 py-1 rounded-full"
                        [class]="msg.status === 'SENT' ? 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400'
                          : msg.status === 'FAILED' ? 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-400'
                          : 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-400'">
                        {{ msg.status }}
                      </span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="text-sm text-slate-600 dark:text-slate-400">{{ msg.createdAtUtc | slice:0:19 }}</span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="text-sm text-red-500 truncate max-w-[200px] block">{{ msg.failureReason || '—' }}</span>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Pagination -->
          <div class="flex items-center justify-between px-6 py-4 border-t border-slate-200 dark:border-slate-700/50">
            <span class="text-sm text-slate-500">
              Showing {{ (page() - 1) * pageSize + 1 }}–{{ Math.min(page() * pageSize, filteredMessages().length) }}
              of {{ filteredMessages().length }}
            </span>
            <div class="flex gap-2">
              <button mat-stroked-button [disabled]="page() === 1" (click)="page.set(page() - 1)" class="!rounded-lg">
                <mat-icon>chevron_left</mat-icon>
              </button>
              <button mat-stroked-button [disabled]="page() * pageSize >= filteredMessages().length" (click)="page.set(page() + 1)" class="!rounded-lg">
                <mat-icon>chevron_right</mat-icon>
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class MessagesComponent implements OnInit {
  private api = inject(ApiService);
  protected Math = Math;

  messages = signal<Message[]>([]);
  filteredMessages = signal<Message[]>([]);
  loading = signal(true);
  page = signal(1);
  pageSize = 20;
  searchQuery = '';
  statusFilter = '';
  typeFilter = '';

  paginatedMessages = () => {
    const start = (this.page() - 1) * this.pageSize;
    return this.filteredMessages().slice(start, start + this.pageSize);
  };

  ngOnInit(): void {
    // Messages endpoint doesn't exist yet in the backend — we'll call the API logs as a proxy
    // or just show empty state. Let's try to load if it exists.
    this.loading.set(false);
    this.filteredMessages.set([]);
  }

  applyFilters(): void {
    const q = this.searchQuery.toLowerCase();
    this.filteredMessages.set(
      this.messages().filter((m) =>
        (q ? m.toNumber.toLowerCase().includes(q) : true) &&
        (this.statusFilter ? m.status === this.statusFilter : true) &&
        (this.typeFilter ? m.messageType === this.typeFilter : true)
      ),
    );
    this.page.set(1);
  }
}
