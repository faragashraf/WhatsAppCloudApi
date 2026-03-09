import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/services';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, ProgressSpinnerModule, ToastModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-emerald-50/30 to-green-50/20 dark:from-slate-950 dark:via-emerald-950/10 dark:to-slate-950 px-4">
      <div class="w-full max-w-md">
        <div class="text-center mb-8">
          <a routerLink="/" class="inline-flex items-center gap-2 no-underline">
            <div class="w-10 h-10 bg-gradient-to-br from-emerald-500 to-green-600 rounded-xl flex items-center justify-center shadow-lg shadow-emerald-500/25">
              <i class="pi pi-comments text-white"></i>
            </div>
            <span class="text-2xl font-bold bg-gradient-to-r from-emerald-600 to-green-600 bg-clip-text text-transparent">WaCloud</span>
          </a>
        </div>

        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-8 shadow-xl shadow-slate-200/50 dark:shadow-none">
          <a routerLink="/login" class="inline-flex items-center gap-1 text-sm text-slate-500 dark:text-slate-400 hover:text-emerald-600 no-underline mb-6">
            <i class="pi pi-arrow-left"></i>
            Back to login
          </a>

          <h1 class="text-2xl font-bold text-slate-900 dark:text-white mb-2">Forgot password?</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mb-8">Enter your email and we'll send you a reset link.</p>

          <form (ngSubmit)="onSubmit()" class="space-y-5">
            <div class="flex flex-col gap-2 w-full">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Email Address</label>
              <div class="flex items-center gap-2">
                <i class="pi pi-envelope text-slate-400"></i>
                <input pInputText type="email" [(ngModel)]="email" name="email" required class="w-full" />
              </div>
            </div>

            <button pButton type="submit" [disabled]="loading()"
              class="!bg-emerald-600 !text-white !rounded-xl w-full !py-3 hover:!bg-emerald-700 !text-base !font-semibold">
              @if (loading()) {
                <p-progressSpinner [style]="{'width': '20px', 'height': '20px'}" strokeWidth="4" class="inline-block mr-2" />
              }
              Send Reset Link
            </button>
          </form>
        </div>
      </div>
    </div>
  `,
})
export class ForgotPasswordComponent {
  email = '';
  loading = signal(false);

  private messageService = inject(MessageService);

  constructor(private authService: AuthService) {}

  onSubmit(): void {
    if (!this.email) return;
    this.loading.set(true);
    this.authService.forgotPassword(this.email).subscribe({
      next: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'success', summary: 'Reset link sent to your email.', life: 4000 });
      },
      error: (err: any) => {
        this.loading.set(false);
        const msg = err.error?.message || 'Failed to send reset link. Please try again.';
        this.messageService.add({ severity: 'error', summary: msg, life: 4000 });
      },
    });
  }
}
