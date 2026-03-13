import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { SuperAdminService } from '../../../core/services/super-admin.service';
import {
  SuperAdminApiLogDetails,
  SuperAdminApiLogListItem,
  SuperAdminCompany,
  SuperAdminCompanyUserOption,
} from '../../../core/models';

@Component({
  selector: 'app-super-admin-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ProgressSpinnerModule, ToastModule, TagModule, ButtonModule],
  providers: [MessageService],
  template: `
    <p-toast />

    <div class="w-full p-4 sm:p-6 space-y-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">System API Logs</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400">Realtime visibility for super admins with company/user/category filters.</p>
        </div>
        <div class="flex items-center gap-2">
          <label class="text-sm text-slate-600 dark:text-slate-300 inline-flex items-center gap-2">
            <input
              type="checkbox"
              [ngModel]="autoRefresh()"
              (ngModelChange)="onAutoRefreshChanged($event)"
              class="rounded border-slate-300"
            />
            Auto refresh (2s)
          </label>
          <button pButton type="button" label="Reload" icon="pi pi-refresh" [loading]="loading()" (click)="reloadLogs()"></button>
        </div>
      </div>

      <div class="bg-white dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700/60 rounded-2xl p-3 sm:p-4">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-3">
          <div>
            <label class="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">Company</label>
            <select
              class="mt-1 w-full rounded-xl border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-900 px-3 py-2 text-sm text-slate-700 dark:text-slate-200"
              [ngModel]="selectedCompanyId() ?? ''"
              (ngModelChange)="onCompanyChanged($event)"
            >
              <option value="">All companies</option>
              @for (company of companies(); track company.companyId) {
                <option [value]="company.companyId">{{ company.companyName }} (#{{ company.companyId }})</option>
              }
            </select>
          </div>

          <div>
            <label class="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">User</label>
            <select
              class="mt-1 w-full rounded-xl border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-900 px-3 py-2 text-sm text-slate-700 dark:text-slate-200 disabled:opacity-60"
              [ngModel]="selectedCompanyUserId() ?? ''"
              (ngModelChange)="onCompanyUserChanged($event)"
              [disabled]="!selectedCompanyId()"
            >
              <option value="">All users</option>
              @for (user of companyUsers(); track user.companyUserId) {
                <option [value]="user.companyUserId">
                  {{ user.fullName }} ({{ user.email }})
                </option>
              }
            </select>
          </div>

          <div>
            <label class="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">Category</label>
            <select
              class="mt-1 w-full rounded-xl border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-900 px-3 py-2 text-sm text-slate-700 dark:text-slate-200"
              [ngModel]="selectedCategory() ?? ''"
              (ngModelChange)="onCategoryChanged($event)"
            >
              <option value="">All categories</option>
              @for (category of categories(); track category) {
                <option [value]="category">{{ formatCategory(category) }}</option>
              }
            </select>
          </div>

          <div>
            <label class="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">Result</label>
            <select
              class="mt-1 w-full rounded-xl border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-900 px-3 py-2 text-sm text-slate-700 dark:text-slate-200"
              [ngModel]="selectedResult()"
              (ngModelChange)="onResultChanged($event)">
              <option value="">All results</option>
              <option value="success">Success (2xx)</option>
              <option value="non_success">Non-success (3xx/4xx/5xx)</option>
            </select>
          </div>
        </div>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <span class="inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs font-semibold bg-emerald-50 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300">
          Success: {{ successRowsCount() }}
        </span>
        <span class="inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs font-semibold bg-rose-50 text-rose-700 dark:bg-rose-900/30 dark:text-rose-300">
          Non-success: {{ nonSuccessRowsCount() }}
        </span>
      </div>

      <div class="bg-white dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700/60 rounded-2xl overflow-hidden">
        @if (loading() && logs().length === 0) {
          <div class="py-16 flex justify-center">
            <p-progressSpinner [style]="{ width: '40px', height: '40px' }" strokeWidth="4"></p-progressSpinner>
          </div>
        } @else if (logs().length === 0) {
          <div class="py-16 text-center text-slate-500 dark:text-slate-400">No logs found for current filters.</div>
        } @else {
          <div class="overflow-x-auto">
            <table class="min-w-full text-sm">
              <thead class="bg-slate-50 dark:bg-slate-900/60 text-slate-600 dark:text-slate-300">
                <tr>
                  <th class="px-3 py-2 text-start font-semibold whitespace-nowrap">Time (UTC)</th>
                  <th class="px-3 py-2 text-start font-semibold">Status</th>
                  <th class="px-3 py-2 text-start font-semibold">Method</th>
                  <th class="px-3 py-2 text-start font-semibold">Endpoint</th>
                  <th class="px-3 py-2 text-start font-semibold">Category</th>
                  <th class="px-3 py-2 text-start font-semibold">Company</th>
                  <th class="px-3 py-2 text-start font-semibold">User</th>
                  <th class="px-3 py-2 text-start font-semibold">IP</th>
                  <th class="px-3 py-2 text-start font-semibold">Request</th>
                  <th class="px-3 py-2 text-start font-semibold">Response</th>
                  <th class="px-3 py-2 text-center font-semibold">Details</th>
                </tr>
              </thead>
              <tbody>
                @for (log of logs(); track log.apiLogId) {
                  <tr
                    class="border-t border-slate-200 dark:border-slate-700 transition-colors"
                    [ngClass]="rowVisualClass(log.statusCode)">
                    <td class="px-3 py-2 whitespace-nowrap text-slate-600 dark:text-slate-300">{{ log.createdAtUtc | date: 'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td class="px-3 py-2">
                      <div class="flex items-center gap-2">
                        <p-tag [severity]="statusSeverity(log.statusCode)" [value]="log.statusCode.toString()" [rounded]="true"></p-tag>
                        <span class="text-[11px] font-semibold uppercase tracking-wide" [ngClass]="statusLabelClass(log.statusCode)">
                          {{ statusLabel(log.statusCode) }}
                        </span>
                      </div>
                    </td>
                    <td class="px-3 py-2 font-semibold text-slate-700 dark:text-slate-200">{{ log.httpMethod }}</td>
                    <td class="px-3 py-2 max-w-[300px]">
                      <span class="font-mono text-xs text-slate-700 dark:text-slate-200 break-words">{{ log.endpoint }}</span>
                    </td>
                    <td class="px-3 py-2">
                      <span class="inline-flex rounded-full px-2 py-1 text-[11px] font-semibold bg-slate-100 dark:bg-slate-700 text-slate-700 dark:text-slate-200">
                        {{ formatCategory(log.category) }}
                      </span>
                    </td>
                    <td class="px-3 py-2 text-slate-600 dark:text-slate-300">
                      {{ log.companyName || ('#' + (log.companyId ?? '-')) }}
                    </td>
                    <td class="px-3 py-2 text-slate-600 dark:text-slate-300">
                      {{ log.companyUserName || log.companyUserEmail || ('#' + (log.companyUserId ?? '-')) }}
                    </td>
                    <td class="px-3 py-2 text-slate-500 dark:text-slate-400">{{ log.ipAddress || '-' }}</td>
                    <td class="px-3 py-2 max-w-[280px]">
                      <pre class="m-0 whitespace-pre-wrap break-words text-xs text-slate-600 dark:text-slate-300">{{ log.requestPreview || '-' }}</pre>
                    </td>
                    <td class="px-3 py-2 max-w-[280px]">
                      <pre class="m-0 whitespace-pre-wrap break-words text-xs text-slate-600 dark:text-slate-300">{{ log.responsePreview || '-' }}</pre>
                    </td>
                    <td class="px-3 py-2 text-center">
                      <button pButton type="button" icon="pi pi-eye" [text]="true" [rounded]="true" size="small" (click)="openDetails(log.apiLogId)"></button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <div class="border-t border-slate-200 dark:border-slate-700 px-3 py-3 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div class="text-sm text-slate-600 dark:text-slate-300">
            Showing {{ rangeStart() }}-{{ rangeEnd() }} of {{ totalCount() }} logs
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <label class="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">Rows</label>
            <select
              class="rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-900 px-2 py-1 text-sm text-slate-700 dark:text-slate-200"
              [ngModel]="pageSize()"
              (ngModelChange)="onPageSizeChanged($event)"
            >
              @for (size of pageSizeOptions; track size) {
                <option [value]="size">{{ size }}</option>
              }
            </select>

            <button pButton type="button" icon="pi pi-chevron-left" [text]="true" [disabled]="page() <= 1 || loading()" (click)="goToPreviousPage()"></button>
            <span class="text-sm text-slate-700 dark:text-slate-200">Page {{ page() }} / {{ totalPages() || 1 }}</span>
            <button pButton type="button" icon="pi pi-chevron-right" [text]="true" [disabled]="!hasNextPage() || loading()" (click)="goToNextPage()"></button>
          </div>
        </div>
      </div>
    </div>

    <p-dialog
      [(visible)]="detailsVisible"
      [modal]="true"
      [draggable]="false"
      [style]="{ width: '90vw', maxWidth: '1100px' }"
      header="API Log Details"
    >
      @if (detailsLoading()) {
        <div class="py-12 flex justify-center">
          <p-progressSpinner [style]="{ width: '32px', height: '32px' }" strokeWidth="4"></p-progressSpinner>
        </div>
      } @else if (details()) {
        <div class="space-y-3">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
            <div class="rounded-xl bg-slate-50 dark:bg-slate-900/50 p-3">
              <div><strong>ID:</strong> {{ details()!.apiLogId }}</div>
              <div><strong>Time:</strong> {{ details()!.createdAtUtc | date: 'yyyy-MM-dd HH:mm:ss' }}</div>
              <div><strong>Status:</strong> {{ details()!.statusCode }}</div>
              <div><strong>Method:</strong> {{ details()!.httpMethod }}</div>
              <div><strong>Endpoint:</strong> {{ details()!.endpoint }}</div>
            </div>
            <div class="rounded-xl bg-slate-50 dark:bg-slate-900/50 p-3">
              <div><strong>Company:</strong> {{ details()!.companyName || details()!.companyId || '-' }}</div>
              <div><strong>User:</strong> {{ details()!.companyUserName || details()!.companyUserEmail || details()!.companyUserId || '-' }}</div>
              <div><strong>IP:</strong> {{ details()!.ipAddress || '-' }}</div>
            </div>
          </div>

          <div>
            <h4 class="font-semibold text-slate-800 dark:text-slate-100 mb-1">Request Body</h4>
            <pre class="rounded-xl bg-slate-50 dark:bg-slate-900/50 p-3 text-xs whitespace-pre-wrap break-words max-h-[280px] overflow-auto">{{ details()!.requestBody || '-' }}</pre>
          </div>
          <div>
            <h4 class="font-semibold text-slate-800 dark:text-slate-100 mb-1">Response Body</h4>
            <pre class="rounded-xl bg-slate-50 dark:bg-slate-900/50 p-3 text-xs whitespace-pre-wrap break-words max-h-[280px] overflow-auto">{{ details()!.responseBody || '-' }}</pre>
          </div>
        </div>
      }
    </p-dialog>
  `,
})
export class SuperAdminLogsComponent implements OnInit, OnDestroy {
  private readonly superAdminService = inject(SuperAdminService);
  private readonly messageService = inject(MessageService);

