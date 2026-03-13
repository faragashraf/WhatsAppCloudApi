import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { TooltipModule } from 'primeng/tooltip';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { NavbarComponent } from '../navbar/navbar.component';
import { LanguageService, SidebarService, ApiService, NotificationManagerService } from '../../core/services';

@Component({
  selector: 'app-dashboard-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, CommonModule, TranslateModule, TooltipModule, SidebarComponent, NavbarComponent],
  template: `
    <app-navbar />
    <div class="flex min-h-[calc(100vh-64px)] pt-16">
      <app-sidebar />
      <main
        class="flex-1 p-3 sm:p-4 md:p-5 lg:p-5 xl:p-8 bg-transparent overflow-y-auto overflow-x-hidden transition-all duration-300 relative"
        [style.margin-inline-start]="sidebarService.contentOffset"
        [style.padding-inline-end]="sidebarService.isMobile() ? '0.9rem' : '1.4rem'">

        <div class="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(circle_at_20%_8%,rgba(16,168,97,0.08),transparent_28%),radial-gradient(circle_at_84%_12%,rgba(13,139,202,0.08),transparent_24%)]"></div>

        <!-- Floating Notification Bell -->
        <a
          routerLink="/dashboard/notifications"
          class="fixed top-20 z-40 w-10 h-10 sm:w-11 sm:h-11 bg-[var(--app-surface)] dark:bg-slate-800 rounded-full shadow-[0_16px_28px_-18px_rgba(13,37,63,0.7)] border border-[var(--app-border)] dark:border-slate-700 flex items-center justify-center hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] transition-all no-underline group"
          [style.inset-inline-end]="sidebarService.isMobile() ? '1rem' : '1.5rem'"
          [pTooltip]="'sidebar.notifications' | translate">
          <i
            class="pi pi-bell text-[20px] text-[var(--app-text-soft)] dark:text-slate-300 group-hover:text-[var(--app-primary-strong)] dark:group-hover:text-[var(--app-primary)] transition-colors"
            [class.animate-bell-ring]="notifService.unreadCount() > 0"></i>
          @if (notifService.unreadCount() > 0) {
            <span class="absolute -top-1 -end-1 bg-[var(--app-danger)] text-white rounded-full text-[9px] min-w-[18px] h-[18px] px-1 flex items-center justify-center font-bold shadow-sm animate-bounce-in">
              {{ notifService.unreadCount() > 99 ? '99+' : notifService.unreadCount() }}
            </span>
          }
        </a>
        @if (notifService.inAppNotification(); as inApp) {
          <div
            class="fixed top-20 z-40 w-[min(88vw,22rem)] cursor-pointer rounded-2xl border border-[var(--app-border)] dark:border-slate-700 bg-[var(--app-surface)]/95 dark:bg-slate-900/95 backdrop-blur-sm shadow-[0_24px_45px_-30px_rgba(13,37,63,0.7)]"
            [style.inset-inline-end]="sidebarService.isMobile() ? '4rem' : '4.9rem'"
            (click)="openInAppNotification()">
            <div class="flex items-start gap-3 p-3">
              <div class="mt-0.5 h-8 w-8 shrink-0 rounded-xl bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300 flex items-center justify-center">
                <i class="pi pi-bell !text-[14px]"></i>
              </div>
              <div class="min-w-0 flex-1">
                <p class="text-sm font-semibold text-[var(--app-text)] dark:text-white truncate">{{ inApp.title }}</p>
                <p class="mt-1 text-xs leading-5 text-[var(--app-text-soft)] dark:text-slate-300 break-words">{{ inApp.body }}</p>
              </div>
              <button
                type="button"
                class="mt-0.5 h-6 w-6 shrink-0 rounded-full border-none bg-transparent text-[var(--app-text-soft)] dark:text-slate-400 hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-800 cursor-pointer"
                aria-label="Dismiss notification"
                (click)="dismissInAppNotification($event)">
                <i class="pi pi-times !text-[11px]"></i>
              </button>
            </div>
          </div>
        }

        <div class="app-page-shell app-page-shell--wide w-full min-w-0">
          <router-outlet />
        </div>
      </main>
    </div>
  `,
  styles: [`
    @keyframes bell-ring {
      0%, 100% { transform: rotate(0deg); }
      10% { transform: rotate(14deg); }
      20% { transform: rotate(-14deg); }
      30% { transform: rotate(10deg); }
      40% { transform: rotate(-8deg); }
      50% { transform: rotate(4deg); }
      60% { transform: rotate(0deg); }
    }

    .animate-bell-ring {
      animation: bell-ring 1.5s ease-in-out infinite;
      animation-delay: 2s;
    }

    @keyframes bounce-in {
      0% { transform: scale(0); }
      50% { transform: scale(1.2); }
      100% { transform: scale(1); }
    }

    .animate-bounce-in {
      animation: bounce-in 0.3s ease-out;
    }
  `],
})
export class DashboardLayoutComponent implements OnInit, OnDestroy {
  readonly langService = inject(LanguageService);
  readonly sidebarService = inject(SidebarService);
  readonly notifService = inject(NotificationManagerService);
  private readonly api = inject(ApiService);

  ngOnInit(): void {
    this.notifService.startUnreadPolling(this.api);
  }

  ngOnDestroy(): void {
    this.notifService.stopUnreadPolling();
  }

  openInAppNotification(): void {
    this.notifService.activateInAppNotification();
  }

  dismissInAppNotification(event: Event): void {
    event.stopPropagation();
    this.notifService.dismissInAppNotification();
  }
}
