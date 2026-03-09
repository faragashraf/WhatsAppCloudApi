import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TooltipModule } from 'primeng/tooltip';
import { TranslateModule } from '@ngx-translate/core';
import { TokenService, LanguageService, SidebarService } from '../../core/services';

interface NavItem {
  icon: string;        // PrimeIcon class
  labelKey: string;
  route: string;
  badge?: number;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, TooltipModule, TranslateModule],
  template: `
    <aside
      class="fixed top-16 bottom-0 z-40 flex flex-col border-slate-200 dark:border-slate-700/50 bg-white dark:bg-slate-900 transition-all duration-300 ease-in-out"
      [class.border-r]="!langService.isRtl()"
      [class.border-l]="langService.isRtl()"
      [style.inset-inline-start]="'0'"
      [style.width]="sidebarService.width">

      <!-- Toggle -->
      <button
        (click)="sidebarService.toggle()"
        class="absolute top-6 w-6 h-6 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-full flex items-center justify-center cursor-pointer hover:bg-emerald-50 dark:hover:bg-emerald-950/30 transition-colors z-10 shadow-sm"
        [style.inset-inline-end]="'-0.75rem'">
        <i class="pi text-[14px] text-slate-500" [class]="getToggleIcon()"></i>
      </button>

      <!-- Navigation -->
      <nav class="flex-1 overflow-y-auto py-4 px-3 space-y-1">
        @for (item of navItems; track item.route) {
          <a
            [routerLink]="item.route"
            routerLinkActive="!bg-emerald-50 dark:!bg-emerald-950/40 !text-emerald-600 dark:!text-emerald-400"
            [routerLinkActiveOptions]="{exact: item.route === '/dashboard'}"
            [pTooltip]="sidebarService.expanded() ? '' : (item.labelKey | translate)"
            [tooltipPosition]="langService.isRtl() ? 'left' : 'right'"
            class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800/50 hover:text-slate-900 dark:hover:text-slate-200 transition-all no-underline">
            <i class="pi shrink-0 text-[20px]" [class]="item.icon"></i>
            @if (sidebarService.expanded()) {
              <span class="truncate">{{ item.labelKey | translate }}</span>
            }
          </a>
        }
      </nav>

      <!-- Settings section -->
      <div class="border-t border-slate-200 dark:border-slate-700/50 p-3">
        <a
          routerLink="/dashboard/settings"
          routerLinkActive="!bg-emerald-50 dark:!bg-emerald-950/40 !text-emerald-600"
          [pTooltip]="sidebarService.expanded() ? '' : ('sidebar.settings' | translate)"
          [tooltipPosition]="langService.isRtl() ? 'left' : 'right'"
          class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-all no-underline">
          <i class="pi pi-cog shrink-0 text-[20px]"></i>
          @if (sidebarService.expanded()) {
            <span class="truncate">{{ 'sidebar.settings' | translate }}</span>
          }
        </a>
      </div>
    </aside>
  `,
})
export class SidebarComponent {
  private readonly tokenService = inject(TokenService);
  readonly langService = inject(LanguageService);
  readonly sidebarService = inject(SidebarService);

  readonly navItems: NavItem[] = [
    { icon: 'pi-th-large', labelKey: 'sidebar.dashboard', route: '/dashboard' },
    { icon: 'pi-comments', labelKey: 'sidebar.inbox', route: '/dashboard/inbox' },
    { icon: 'pi-users', labelKey: 'sidebar.contacts', route: '/dashboard/contacts' },
    { icon: 'pi-megaphone', labelKey: 'sidebar.campaigns', route: '/dashboard/campaigns' },
    { icon: 'pi-bolt', labelKey: 'sidebar.automation', route: '/dashboard/automation' },
    { icon: 'pi-server', labelKey: 'sidebar.instances', route: '/dashboard/instances' },
    { icon: 'pi-phone', labelKey: 'sidebar.numbers', route: '/dashboard/numbers' },
    { icon: 'pi-envelope', labelKey: 'sidebar.messages', route: '/dashboard/messages' },
    { icon: 'pi-heart-fill', labelKey: 'sidebar.health', route: '/dashboard/health' },
    { icon: 'pi-bell', labelKey: 'sidebar.notifications', route: '/dashboard/notifications' },
    { icon: 'pi-send', labelKey: 'sidebar.sendMessage', route: '/dashboard/send-message' },
    { icon: 'pi-chart-line', labelKey: 'sidebar.activityFeed', route: '/dashboard/activity' },
    { icon: 'pi-code', labelKey: 'sidebar.developer', route: '/dashboard/developer' },
    { icon: 'pi-receipt', labelKey: 'sidebar.billing', route: '/dashboard/billing' },
    { icon: 'pi-user-plus', labelKey: 'sidebar.users', route: '/dashboard/users' },
  ];

  getToggleIcon(): string {
    const isRtl = this.langService.isRtl();
    const isExpanded = this.sidebarService.expanded();
    if (isRtl) return isExpanded ? 'pi-chevron-right' : 'pi-chevron-left';
    return isExpanded ? 'pi-chevron-left' : 'pi-chevron-right';
  }
}
