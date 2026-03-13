import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { EMPTY, Subject, forkJoin, merge, of, timer } from 'rxjs';
import { catchError, finalize, map, switchMap } from 'rxjs/operators';
import { ApiService, NotificationManagerService } from '../../../core/services';
import { Notification, PagedResult } from '../../../core/models';

type NotificationFilter = 'all' | 'unread' | 'read';
type RefreshReason = 'initial' | 'poll' | 'manual';

const AUTO_REFRESH_MS = 10000;

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, ProgressSpinnerModule, ToastModule, ConfirmDialogModule, TranslateModule],
  providers: [MessageService, ConfirmationService],
  template: `
    <p-toast />
    <p-confirmDialog />

    <div class="mx-auto w-full max-w-6xl space-y-4 sm:space-y-5 lg:space-y-6 min-w-0 app-wrap-safe">
      <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-start">
        <div class="space-y-1">
          <h1 class="text-xl font-bold text-slate-900 sm:text-2xl lg:text-3xl dark:text-white">{{ 'notifications.title' | translate }}</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400">
            {{ unreadCount() }} {{ 'notifications.unread' | translate }}
          </p>
          <div class="mt-1.5 inline-flex items-center gap-2 rounded-full bg-slate-100 px-3 py-1 text-xs text-slate-500 dark:bg-slate-800 dark:text-slate-300">
            <i class="pi pi-spin pi-refresh text-[10px]" *ngIf="refreshing()"></i>
            <i class="pi pi-clock text-[10px]" *ngIf="!refreshing()"></i>
            {{ 'notifications.autoRefresh' | translate }}
          </div>
        </div>

        <div class="flex flex-wrap items-center gap-2 sm:gap-3 lg:justify-end">
          <button
            (click)="markAllAsRead()"
            [disabled]="loading() || unreadCount() === 0"
            class="flex w-full items-center justify-center gap-2 rounded-xl bg-slate-100 px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-200 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto dark:bg-slate-700 dark:text-slate-200 dark:hover:bg-slate-600">
            <i class="pi pi-check !text-[16px]"></i>
            {{ 'notifications.markAllRead' | translate }}
          </button>

          <button
            (click)="confirmDeleteAll()"
            [disabled]="loading() || totalCount() === 0"
            class="flex w-full items-center justify-center gap-2 rounded-xl bg-red-50 px-4 py-2 text-sm font-medium text-red-600 transition-colors hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto dark:bg-red-950/20 dark:text-red-300 dark:hover:bg-red-950/40">
            <i class="pi pi-trash !text-[16px]"></i>
            {{ 'notifications.deleteAll' | translate }}
          </button>
        </div>
      </div>

      <div class="overflow-x-auto pb-1">
        <div class="flex min-w-max items-center gap-2">
          @for (filter of filters; track filter.value) {
            <button
              (click)="setFilter(filter.value)"
              class="whitespace-nowrap rounded-full border px-4 py-2 text-sm font-medium transition-colors"
              [class.bg-slate-900]="filterStatus() === filter.value"
              [class.border-slate-900]="filterStatus() === filter.value"
              [class.text-white]="filterStatus() === filter.value"
              [class.dark:bg-white]="filterStatus() === filter.value"
              [class.dark:border-white]="filterStatus() === filter.value"
              [class.dark:text-slate-900]="filterStatus() === filter.value"
              [class.bg-white]="filterStatus() !== filter.value"
              [class.border-slate-200]="filterStatus() !== filter.value"
              [class.text-slate-600]="filterStatus() !== filter.value"
              [class.hover:bg-slate-50]="filterStatus() !== filter.value"
              [class.dark:bg-slate-800/60]="filterStatus() !== filter.value"
              [class.dark:border-slate-700]="filterStatus() !== filter.value"
              [class.dark:text-slate-300]="filterStatus() !== filter.value"
              [class.dark:hover:bg-slate-800]="filterStatus() !== filter.value">
              {{ filter.labelKey | translate }}
            </button>
          }
        </div>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-12">
          <p-progressSpinner [style]="{'width':'32px','height':'32px'}" strokeWidth="4" />
        </div>
      } @else if (notifications().length === 0) {
        <div class="rounded-2xl border border-slate-200 bg-white py-12 text-center text-slate-400 dark:border-slate-700/50 dark:bg-slate-800/50">
          <i class="pi pi-bell mb-2 text-[42px] sm:text-[48px]"></i>
          <p>{{ 'notifications.noNotifications' | translate }}</p>
        </div>
      } @else {
        <div class="space-y-2.5 sm:space-y-3">
          @for (n of notifications(); track n.notificationId) {
            <div
              class="flex items-start gap-3 rounded-2xl border border-slate-200 bg-white p-3 transition-colors sm:gap-4 sm:p-4 lg:p-5 dark:border-slate-700/50 dark:bg-slate-800/50"
              [class.border-s-4]="!n.isRead"
              [class.border-s-emerald-500]="!n.isRead && n.type === 'success'"
              [class.border-s-blue-500]="!n.isRead && n.type === 'info'"
              [class.border-s-amber-500]="!n.isRead && n.type === 'warning'"
              [class.border-s-red-500]="!n.isRead && n.type === 'error'">
              <div class="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl sm:h-10 sm:w-10" [class]="getIconBg(n.type)">
                <i class="pi !text-[20px]" [class]="getIconColor(n.type)" [ngClass]="getIcon(n.type)"></i>
              </div>

              <div class="min-w-0 flex-1">
                <div class="grid grid-cols-[minmax(0,1fr)_auto] items-start gap-2 sm:gap-3">
                  <div class="min-w-0">
                    <h3 class="break-words text-sm font-semibold text-slate-900 dark:text-white">{{ n.title }}</h3>
                    <p class="mt-0.5 break-words text-sm text-slate-600 dark:text-slate-400">{{ n.body }}</p>
                  </div>

                  <div class="flex shrink-0 items-center gap-1 self-start">
                    @if (!n.isRead) {
                      <button
                        (click)="markAsRead(n)"
                        class="rounded-lg p-1.5 transition-colors hover:bg-slate-100 dark:hover:bg-slate-700"
                        [title]="'notifications.markRead' | translate">
                        <i class="pi pi-check !text-[16px] text-slate-400"></i>
                      </button>
                    }

                    <button
                      (click)="deleteNotification(n)"
                      class="rounded-lg p-1.5 transition-colors hover:bg-red-50 dark:hover:bg-red-950/20"
                      [title]="'common.delete' | translate">
                      <i class="pi pi-times !text-[16px] text-slate-400 hover:text-red-500"></i>
                    </button>
                  </div>
                </div>

                <div class="mt-2 flex flex-wrap items-center gap-2 text-xs text-slate-400 sm:gap-3">
                  @if (n.category) {
                    <span class="rounded-full px-2 py-0.5" [class]="getCategoryClass(n.category)">
                      {{ n.category }}
                    </span>
                  }
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
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly notifService = inject(NotificationManagerService);
  private readonly messageService = inject(MessageService);
  private readonly confirmService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);
  private readonly refresh$ = new Subject<RefreshReason>();

  protected readonly filters: { value: NotificationFilter; labelKey: string }[] = [
    { value: 'all', labelKey: 'notifications.all' },
    { value: 'unread', labelKey: 'notifications.unread' },
    { value: 'read', labelKey: 'notifications.read' },
  ];

  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly notifications = signal<Notification[]>([]);
  readonly unreadCount = signal(0);
  readonly totalCount = signal(0);
  readonly filterStatus = signal<NotificationFilter>('all');

  ngOnInit(): void {
    merge(
      of<RefreshReason>('initial'),
      timer(AUTO_REFRESH_MS, AUTO_REFRESH_MS).pipe(map(() => 'poll' as RefreshReason)),
      this.refresh$
    ).pipe(
      switchMap((reason) =>
        this.fetchSnapshot(reason).pipe(
          catchError(() => {
            if (reason !== 'poll') {
              this.messageService.add({
                severity: 'error',
                summary: this.translate.instant('notifications.loadError'),
                life: 3000,
              });
            }
            return EMPTY;
          })
        )
      ),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(({ list, unreadCount }) => {
      this.notifications.set(list.items ?? []);
      this.totalCount.set(list.totalCount ?? 0);
      this.unreadCount.set(unreadCount ?? 0);
      this.notifService.syncUnreadCount(unreadCount ?? 0);
    });
  }

  setFilter(filter: NotificationFilter): void {
    if (this.filterStatus() === filter) return;
    this.filterStatus.set(filter);
    this.refresh$.next('manual');
  }

  markAsRead(n: Notification): void {
    if (n.isRead) return;

    this.api.post<boolean>(`/notifications/${n.notificationId}/read`).subscribe({
      next: () => this.refresh$.next('manual'),
      error: () => this.showActionError(),
    });
  }

  markAllAsRead(): void {
    if (this.unreadCount() === 0) return;

    this.api.post<boolean>('/notifications/read-all').subscribe({
      next: () => {
        this.unreadCount.set(0);
        this.notifService.syncUnreadCount(0);
        this.refresh$.next('manual');
      },
      error: () => this.showActionError(),
    });
  }

  deleteNotification(n: Notification): void {
    this.api.delete<boolean>(`/notifications/${n.notificationId}`).subscribe({
      next: () => this.refresh$.next('manual'),
      error: () => this.showActionError(),
    });
  }

  confirmDeleteAll(): void {
    if (this.totalCount() === 0) return;

    this.confirmService.confirm({
      message: this.translate.instant(this.getDeleteAllMessageKey()),
      header: this.translate.instant('notifications.deleteAllHeader'),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: '!bg-red-500 !text-white !rounded-xl !border-none',
      rejectButtonStyleClass: '!rounded-xl',
      accept: () => this.deleteAllForCurrentFilter(),
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
      inbox: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300',
      system: 'bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-300',
    }[cat] || 'bg-slate-100 text-slate-600';
  }

  private fetchSnapshot(reason: RefreshReason) {
    const showInitialLoader = reason === 'initial' && this.notifications().length === 0;

    if (showInitialLoader) {
      this.loading.set(true);
    } else {
      this.refreshing.set(true);
    }

    return forkJoin({
      list: this.api.get<PagedResult<Notification>>('/notifications', this.buildListParams()),
      unreadCount: this.api.get<number>('/notifications/unread-count'),
    }).pipe(
      finalize(() => {
        this.loading.set(false);
        this.refreshing.set(false);
      })
    );
  }

  private buildListParams(): Record<string, string | number | boolean> {
    const params: Record<string, string | number | boolean> = { pageSize: 50 };
    const isRead = this.currentIsReadFilter();

    if (isRead !== null) {
      params['isRead'] = isRead;
    }

    return params;
  }

  private currentIsReadFilter(): boolean | null {
    switch (this.filterStatus()) {
      case 'read':
        return true;
      case 'unread':
        return false;
      default:
        return null;
    }
  }

  private deleteAllForCurrentFilter(): void {
    const isRead = this.currentIsReadFilter();
    const params = isRead === null ? undefined : { isRead };

    this.api.delete<boolean>('/notifications/all', params).subscribe({
      next: () => {
        if (isRead !== true) {
          this.unreadCount.set(0);
          this.notifService.syncUnreadCount(0);
        }

        this.messageService.add({
          severity: 'success',
          summary: this.translate.instant('notifications.deleteAllSuccess'),
          life: 2500,
        });

        this.refresh$.next('manual');
      },
      error: () => this.showActionError(),
    });
  }

  private getDeleteAllMessageKey(): string {
    switch (this.filterStatus()) {
      case 'read':
        return 'notifications.confirmDeleteRead';
      case 'unread':
        return 'notifications.confirmDeleteUnread';
      default:
        return 'notifications.confirmDeleteAll';
    }
  }

  private showActionError(): void {
    this.messageService.add({
      severity: 'error',
      summary: this.translate.instant('notifications.actionError'),
      life: 3000,
    });
  }
}
