import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService, TokenService } from '../../../core/services';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="min-h-[calc(100vh-64px)] flex items-center justify-center px-4 py-12 bg-gradient-to-br from-slate-50 via-sky-50/30 to-emerald-50/20 dark:from-slate-950 dark:via-slate-900 dark:to-slate-950">
      <div class="w-full max-w-xl rounded-2xl border border-[var(--app-border)] dark:border-slate-700/60 bg-[var(--app-surface)] dark:bg-slate-900/80 shadow-[var(--app-shadow-md)] p-6 sm:p-8 text-center space-y-4">
        <div class="mx-auto w-16 h-16 rounded-full bg-slate-100 dark:bg-slate-800 flex items-center justify-center">
          <i class="pi pi-map text-2xl text-slate-600 dark:text-slate-300"></i>
        </div>
        <p class="text-xs font-semibold tracking-[0.16em] uppercase text-[var(--app-text-muted)] dark:text-slate-400">404</p>
        <h1 class="text-2xl sm:text-3xl font-bold text-[var(--app-text)] dark:text-slate-100">{{ copy().title }}</h1>
        <p class="text-sm sm:text-base text-[var(--app-text-soft)] dark:text-slate-300 leading-relaxed">{{ copy().description }}</p>

        <div class="pt-2 flex flex-col sm:flex-row gap-3 justify-center">
          <a
            routerLink="/"
            class="inline-flex items-center justify-center px-5 py-2.5 rounded-xl bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white font-semibold no-underline transition-colors">
            {{ copy().homeCta }}
          </a>
          @if (tokenService.isAuthenticated()) {
            <a
              routerLink="/dashboard"
              class="inline-flex items-center justify-center px-5 py-2.5 rounded-xl border border-[var(--app-border)] dark:border-slate-700 text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-800/80 no-underline transition-colors">
              {{ copy().dashboardCta }}
            </a>
          }
        </div>
      </div>
    </section>
  `,
})
export class NotFoundComponent {
  readonly tokenService = inject(TokenService);
  private readonly lang = inject(LanguageService);

  readonly copy = computed(() => this.lang.currentLang() === 'ar'
    ? {
      title: 'الصفحة غير متاحة',
      description: 'الرابط الذي تحاول فتحه غير موجود أو تم نقله. يمكنك الرجوع للصفحة الرئيسية أو لوحة التحكم.',
      homeCta: 'الصفحة الرئيسية',
      dashboardCta: 'لوحة التحكم',
    }
    : {
      title: 'Page not found',
      description: 'The page you requested does not exist or has been moved. You can go back to home or open your dashboard.',
      homeCta: 'Back to Home',
      dashboardCta: 'Open Dashboard',
    });
}
