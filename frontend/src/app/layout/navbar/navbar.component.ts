import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { TooltipModule } from 'primeng/tooltip';
import { TokenService, ThemeService, LanguageService } from '../../core/services';
import { LogoComponent } from '../../shared/components/logo/logo.component';
import { MenuItem } from 'primeng/api';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    TranslateModule,
    ButtonModule,
    MenuModule,
    TooltipModule,
    LogoComponent,
  ],
  template: `
    <nav class="fixed top-0 left-0 right-0 z-50 backdrop-blur-xl bg-white/85 dark:bg-slate-900/80 border-b border-[var(--app-border)] dark:border-slate-700/60 shadow-[0_10px_28px_-22px_rgba(13,37,63,0.5)]">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center h-16">
          <!-- Logo -->
          <a routerLink="/" class="flex items-center no-underline">
            <app-logo size="sm" [showText]="true" />
          </a>

          <!-- Desktop Nav -->
          <div class="hidden md:flex items-center gap-1">
            <a routerLink="/" routerLinkActive="!text-[var(--app-primary)] !font-semibold"
               [routerLinkActiveOptions]="{exact: true}"
               class="px-4 py-2 text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 hover:text-[var(--app-primary)] transition-colors rounded-lg hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] no-underline">
              {{ 'nav.home' | translate }}
            </a>
            <a routerLink="/pricing" routerLinkActive="!text-[var(--app-primary)] !font-semibold"
               class="px-4 py-2 text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 hover:text-[var(--app-primary)] transition-colors rounded-lg hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] no-underline">
              {{ 'nav.pricing' | translate }}
            </a>
            <a routerLink="/about" routerLinkActive="!text-[var(--app-primary)] !font-semibold"
               class="px-4 py-2 text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 hover:text-[var(--app-primary)] transition-colors rounded-lg hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] no-underline">
              {{ 'nav.about' | translate }}
            </a>
            <a routerLink="/whatsapp-meta-guide" routerLinkActive="!text-[var(--app-primary)] !font-semibold"
               class="px-4 py-2 text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 hover:text-[var(--app-primary)] transition-colors rounded-lg hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] no-underline">
              {{ 'nav.whatsappMetaGuide' | translate }}
            </a>
            <a routerLink="/contact" routerLinkActive="!text-[var(--app-primary)] !font-semibold"
               class="px-4 py-2 text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 hover:text-[var(--app-primary)] transition-colors rounded-lg hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] no-underline">
              {{ 'nav.contact' | translate }}
            </a>
          </div>

          <!-- Actions -->
          <div class="flex items-center gap-2">
            <!-- Language Switcher -->
            <button
              pButton
              [text]="true"
              [rounded]="true"
              severity="secondary"
              (click)="langService.toggle()"
              class="!w-10 !h-10 !border !border-[var(--app-border)] !bg-white/80 dark:!bg-slate-800/80"
              [pTooltip]="'language.switch' | translate"
              tooltipPosition="bottom">
              <span class="text-xs font-bold text-[var(--app-text-soft)] dark:text-slate-300">{{ langService.currentLang() === 'ar' ? 'EN' : 'AR' }}</span>
            </button>

            <button
              pButton
              [text]="true"
              [rounded]="true"
              severity="secondary"
              (click)="themeService.toggle()"
              class="!w-10 !h-10 !border !border-[var(--app-border)] !bg-white/80 dark:!bg-slate-800/80">
              <i class="pi text-[var(--app-text-soft)] dark:text-slate-300" [class]="themeService.mode() === 'dark' ? 'pi-sun' : 'pi-moon'"></i>
            </button>

            @if (tokenService.isAuthenticated()) {
              <a routerLink="/dashboard" pButton class="!bg-[var(--app-primary)] !text-white !rounded-xl hover:!bg-[var(--app-primary-strong)] !no-underline !shadow-[0_14px_30px_-18px_rgba(16,168,97,0.72)] !border-0">
                {{ 'nav.dashboard' | translate }}
              </a>
              <button
                pButton
                [text]="true"
                [rounded]="true"
                severity="secondary"
                (click)="userMenuRef.toggle($event)"
                class="!w-auto !h-10 !px-3 !gap-2 !flex !items-center !border !border-[var(--app-border)] !bg-white/85 dark:!bg-slate-800/80">
                <i class="pi pi-user text-[var(--app-text-soft)] dark:text-slate-300"></i>
                @if (tokenService.fullName()) {
                  <span class="text-sm font-medium text-[var(--app-text-soft)] dark:text-slate-300 max-w-[120px] truncate hidden sm:inline">{{ tokenService.fullName() }}</span>
                }
              </button>
              <p-menu #userMenuRef [model]="userMenuItems()" [popup]="true" />
            } @else {
              <a routerLink="/login" pButton [text]="true" class="!text-[var(--app-text-soft)] dark:!text-slate-300 !no-underline">
                {{ 'nav.login' | translate }}
              </a>
              <a routerLink="/register" pButton class="!bg-[var(--app-primary)] !text-white !rounded-xl hover:!bg-[var(--app-primary-strong)] !no-underline !shadow-[0_14px_30px_-18px_rgba(16,168,97,0.72)] !border-0">
                {{ 'nav.register' | translate }}
              </a>
            }

            <!-- Mobile menu -->
            <button pButton [text]="true" [rounded]="true" severity="secondary" class="md:!hidden !w-10 !h-10 !border !border-[var(--app-border)] !bg-white/80 dark:!bg-slate-800/80" (click)="mobileOpen.set(!mobileOpen())">
              <i class="pi text-[var(--app-text-soft)] dark:text-slate-300" [class]="mobileOpen() ? 'pi-times' : 'pi-bars'"></i>
            </button>
          </div>
        </div>
      </div>

      <!-- Mobile Nav -->
      @if (mobileOpen()) {
        <div class="md:hidden border-t border-[var(--app-border)] dark:border-slate-700/60 bg-white/95 dark:bg-slate-900 px-4 pb-4 pt-2 space-y-1">
          <a routerLink="/" (click)="mobileOpen.set(false)" class="block px-4 py-2 text-sm text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] rounded-lg no-underline">{{ 'nav.home' | translate }}</a>
          <a routerLink="/pricing" (click)="mobileOpen.set(false)" class="block px-4 py-2 text-sm text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] rounded-lg no-underline">{{ 'nav.pricing' | translate }}</a>
          <a routerLink="/about" (click)="mobileOpen.set(false)" class="block px-4 py-2 text-sm text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] rounded-lg no-underline">{{ 'nav.about' | translate }}</a>
          <a routerLink="/whatsapp-meta-guide" (click)="mobileOpen.set(false)" class="block px-4 py-2 text-sm text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] rounded-lg no-underline">{{ 'nav.whatsappMetaGuide' | translate }}</a>
          <a routerLink="/contact" (click)="mobileOpen.set(false)" class="block px-4 py-2 text-sm text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-primary-soft)] dark:hover:bg-[var(--app-primary-soft)] rounded-lg no-underline">{{ 'nav.contact' | translate }}</a>
        </div>
      }
    </nav>
  `,
})
export class NavbarComponent {
  readonly tokenService = inject(TokenService);
  readonly themeService = inject(ThemeService);
  readonly langService = inject(LanguageService);
  readonly mobileOpen = signal(false);

  userMenuItems = signal<MenuItem[]>([
    {
      label: 'Settings',
      icon: 'pi pi-cog',
      routerLink: '/dashboard/settings',
    },
    {
      label: 'Logout',
      icon: 'pi pi-sign-out',
      command: () => this.tokenService.logout(),
    },
  ]);

  constructor() {
    // Update menu labels on language change
    this.langService.currentLang;
    const updateLabels = () => {
      this.userMenuItems.set([
        { label: this.langService.currentLang() === 'ar' ? '\u0627\u0644\u0625\u0639\u062f\u0627\u062f\u0627\u062a' : 'Settings', icon: 'pi pi-cog', routerLink: '/dashboard/settings' },
        { label: this.langService.currentLang() === 'ar' ? '\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062e\u0631\u0648\u062c' : 'Logout', icon: 'pi pi-sign-out', command: () => this.tokenService.logout() },
      ]);
    };
    updateLabels();
  }
}