  readonly companies = signal<SuperAdminCompany[]>([]);
  readonly companyUsers = signal<SuperAdminCompanyUserOption[]>([]);
  readonly categories = signal<string[]>([]);
  readonly logs = signal<SuperAdminApiLogListItem[]>([]);
  readonly loading = signal(false);
  readonly autoRefresh = signal(true);
  readonly selectedCompanyId = signal<number | null>(null);
  readonly selectedCompanyUserId = signal<number | null>(null);
  readonly selectedCategory = signal<string | null>(null);
  readonly selectedResult = signal<'success' | 'non_success' | ''>('');
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly detailsLoading = signal(false);
  readonly details = signal<SuperAdminApiLogDetails | null>(null);

  readonly pageSizeOptions = [25, 50, 100, 150] as const;
  readonly hasNextPage = computed(() => this.totalPages() > 0 && this.page() < this.totalPages());
  readonly rangeStart = computed(() => this.totalCount() > 0 ? ((this.page() - 1) * this.pageSize()) + 1 : 0);
  readonly rangeEnd = computed(() => this.totalCount() > 0 ? Math.min(this.page() * this.pageSize(), this.totalCount()) : 0);
  readonly successRowsCount = computed(() => this.logs().filter((item) => this.isSuccessStatus(item.statusCode)).length);
  readonly nonSuccessRowsCount = computed(() => this.logs().length - this.successRowsCount());

