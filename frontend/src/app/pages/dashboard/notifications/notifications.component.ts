import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService } from '../../../core/services';
import { Notification, PagedResult } from '../../../core/models';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, ProgressSpinnerModule, TranslateModule],
  template: `
    <div class="space-y-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'notifications.title' | translate }}</h1>
          <p class="text-sm text-slate-500 mt-1">{{ unreadCount() }} {{ 'notifications.unread' | translate }}</p>
        </div>
        <button
          (click)="markAllAsRead()"
          class="flex items-center gap-2 px-4 py-2 bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 rounded-xl text-sm font-medium transition-colors text-slate-700 dark:text-slate-200">
          <i class="pi pi-check !text-[18px]"></i>
          {{ 'notifications.markAllRead' | translate }}
        </button>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-12"><p-progressSpinner [style]="{'width':'32px','height':'32px'}" strokeWidth="4" /></div>
      } @else if (notifications().length === 0) {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 text-center py-12 text-slate-400">
          <i class="pi pi-bell !text-[48px] mb-2"></i>
          <p>{{ 'notifications.noNotifications' | translate }}</p>
        </div>
      } @else {
        <div class="space-y-2">
          @for (n of notifications(); track n.notificationId) {
            <div
              class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-4 flex items-start gap-4 transition-colors"
              [class.border-s-4]="!n.isRead"
              [class.border-s-emerald-500]="!n.isRead && n.type === 'success'"
              [class.border-s-blue-500]="!n.isRead && n.type === 'info'"
              [class.border-s-amber-500]="!n.isRead && n.type === 'warning'"
              [class.border-s-red-500]="!n.isRead && n.type === 'error'">
              <div class="w-10 h-10 rounded-xl flex items-center justify-center shrink-0"
                [class]="getIconBg(n.type)">
                <i class="pi !text-[20px]" [class]="getIconColor(n.type)" [ngClass]="getIcon(n.type)"></i>
              </div>
              <div class="flex-1 min-w-0">
                <div class="flex items-start justify-between gap-2">
                  <div>
                    <h3 class="text-sm font-semibold text-slate-900 dark:text-white">{{ n.title }}</h3>
                    <p class="text-sm text-slate-600 dark:text-slate-400 mt-0.5">{{ n.body }}</p>
                  </div>
                  <div class="flex items-center gap-1 shrink-0">
                    @if (!n.isRead) {
                      <button (click)="markAsRead(n)" class="p-1.5 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors" [title]="'notifications.markRead' | translate">
                        <i class="pi pi-check !text-[16px] text-slate-400"></i>
                      </button>
                    }
                    <button (click)="deleteNotification(n)" class="p-1.5 rounded-lg hover:bg-red-50 dark:hover:bg-red-950/20 transition-colors" [title]="'common.delete' | translate">
                      <i class="pi pi-times !text-[16px] text-slate-400 hover:text-red-500"></i>
                    </button>
                  </div>
                </div>
                <div class="flex items-center gap-3 mt-2 text-xs text-slate-400">
                  <span class="px-2 py-0.5 rounded-full"
                    [class]="getCategoryClass(n.category ?? '')">{{ n.category }}</span>
                  <span>{{ n.createdAtUtc | date:'short' }}</span>
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class NotificationsComponent implements OnInit {
  private api = inject(ApiService);

  loading = signal(true);
  notifications = signal<Notification[]>([]);
  unreadCount = signal(0);

  ngOnInit(): void {
    this.loadNotifications();
    this.loadUnreadCount();
  }

  loadNotifications(): void {
    this.api.get<PagedResult<Notification>>('/notifications', { pageSize: '50' }).subscribe({
      next: (r) => { this.notifications.set(r?.items ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  loadUnreadCount(): void {
    this.api.get<number>('/notifications/unread-count').subscribe({
      next: (c) => this.unreadCount.set(c ?? 0),
    });
  }

  markAsRead(n: Notification): void {
    this.api.post(`/notifications/${n.notificationId}/read`).subscribe({
      next: () => { this.loadNotifications(); this.loadUnreadCount(); },
    });
  }

  markAllAsRead(): void {
    this.api.post('/notifications/read-all').subscribe({
      next: () => { this.loadNotifications(); this.loadUnreadCount(); },
    });
  }

  deleteNotification(n: Notification): void {
    this.api.delete(`/notifications/${n.notificationId}`).subscribe({
      next: () => { this.loadNotifications(); this.loadUnreadCount(); },
    });
  }

  getIcon(type: string): string {
    return { info: 'pi-info-circle', warning: 'pi-exclamation-triangle', error: 'pi-times-circle', success: 'pi-check-circle' }[type] || 'pi-info-circle';
  }

  getIconBg(type: string): string {
    return {
      info: 'bg-blue-100 dark:bg-blue-900/30',
      warning: 'bg-amber-100 dark:bg-amber-900/30',
      error: 'bg-red-100 dark:bg-red-900/30',
      success: 'bg-emerald-100 dark:bg-emerald-900/30',
    }[type] || 'bg-slate-100';
  }

  getIconColor(type: string): string {
    return {
      info: 'text-blue-600',
      warning: 'text-amber-600',
      error: 'text-red-600',
      success: 'text-emerald-600',
    }[type] || 'text-slate-400';
  }

  getCategoryClass(cat: string): string {
    return {
      token_expiry: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300',
      webhook_failure: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300',
      quality_drop: 'bg-orange-100 text-orange-700 dark:bg-orange-900/30 dark:text-orange-300',
      campaign: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300',
      system: 'bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-300',
    }[cat] || 'bg-slate-100 text-slate-600';
  }
}
