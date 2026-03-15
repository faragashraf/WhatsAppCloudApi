import { Component, computed, inject, signal } from '@angular/core';
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

interface NavGroup {
  titleKey: string;
  items: NavItem[];
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
      [style.width]="sidebarService.isMobile() ? 'min(18rem, 88vw)' : sidebarService.width"
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
      <nav class="flex-1 overflow-y-auto py-4 px-3 space-y-3">
        @if (showLabels()) {
          @for (group of filteredNavGroups(); track group.titleKey; let groupIndex = $index) {
            <section
              class="space-y-1 border-slate-200 dark:border-slate-700/60"
              [class.pt-2]="groupIndex > 0"
              [class.border-t]="groupIndex > 0">
              <button
                type="button"
                (click)="toggleGroup(groupIndex)"
                class="w-full flex items-center justify-between px-3 py-2 rounded-xl border border-transparent hover:border-slate-200 dark:hover:border-slate-700/70 hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-800/60 text-[var(--app-text-soft)] dark:text-slate-300 transition-colors cursor-pointer">
                <span class="text-[11px] font-semibold uppercase tracking-[0.08em]">
                  {{ group.titleKey | translate }}
                </span>
                <i
                  class="pi pi-chevron-down text-[12px] transition-transform duration-200"
                  [class.rotate-180]="isGroupOpen(groupIndex)"></i>
              </button>

              @if (isGroupOpen(groupIndex)) {
                <div class="space-y-1 pb-1">
                  @for (item of group.items; track item.route) {
                    <a
                      [routerLink]="item.route"
                      (click)="closeMobileAfterNavigate()"
                      routerLinkActive="!bg-[var(--app-primary-soft)] dark:!bg-[var(--app-primary-soft)] !text-[var(--app-primary-strong)] dark:!text-[var(--app-primary)]"
                      [routerLinkActiveOptions]="{exact: item.route === '/dashboard'}"
                      class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-800/60 hover:text-[var(--app-text)] dark:hover:text-slate-100 transition-all no-underline">
                      <i [class]="itemIconClass(item)"></i>
                      <span class="truncate">{{ item.labelKey | translate }}</span>
                    </a>
                  }
                </div>
              }
            </section>
          }
        } @else {
          @for (group of filteredNavGroups(); track group.titleKey; let groupIndex = $index) {
            <section
              class="space-y-1 border-slate-200 dark:border-slate-700/60"
              [class.pt-2]="groupIndex > 0"
              [class.border-t]="groupIndex > 0">
              @for (item of group.items; track item.route) {
                <a
                  [routerLink]="item.route"
                  (click)="closeMobileAfterNavigate()"
                  routerLinkActive="!bg-[var(--app-primary-soft)] dark:!bg-[var(--app-primary-soft)] !text-[var(--app-primary-strong)] dark:!text-[var(--app-primary)]"
                  [routerLinkActiveOptions]="{exact: item.route === '/dashboard'}"
                  [pTooltip]="item.labelKey | translate"
                  [tooltipPosition]="langService.isRtl() ? 'left' : 'right'"
                  class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-800/60 hover:text-[var(--app-text)] dark:hover:text-slate-100 transition-all no-underline">
                  <i [class]="itemIconClass(item)"></i>
                </a>
              }
            </section>
          }
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
          <i class="pi pi-shield shrink-0 text-[18px] text-amber-600 dark:text-amber-300"></i>
          @if (showLabels()) {
            <span class="truncate">{{ 'sidebar.superAdmin' | translate }}</span>
          }
        </a>
        <a
          routerLink="/dashboard/super-admin/logs"
          (click)="closeMobileAfterNavigate()"
          routerLinkActive="!bg-amber-100/80 dark:!bg-amber-900/30 !text-amber-700 dark:!text-amber-300"
          [pTooltip]="showLabels() ? '' : ('sidebar.systemLogs' | translate)"
          [tooltipPosition]="langService.isRtl() ? 'left' : 'right'"
          class="mt-2 flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-amber-700 dark:text-amber-300 hover:bg-amber-50 dark:hover:bg-amber-900/20 transition-all no-underline">
          <i class="pi pi-database shrink-0 text-[18px] text-amber-600 dark:text-amber-300"></i>
          @if (showLabels()) {
            <span class="truncate">{{ 'sidebar.systemLogs' | translate }}</span>
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
          <i class="pi pi-cog shrink-0 text-[18px] text-slate-600 dark:text-slate-300"></i>
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

  private readonly iconToneByRoute: Record<string, string> = {
    '/dashboard': 'text-sky-600 dark:text-sky-300',
    '/dashboard/inbox': 'text-emerald-600 dark:text-emerald-300',
    '/dashboard/contacts': 'text-cyan-600 dark:text-cyan-300',
    '/dashboard/leads': 'text-indigo-600 dark:text-indigo-300',
    '/dashboard/campaigns': 'text-orange-600 dark:text-orange-300',
    '/dashboard/templates': 'text-teal-600 dark:text-teal-300',
    '/dashboard/send-message': 'text-blue-600 dark:text-blue-300',
    '/dashboard/messages': 'text-slate-600 dark:text-slate-300',
    '/dashboard/notifications': 'text-rose-600 dark:text-rose-300',
    '/dashboard/activity': 'text-amber-600 dark:text-amber-300',
    '/dashboard/automation': 'text-lime-600 dark:text-lime-300',
    '/dashboard/whatsapp-policies': 'text-green-600 dark:text-green-300',
    '/dashboard/routing-teams': 'text-indigo-600 dark:text-indigo-300',
    '/dashboard/form-submissions': 'text-cyan-600 dark:text-cyan-300',
    '/dashboard/custom-webhooks': 'text-orange-600 dark:text-orange-300',
    '/dashboard/numbers': 'text-cyan-600 dark:text-cyan-300',
    '/dashboard/instances': 'text-blue-600 dark:text-blue-300',
    '/dashboard/email': 'text-sky-600 dark:text-sky-300',
    '/dashboard/health': 'text-emerald-600 dark:text-emerald-300',
    '/dashboard/billing': 'text-amber-600 dark:text-amber-300',
    '/dashboard/users': 'text-indigo-600 dark:text-indigo-300',
    '/dashboard/developer': 'text-slate-600 dark:text-slate-300',
  };

  readonly navGroups: NavGroup[] = [
    {
      titleKey: 'sidebar.groups.workspace',
      items: [
        { icon: 'pi-th-large', labelKey: 'sidebar.dashboard', route: '/dashboard' },
        { icon: 'pi-comments', labelKey: 'sidebar.inbox', route: '/dashboard/inbox', permKey: 'conversationsView' },
        { icon: 'pi-users', labelKey: 'sidebar.contacts', route: '/dashboard/contacts', permKey: 'contactsView' },
        { icon: 'pi-briefcase', labelKey: 'sidebar.leads', route: '/dashboard/leads', permKey: 'automationView' },
      ],
    },
    {
      titleKey: 'sidebar.groups.engagement',
      items: [
        { icon: 'pi-megaphone', labelKey: 'sidebar.campaigns', route: '/dashboard/campaigns', permKey: 'campaignsView' },
        { icon: 'pi-file-edit', labelKey: 'sidebar.templates', route: '/dashboard/templates', permKey: 'templatesView' },
        { icon: 'pi-send', labelKey: 'sidebar.sendMessage', route: '/dashboard/send-message', permKey: 'conversationsSend' },
        { icon: 'pi-envelope', labelKey: 'sidebar.messages', route: '/dashboard/messages', permKey: 'messagesView' },
        { icon: 'pi-bell', labelKey: 'sidebar.notifications', route: '/dashboard/notifications' },
        { icon: 'pi-chart-line', labelKey: 'sidebar.activityFeed', route: '/dashboard/activity' },
      ],
    },
    {
      titleKey: 'sidebar.groups.automation',
      items: [
        { icon: 'pi-bolt', labelKey: 'sidebar.automation', route: '/dashboard/automation', permKey: 'automationView' },
        { icon: 'pi-shield', labelKey: 'sidebar.whatsappPolicies', route: '/dashboard/whatsapp-policies' },
        { icon: 'pi-sitemap', labelKey: 'sidebar.routingTeams', route: '/dashboard/routing-teams', adminOnly: true },
        { icon: 'pi-table', labelKey: 'sidebar.formSubmissions', route: '/dashboard/form-submissions', permKey: 'automationView' },
        { icon: 'pi-link', labelKey: 'sidebar.customWebhooks', route: '/dashboard/custom-webhooks', permKey: 'automationView' },
      ],
    },
    {
      titleKey: 'sidebar.groups.administration',
      items: [
        { icon: 'pi-phone', labelKey: 'sidebar.numbers', route: '/dashboard/numbers' },
        { icon: 'pi-server', labelKey: 'sidebar.instances', route: '/dashboard/instances', adminOnly: true },
        { icon: 'pi-at', labelKey: 'sidebar.emailCenter', route: '/dashboard/email', adminOnly: true },
        { icon: 'pi-heart-fill', labelKey: 'sidebar.health', route: '/dashboard/health' },
        { icon: 'pi-receipt', labelKey: 'sidebar.billing', route: '/dashboard/billing', adminOnly: true },
        { icon: 'pi-user-plus', labelKey: 'sidebar.users', route: '/dashboard/users', adminOnly: true },
        { icon: 'pi-code', labelKey: 'sidebar.developer', route: '/dashboard/developer', adminOnly: true },
      ],
    },
  ];

  readonly filteredNavGroups = computed(() => this.navGroups
    .map(group => ({
      ...group,
      items: group.items.filter(item => this.canSee(item)),
    }))
    .filter(group => group.items.length > 0));
  readonly openedGroupIndex = signal<number | null>(null);

  toggleGroup(index: number): void {
    this.openedGroupIndex.set(this.openedGroupIndex() === index ? null : index);
  }

  isGroupOpen(index: number): boolean {
    return this.openedGroupIndex() === index;
  }

  private canSee(item: NavItem): boolean {
    if (this.tokenService.role() === 'Admin') return true;
    if (item.adminOnly) return false;
    if (item.permKey && !this.permService.has(item.permKey)) return false;
    return true;
  }

  getToggleIcon(): string {
    const isRtl = this.langService.isRtl();
    const isExpanded = this.sidebarService.expanded();
    if (isRtl) return isExpanded ? 'pi-chevron-right' : 'pi-chevron-left';
    return isExpanded ? 'pi-chevron-left' : 'pi-chevron-right';
  }

  itemIconClass(item: NavItem): string {
    const tone = this.iconToneByRoute[item.route] ?? 'text-slate-600 dark:text-slate-300';
    return `pi ${item.icon} shrink-0 text-[18px] transition-colors duration-200 ${tone}`;
  }

  closeMobileAfterNavigate(): void {
    if (this.sidebarService.isMobile()) this.sidebarService.closeMobile();
  }
}