  detailsVisible = false;

  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private pollInFlight = false;

  ngOnInit(): void {
    this.loadCompanies();
    this.loadCategories();
    this.fetchLogs(false);
    this.startPolling();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  onCompanyChanged(rawValue: string | number): void {
    const companyId = this.toNullableNumber(rawValue);
    this.selectedCompanyId.set(companyId);
    this.selectedCompanyUserId.set(null);
    this.selectedCategory.set(null);
    this.companyUsers.set([]);

    if (companyId) {
      this.loadCompanyUsers(companyId);
    }

    this.page.set(1);
    this.loadCategories();
    this.fetchLogs(false);
  }

  onCompanyUserChanged(rawValue: string | number): void {
    this.selectedCompanyUserId.set(this.toNullableNumber(rawValue));
    this.selectedCategory.set(null);
    this.page.set(1);
    this.loadCategories();
    this.fetchLogs(false);
  }

  onCategoryChanged(rawValue: string): void {
    this.selectedCategory.set(this.toNullableCategory(rawValue));
    this.page.set(1);
    this.fetchLogs(false);
  }

  onResultChanged(rawValue: string): void {
    const next = (rawValue ?? '').trim().toLowerCase();
    if (next === 'success' || next === 'non_success') {
      this.selectedResult.set(next);
    } else {
      this.selectedResult.set('');
    }

    this.selectedCategory.set(null);
    this.page.set(1);
    this.loadCategories();
    this.fetchLogs(false);
  }

  onPageSizeChanged(rawValue: string | number): void {
    const parsed = Number(rawValue);
    const normalized = Number.isFinite(parsed) && parsed > 0 ? parsed : 25;
    if (normalized === this.pageSize()) {
      return;
    }

    this.pageSize.set(normalized);
    this.page.set(1);
    this.fetchLogs(false);
  }

  onAutoRefreshChanged(enabled: boolean): void {
    this.autoRefresh.set(!!enabled);
  }

  reloadLogs(): void {
    this.page.set(1);
    this.fetchLogs(false);
  }

  goToPreviousPage(): void {
    if (this.page() <= 1) {
      return;
    }

    this.page.set(this.page() - 1);
    this.fetchLogs(false);
  }

  goToNextPage(): void {
    if (!this.hasNextPage()) {
      return;
    }

    this.page.set(this.page() + 1);
    this.fetchLogs(false);
  }

  openDetails(apiLogId: number): void {
    this.detailsVisible = true;
    this.detailsLoading.set(true);
    this.details.set(null);

    this.superAdminService.getApiLogDetails(apiLogId).subscribe({
      next: (log) => {
        this.details.set(log);
        this.detailsLoading.set(false);
      },
      error: () => {
        this.detailsLoading.set(false);
        this.messageService.add({ severity: 'error', summary: 'Failed to load log details.', life: 3500 });
      },
    });
  }

  formatCategory(category: string | null | undefined): string {
    const normalized = (category ?? '').trim().toLowerCase();
    if (!normalized) {
      return 'Other';
    }

    return normalized
      .split(/[-_]+/g)
      .filter((token) => !!token)
      .map((token) => token.charAt(0).toUpperCase() + token.slice(1))
      .join(' ');
  }

  statusSeverity(statusCode: number): 'success' | 'info' | 'warn' | 'danger' {
    if (statusCode >= 500) return 'danger';
    if (statusCode >= 400) return 'warn';
    if (statusCode >= 300) return 'info';
    return 'success';
  }

  statusLabel(statusCode: number): string {
    return this.isSuccessStatus(statusCode) ? 'Success' : 'Issue';
  }

  statusLabelClass(statusCode: number): string {
    return this.isSuccessStatus(statusCode)
      ? 'text-emerald-700 dark:text-emerald-300'
      : 'text-rose-700 dark:text-rose-300';
  }

  rowVisualClass(statusCode: number): string {
    return this.isSuccessStatus(statusCode)
      ? 'bg-white/70 dark:bg-transparent'
      : 'bg-rose-50/70 dark:bg-rose-900/10';
  }

  private startPolling(): void {
    this.stopPolling();
    this.pollTimer = setInterval(() => {
      if (!this.autoRefresh()) return;
      this.fetchLogs(true);
    }, 2000);
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private loadCompanies(): void {
    this.superAdminService.getCompanies().subscribe({
      next: (companies) => this.companies.set(companies ?? []),
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Failed to load companies.', life: 3000 });
      },
    });
  }

  private loadCompanyUsers(companyId: number): void {
    this.superAdminService.getCompanyUsersForLogs(companyId).subscribe({
      next: (users) => this.companyUsers.set((users ?? []).filter((u) => u.isActive)),
      error: () => {
        this.companyUsers.set([]);
        this.messageService.add({ severity: 'error', summary: 'Failed to load users for selected company.', life: 3000 });
      },
    });
  }

  private loadCategories(): void {
    this.superAdminService.getApiLogCategories({
      companyId: this.selectedCompanyId(),
      companyUserId: this.selectedCompanyUserId(),
      result: this.selectedResult(),
    }).subscribe({
      next: (payload) => {
        const normalized = Array.from(new Set((payload?.items ?? [])
          .map((item) => item.trim().toLowerCase())
          .filter((item) => !!item)));

        this.categories.set(normalized);
        if (this.selectedCategory() && !normalized.includes(this.selectedCategory()!)) {
          this.selectedCategory.set(null);
        }
      },
      error: () => {
        this.categories.set([]);
        this.selectedCategory.set(null);
      },
    });
  }

  private fetchLogs(silent: boolean): void {
    if (this.pollInFlight) {
      return;
    }

    this.pollInFlight = true;
    if (!silent) {
      this.loading.set(true);
    }

    this.superAdminService.getApiLogs({
      page: this.page(),
      pageSize: this.pageSize(),
      companyId: this.selectedCompanyId(),
      companyUserId: this.selectedCompanyUserId(),
      category: this.selectedCategory(),
      result: this.selectedResult(),
    }).subscribe({
      next: (feed) => {
        this.logs.set(feed?.items ?? []);
        this.totalCount.set(feed?.totalCount ?? 0);
        this.totalPages.set(feed?.totalPages ?? 0);

        if (feed?.page && feed.page > 0) {
          this.page.set(feed.page);
        }

        if (feed?.pageSize && feed.pageSize > 0) {
          this.pageSize.set(feed.pageSize);
        }

        this.loading.set(false);
        this.pollInFlight = false;
      },
      error: () => {
        this.loading.set(false);
        this.pollInFlight = false;
        if (!silent) {
          this.messageService.add({ severity: 'error', summary: 'Failed to load logs.', life: 3500 });
        }
      },
    });
  }

  private toNullableCategory(rawValue: string | null | undefined): string | null {
    const value = (rawValue ?? '').trim().toLowerCase();
    return value.length > 0 ? value : null;
  }

  private isSuccessStatus(statusCode: number): boolean {
    return statusCode >= 200 && statusCode < 300;
  }

  private toNullableNumber(rawValue: string | number | null | undefined): number | null {
    const value = Number(rawValue);
    return Number.isFinite(value) && value > 0 ? value : null;
  }
}
