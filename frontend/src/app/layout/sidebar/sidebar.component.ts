import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TokenService } from '../../core/services';

interface NavItem {
  icon: string;
  label: string;
  route: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, MatIconModule, MatTooltipModule],
  template: `
    <aside
      class="fixed left-0 top-16 bottom-0 z-40 flex flex-col border-r transition-all duration-300 ease-in-out bg-white dark:bg-slate-900 border-slate-200 dark:border-slate-700/50"
      [class.w-64]="expanded()"
      [class.w-[70px]]="!expanded()">

      <!-- Toggle -->
      <button
        (click)="expanded.set(!expanded())"
        class="absolute -right-3 top-6 w-6 h-6 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-full flex items-center justify-center cursor-pointer hover:bg-emerald-50 dark:hover:bg-emerald-950/30 transition-colors z-10 shadow-sm">
        <mat-icon class="!text-[14px] text-slate-500">{{ expanded() ? 'chevron_left' : 'chevron_right' }}</mat-icon>
      </button>

      <!-- Navigation -->
      <nav class="flex-1 overflow-y-auto py-4 px-3 space-y-1">
        @for (item of navItems; track item.route) {
          <a
            [routerLink]="item.route"
            routerLinkActive="!bg-emerald-50 dark:!bg-emerald-950/40 !text-emerald-600 dark:!text-emerald-400"
            [routerLinkActiveOptions]="{exact: item.route === '/dashboard'}"
            [matTooltip]="expanded() ? '' : item.label"
            matTooltipPosition="right"
            class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800/50 hover:text-slate-900 dark:hover:text-slate-200 transition-all no-underline">
            <mat-icon class="!text-[20px] shrink-0">{{ item.icon }}</mat-icon>
            @if (expanded()) {
              <span class="truncate">{{ item.label }}</span>
            }
          </a>
        }
      </nav>

      <!-- User section -->
      <div class="border-t border-slate-200 dark:border-slate-700/50 p-3">
        <a
          routerLink="/dashboard/settings"
          routerLinkActive="!bg-emerald-50 dark:!bg-emerald-950/40 !text-emerald-600"
          [matTooltip]="expanded() ? '' : 'Settings'"
          matTooltipPosition="right"
          class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-all no-underline">
          <mat-icon class="!text-[20px] shrink-0">settings</mat-icon>
          @if (expanded()) {
            <span class="truncate">Settings</span>
          }
        </a>
      </div>
    </aside>
  `,
})
export class SidebarComponent {
  private readonly tokenService = inject(TokenService);
  readonly expanded = signal(true);

  readonly navItems: NavItem[] = [
    { icon: 'dashboard', label: 'Dashboard', route: '/dashboard' },
    { icon: 'router', label: 'API Instances', route: '/dashboard/instances' },
    { icon: 'phone_iphone', label: 'Phone Numbers', route: '/dashboard/numbers' },
    { icon: 'message', label: 'Messages', route: '/dashboard/messages' },
    { icon: 'monitor_heart', label: 'Health', route: '/dashboard/health' },
    { icon: 'receipt_long', label: 'Billing', route: '/dashboard/billing' },
    { icon: 'group', label: 'Users', route: '/dashboard/users' },
  ];
}
