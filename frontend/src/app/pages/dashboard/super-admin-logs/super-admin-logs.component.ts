import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ButtonModule } from 'primeng/button';
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

    <div class="p-4 sm:p-6 max-w-7xl mx-auto space-y-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">System API Logs</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400">Realtime visibility for super admins with company/user filters.</p>
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
        <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
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
        </div>
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
                  <th class="px-3 py-2 text-start font-semibold">Time (UTC)</th>
                  <th class="px-3 py-2 text-start font-semibold">Status</th>
                  <th class="px-3 py-2 text-start font-semibold">Method</th>
                  <th class="px-3 py-2 text-start font-semibold">Endpoint</th>
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
                  <tr class="border-t border-slate-200 dark:border-slate-700">
                    <td class="px-3 py-2 whitespace-nowrap text-slate-600 dark:text-slate-300">{{ log.createdAtUtc | date: 'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td class="px-3 py-2">
                      <p-tag [severity]="statusSeverity(log.statusCode)" [value]="log.statusCode.toString()" [rounded]="true"></p-tag>
                    </td>
                    <td class="px-3 py-2 font-semibold text-slate-700 dark:text-slate-200">{{ log.httpMethod }}</td>
                    <td class="px-3 py-2">
                      <span class="font-mono text-xs text-slate-700 dark:text-slate-200">{{ log.endpoint }}</span>
                    </td>
                    <td class="px-3 py-2 text-slate-600 dark:text-slate-300">
                      {{ log.companyName || ('#' + (log.companyId ?? '-')) }}
                    </td>
                    <td class="px-3 py-2 text-slate-600 dark:text-slate-300">
                      {{ log.companyUserName || log.companyUserEmail || ('#' + (log.companyUserId ?? '-')) }}
                    </td>
                    <td class="px-3 py-2 text-slate-500 dark:text-slate-400">{{ log.ipAddress || '-' }}</td>
                    <td class="px-3 py-2 max-w-[260px]">
                      <pre class="m-0 whitespace-pre-wrap break-words text-xs text-slate-600 dark:text-slate-300">{{ log.requestPreview || '-' }}</pre>
                    </td>
                    <td class="px-3 py-2 max-w-[260px]">
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
  readonly logs = signal<SuperAdminApiLogListItem[]>([]);
  readonly loading = signal(false);
  readonly autoRefresh = signal(true);
  readonly selectedCompanyId = signal<number | null>(null);
  readonly selectedCompanyUserId = signal<number | null>(null);
  readonly detailsLoading = signal(false);
  readonly details = signal<SuperAdminApiLogDetails | null>(null);

  detailsVisible = false;

  private latestId = 0;
  private readonly maxRows = 500;
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private pollInFlight = false;

  ngOnInit(): void {
    this.loadCompanies();
    this.reloadLogs();
    this.startPolling();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  onCompanyChanged(rawValue: string): void {
    const companyId = this.toNullableNumber(rawValue);
    this.selectedCompanyId.set(companyId);
    this.selectedCompanyUserId.set(null);
    this.companyUsers.set([]);

    if (companyId) {
      this.loadCompanyUsers(companyId);
    }

    this.reloadLogs();
  }

  onCompanyUserChanged(rawValue: string): void {
    this.selectedCompanyUserId.set(this.toNullableNumber(rawValue));
    this.reloadLogs();
  }

  onAutoRefreshChanged(enabled: boolean): void {
    this.autoRefresh.set(!!enabled);
  }

  reloadLogs(): void {
    this.latestId = 0;
    this.logs.set([]);
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

  statusSeverity(statusCode: number): 'success' | 'info' | 'warn' | 'danger' {
    if (statusCode >= 500) return 'danger';
    if (statusCode >= 400) return 'warn';
    if (statusCode >= 300) return 'info';
    return 'success';
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

  private fetchLogs(isIncremental: boolean): void {
    if (this.pollInFlight) {
      return;
    }

    this.pollInFlight = true;
    if (!isIncremental) {
      this.loading.set(true);
    }

    this.superAdminService.getApiLogs({
      take: isIncremental ? 120 : 200,
      afterId: isIncremental ? this.latestId : undefined,
      companyId: this.selectedCompanyId(),
      companyUserId: this.selectedCompanyUserId(),
    }).subscribe({
      next: (feed) => {
        this.mergeLogs(feed?.items ?? []);
        if (feed?.latestId && feed.latestId > this.latestId) {
          this.latestId = feed.latestId;
        }
        this.loading.set(false);
        this.pollInFlight = false;
      },
      error: () => {
        this.loading.set(false);
        this.pollInFlight = false;
        if (!isIncremental) {
          this.messageService.add({ severity: 'error', summary: 'Failed to load logs.', life: 3500 });
        }
      },
    });
  }

  private mergeLogs(incoming: SuperAdminApiLogListItem[]): void {
    if (!incoming.length) {
      return;
    }

    const map = new Map<number, SuperAdminApiLogListItem>();
    for (const item of this.logs()) {
      map.set(item.apiLogId, item);
    }

    for (const item of incoming) {
      map.set(item.apiLogId, item);
      if (item.apiLogId > this.latestId) {
        this.latestId = item.apiLogId;
      }
    }

    const merged = Array.from(map.values())
      .sort((a, b) => b.apiLogId - a.apiLogId)
      .slice(0, this.maxRows);

    this.logs.set(merged);
  }

  private toNullableNumber(rawValue: string): number | null {
    const value = Number(rawValue);
    return Number.isFinite(value) && value > 0 ? value : null;
  }
}
