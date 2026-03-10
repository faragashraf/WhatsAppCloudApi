import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService, LanguageService } from '../../../core/services';
import { LogoComponent } from '../../../shared/components/logo/logo.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, ProgressSpinnerModule, ToastModule, TranslateModule, LogoComponent],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-emerald-50/30 to-green-50/20 dark:from-slate-950 dark:via-emerald-950/10 dark:to-slate-950 px-4">
      <div class="w-full max-w-md">
        <!-- Logo -->
        <div class="text-center mb-8">
          <a routerLink="/" class="inline-flex items-center gap-2 no-underline">
            <app-logo size="md" [showText]="true" />
          </a>
        </div>

        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-8 shadow-xl shadow-slate-200/50 dark:shadow-none">
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white mb-2">{{ 'nav.login' | translate }}</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mb-8">{{ 'login.subtitle' | translate }}</p>

          <form (ngSubmit)="onLogin()" class="space-y-5">
            <div class="flex flex-col gap-2 w-full">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Email Address</label>
              <div class="flex items-center gap-2">
                <i class="pi pi-envelope text-slate-400"></i>
                <input pInputText type="email" [(ngModel)]="email" name="email" required class="w-full" />
              </div>
            </div>

            <div class="flex flex-col gap-2 w-full">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Password</label>
              <div class="flex items-center gap-2">
                <i class="pi pi-lock text-slate-400"></i>
                <input pInputText [type]="showPassword() ? 'text' : 'password'" [(ngModel)]="password" name="password" required class="w-full" />
                <button pButton [text]="true" [rounded]="true" type="button" (click)="showPassword.set(!showPassword())" class="!text-slate-400">
                  <i [class]="showPassword() ? 'pi pi-eye-slash' : 'pi pi-eye'"></i>
                </button>
              </div>
            </div>

            <div class="flex justify-end">
              <a routerLink="/forgot-password" class="text-sm text-emerald-600 dark:text-emerald-400 hover:underline no-underline">
                Forgot password?
              </a>
            </div>

            <button pButton type="submit" [disabled]="loading()"
              class="!bg-emerald-600 !text-white !rounded-xl w-full !py-3 hover:!bg-emerald-700 !text-base !font-semibold">
              @if (loading()) {
                <p-progressSpinner [style]="{'width': '20px', 'height': '20px'}" strokeWidth="4" class="inline-block mr-2" />
              }
              Sign In
            </button>
          </form>

          <p class="text-center text-sm text-slate-500 dark:text-slate-400 mt-6">
            Don't have an account?
            <a routerLink="/register" class="text-emerald-600 dark:text-emerald-400 font-semibold hover:underline no-underline ml-1">
              Create account
            </a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class LoginComponent implements OnInit {
  email = '';
  password = '';
  loading = signal(false);
  showPassword = signal(false);

  private messageService = inject(MessageService);
  private route = inject(ActivatedRoute);
  private lang = inject(LanguageService);

  constructor(
    private authService: AuthService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    const reason = this.route.snapshot.queryParamMap.get('reason');
    if (!reason) return;

    const isArabic = this.lang.currentLang() === 'ar';
    const summaryByReason: Record<string, string> = {
      auth_required: isArabic
        ? '\u064a\u0631\u062c\u0649 \u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644 \u0644\u0644\u0645\u062a\u0627\u0628\u0639\u0629.'
        : 'Please sign in to continue.',
      session_expired: isArabic
        ? '\u0627\u0646\u062a\u0647\u062a \u062c\u0644\u0633\u062a\u0643\u060c \u064a\u0631\u062c\u0649 \u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644 \u0645\u062c\u062f\u062f\u064b\u0627.'
        : 'Your session has expired. Please sign in again.',
    };

    const summary = summaryByReason[reason];
    if (summary) {
      this.messageService.add({ severity: 'info', summary, life: 4500 });
    }
  }

  onLogin(): void {
    if (!this.email || !this.password) return;
    this.loading.set(true);
    this.authService.login({ email: this.email, password: this.password }).subscribe({
      next: () => {
        this.loading.set(false);
        const redirectUrl = this.route.snapshot.queryParamMap.get('redirectUrl');
        const safeRedirect = redirectUrl && redirectUrl.startsWith('/') ? redirectUrl : '/dashboard';
        this.router.navigateByUrl(safeRedirect);
      },
      error: (err: any) => {
        this.loading.set(false);
        const msg = err.error?.message || 'Login failed. Please try again.';
        this.messageService.add({ severity: 'error', summary: msg, life: 4000 });
      },
    });
  }
}
