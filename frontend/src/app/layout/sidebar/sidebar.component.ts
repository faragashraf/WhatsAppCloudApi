import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TooltipModule } from 'primeng/tooltip';
import { TranslateModule } from '@ngx-translate/core';
import { TokenService, LanguageService, SidebarService, PermissionService } from '../../core/services';
import { UserPermissions } from '../../core/models';

interface NavItem {
  icon: string;
  labelKey: string;
  route: string;
  badge?: number;
  adminOnly?: boolean;
  /** Permission key required to see this nav item (non-admin only) */
  permKey?: keyof UserPermissions;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, TooltipModule, TranslateModule],
  template: `
    @if (sidebarService.isMobile() && sidebarService.mobileOpen()) {
      <button
        type="button"
        class="fixed inset-0 top-16 z-30 bg-slate-900/35 lg:hidden border-0 p-0 m-0"
        (click)="sidebarService.closeMobile()"
        aria-label="Close sidebar"></button>
    }

    <aside
      class="fixed top-16 bottom-0 z-40 flex flex-col border-[var(--app-border)] dark:border-slate-700/60 bg-white/85 dark:bg-slate-900/85 backdrop-blur-md transition-all duration-300 ease-in-out lg:translate-x-0"
      [class.border-r]="!langService.isRtl()"
      [class.border-l]="langService.isRtl()"
      [style.inset-inline-start]="'0'"
      [style.width]="sidebarService.isMobile() ? '16rem' : sidebarService.width"
      [class.shadow-2xl]="sidebarService.isMobile()"
      [class.translate-x-0]="!sidebarService.isMobile() || sidebarService.mobileOpen()"
      [class.-translate-x-full]="sidebarService.isMobile() && !sidebarService.mobileOpen() && !langService.isRtl()"
      [class.translate-x-full]="sidebarService.isMobile() && !sidebarService.mobileOpen() && langService.isRtl()">

      <!-- Toggle -->
      @if (!sidebarService.isMobile()) {
        <button
          (click)="sidebarService.toggle()"
          class="absolute top-6 w-6 h-6 bg-[var(--app-surface)] dark:bg-slate-800 border border-[var(--app-border)] dark:border-slate-600 rounded-full flex items-center justify-center cursor-pointer hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] transition-colors z-10 shadow-[0_8px_16px_-10px_rgba(13,37,63,0.45)]"
          [style.inset-inline-end]="'-0.75rem'">
          <i class="pi text-[14px] text-[var(--app-text-soft)] dark:text-slate-300" [class]="getToggleIcon()"></i>
        </button>
      }

      <!-- Navigation -->
      <nav class="flex-1 overflow-y-auto py-4 px-3 space-y-1">
        @for (item of filteredNavItems(); track item.route) {
          <a
            [routerLink]="item.route"
            (click)="closeMobileAfterNavigate()"
            routerLinkActive="!bg-[var(--app-primary-soft)] dark:!bg-[var(--app-primary-soft)] !text-[var(--app-primary-strong)] dark:!text-[var(--app-primary)]"
            [routerLinkActiveOptions]="{exact: item.route === '/dashboard'}"
            [pTooltip]="showLabels() ? '' : (item.labelKey | translate)"
            [tooltipPosition]="langService.isRtl() ? 'left' : 'right'"
            class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-800/60 hover:text-[var(--app-text)] dark:hover:text-slate-100 transition-all no-underline">
            <i class="pi shrink-0 text-[20px]" [class]="item.icon"></i>
            @if (showLabels()) {
              <span class="truncate">{{ item.labelKey | translate }}</span>
            }
          </a>
        }
      </nav>

      <!-- Super Admin (super admin only) -->
      @if (tokenService.isSuperAdmin()) {
      <div class="border-t border-[var(--app-border)] dark:border-slate-700/60 p-3">
        <a
          routerLink="/dashboard/super-admin"
          (click)="closeMobileAfterNavigate()"
          routerLinkActive="!bg-amber-100/80 dark:!bg-amber-900/30 !text-amber-700 dark:!text-amber-300"
          [pTooltip]="showLabels() ? '' : ('sidebar.superAdmin' | translate)"
          [tooltipPosition]="langService.isRtl() ? 'left' : 'right'"
          class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-amber-700 dark:text-amber-300 hover:bg-amber-50 dark:hover:bg-amber-900/20 transition-all no-underline">
          <i class="pi pi-shield shrink-0 text-[20px]"></i>
          @if (showLabels()) {
            <span class="truncate">{{ 'sidebar.superAdmin' | translate }}</span>
          }
        </a>
        <a
          routerLink="/dashboard/super-admin/logs"
          (click)="closeMobileAfterNavigate()"
          routerLinkActive="!bg-amber-100/80 dark:!bg-amber-900/30 !text-amber-700 dark:!text-amber-300"
          [pTooltip]="showLabels() ? '' : 'System Logs'"
          [tooltipPosition]="langService.isRtl() ? 'left' : 'right'"
          class="mt-2 flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-amber-700 dark:text-amber-300 hover:bg-amber-50 dark:hover:bg-amber-900/20 transition-all no-underline">
          <i class="pi pi-database shrink-0 text-[20px]"></i>
          @if (showLabels()) {
            <span class="truncate">System Logs</span>
          }
        </a>
      </div>
      }

      <!-- Settings section (admin only) -->
      @if (tokenService.role() === 'Admin') {
      <div class="border-t border-[var(--app-border)] dark:border-slate-700/60 p-3">
        <a
          routerLink="/dashboard/settings"
          (click)="closeMobileAfterNavigate()"
          routerLinkActive="!bg-[var(--app-primary-soft)] dark:!bg-[var(--app-primary-soft)] !text-[var(--app-primary-strong)] dark:!text-[var(--app-primary)]"
          [pTooltip]="showLabels() ? '' : ('sidebar.settings' | translate)"
          [tooltipPosition]="langService.isRtl() ? 'left' : 'right'"
          class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-800/60 transition-all no-underline">
          <i class="pi pi-cog shrink-0 text-[20px]"></i>
          @if (showLabels()) {
            <span class="truncate">{{ 'sidebar.settings' | translate }}</span>
          }
        </a>
      </div>
      }
    </aside>
  `,
})
export class SidebarComponent {
  protected readonly tokenService = inject(TokenService);
  private readonly permService = inject(PermissionService);
  readonly langService = inject(LanguageService);
  readonly sidebarService = inject(SidebarService);
  readonly showLabels = computed(() => this.sidebarService.isMobile() || this.sidebarService.expanded());

