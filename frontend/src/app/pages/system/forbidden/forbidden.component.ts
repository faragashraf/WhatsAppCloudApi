import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LanguageService, TokenService } from '../../../core/services';

@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="min-h-[calc(100vh-64px)] flex items-center justify-center px-4 py-12 bg-gradient-to-br from-slate-50 via-emerald-50/20 to-sky-50/30 dark:from-slate-950 dark:via-slate-900 dark:to-slate-950">
      <div class="w-full max-w-xl rounded-2xl border border-[var(--app-border)] dark:border-slate-700/60 bg-[var(--app-surface)] dark:bg-slate-900/80 shadow-[var(--app-shadow-md)] p-6 sm:p-8 text-center space-y-4">
        <div class="mx-auto w-16 h-16 rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
          <i class="pi pi-lock text-2xl text-red-600 dark:text-red-400"></i>
        </div>
        <p class="text-xs font-semibold tracking-[0.16em] uppercase text-red-500">403</p>
        <h1 class="text-2xl sm:text-3xl font-bold text-[var(--app-text)] dark:text-slate-100">{{ copy().title }}</h1>
        <p class="text-sm sm:text-base text-[var(--app-text-soft)] dark:text-slate-300 leading-relaxed">{{ copy().description }}</p>

        @if (sourcePath) {
          <p class="text-xs text-[var(--app-text-muted)] dark:text-slate-400">
            {{ copy().requestedPathLabel }} <span class="dir-ltr font-mono">{{ sourcePath }}</span>
          </p>
        }

        <div class="pt-2 flex flex-col sm:flex-row gap-3 justify-center">
          @if (tokenService.isAuthenticated()) {
            <a
              routerLink="/dashboard"
              class="inline-flex items-center justify-center px-5 py-2.5 rounded-xl bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white font-semibold no-underline transition-colors">
              {{ copy().dashboardCta }}
            </a>
          } @else {
            <a
              routerLink="/login"
              class="inline-flex items-center justify-center px-5 py-2.5 rounded-xl bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white font-semibold no-underline transition-colors">
              {{ copy().loginCta }}
            </a>
          }
          <a
            routerLink="/"
            class="inline-flex items-center justify-center px-5 py-2.5 rounded-xl border border-[var(--app-border)] dark:border-slate-700 text-[var(--app-text-soft)] dark:text-slate-300 hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-800/80 no-underline transition-colors">
            {{ copy().homeCta }}
          </a>
        </div>
      </div>
    </section>
  `,
})
export class ForbiddenComponent {
  readonly tokenService = inject(TokenService);
  private readonly lang = inject(LanguageService);
  private readonly route = inject(ActivatedRoute);

  readonly copy = computed(() => this.lang.currentLang() === 'ar'
    ? {
      title: 'لا يمكنك الوصول إلى هذه الصفحة',
      description: 'هذه الصفحة تتطلب صلاحيات أعلى من حسابك الحالي. إذا كنت ترى أن هذا خطأ، تواصل مع مدير النظام.',
      requestedPathLabel: 'الرابط المطلوب:',
      dashboardCta: 'العودة للوحة التحكم',
      loginCta: 'تسجيل الدخول',
      homeCta: 'الصفحة الرئيسية',
    }
    : {
      title: 'You do not have access to this page',
      description: 'This page requires permissions that are not available for your current account. Contact your administrator if you think this is a mistake.',
      requestedPathLabel: 'Requested path:',
      dashboardCta: 'Go to Dashboard',
      loginCta: 'Sign In',
      homeCta: 'Back to Home',
    });

  readonly sourcePath = this.route.snapshot.queryParamMap.get('from');
}