  readonly navItems: NavItem[] = [
    { icon: 'pi-th-large', labelKey: 'sidebar.dashboard', route: '/dashboard' },
    { icon: 'pi-comments', labelKey: 'sidebar.inbox', route: '/dashboard/inbox', permKey: 'conversationsView' },
    { icon: 'pi-users', labelKey: 'sidebar.contacts', route: '/dashboard/contacts', permKey: 'contactsView' },
    { icon: 'pi-megaphone', labelKey: 'sidebar.campaigns', route: '/dashboard/campaigns', permKey: 'campaignsView' },
    { icon: 'pi-bolt', labelKey: 'sidebar.automation', route: '/dashboard/automation', permKey: 'automationView' },
    { icon: 'pi-table', labelKey: 'sidebar.formSubmissions', route: '/dashboard/form-submissions', permKey: 'automationView' },
    { icon: 'pi-link', labelKey: 'sidebar.customWebhooks', route: '/dashboard/custom-webhooks', permKey: 'automationView' },
    { icon: 'pi-server', labelKey: 'sidebar.instances', route: '/dashboard/instances', adminOnly: true },
    { icon: 'pi-phone', labelKey: 'sidebar.numbers', route: '/dashboard/numbers' },
    { icon: 'pi-envelope', labelKey: 'sidebar.messages', route: '/dashboard/messages', permKey: 'messagesView' },
    { icon: 'pi-file-edit', labelKey: 'sidebar.templates', route: '/dashboard/templates', permKey: 'templatesView' },
    { icon: 'pi-heart-fill', labelKey: 'sidebar.health', route: '/dashboard/health' },
    { icon: 'pi-bell', labelKey: 'sidebar.notifications', route: '/dashboard/notifications' },
    { icon: 'pi-at', labelKey: 'sidebar.emailCenter', route: '/dashboard/email', adminOnly: true },
    { icon: 'pi-send', labelKey: 'sidebar.sendMessage', route: '/dashboard/send-message', permKey: 'conversationsSend' },
    { icon: 'pi-chart-line', labelKey: 'sidebar.activityFeed', route: '/dashboard/activity' },
    { icon: 'pi-code', labelKey: 'sidebar.developer', route: '/dashboard/developer', adminOnly: true },
    { icon: 'pi-receipt', labelKey: 'sidebar.billing', route: '/dashboard/billing', adminOnly: true },
    { icon: 'pi-user-plus', labelKey: 'sidebar.users', route: '/dashboard/users', adminOnly: true },
  ];

  readonly filteredNavItems = computed(() => {
    if (this.tokenService.role() === 'Admin') return this.navItems;
    return this.navItems.filter(item => {
      if (item.adminOnly) return false;
      if (item.permKey && !this.permService.has(item.permKey)) return false;
      return true;
    });
  });

  getToggleIcon(): string {
    const isRtl = this.langService.isRtl();
    const isExpanded = this.sidebarService.expanded();
    if (isRtl) return isExpanded ? 'pi-chevron-right' : 'pi-chevron-left';
    return isExpanded ? 'pi-chevron-left' : 'pi-chevron-right';
  }

  closeMobileAfterNavigate(): void {
    if (this.sidebarService.isMobile()) this.sidebarService.closeMobile();
  }
}
