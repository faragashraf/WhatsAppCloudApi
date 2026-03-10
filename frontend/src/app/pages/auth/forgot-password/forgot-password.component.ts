import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { InputOtpModule } from 'primeng/inputotp';
import { PasswordModule } from 'primeng/password';
import { AuthService } from '../../../core/services';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, ProgressSpinnerModule, ToastModule, InputOtpModule, PasswordModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-emerald-50/30 to-green-50/20 dark:from-slate-950 dark:via-emerald-950/10 dark:to-slate-950 px-4">
      <div class="w-full max-w-md">
        <!-- Logo -->
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

          <!-- Step Indicators -->
          <div class="flex items-center justify-center gap-2 mb-8">
            @for (s of [1,2,3]; track s) {
              <div class="flex items-center gap-2">
                <div class="w-8 h-8 rounded-full flex items-center justify-center text-sm font-semibold transition-all duration-300"
                  [class]="step() >= s
                    ? 'bg-emerald-500 text-white shadow-md shadow-emerald-500/25'
                    : 'bg-slate-100 dark:bg-slate-700 text-slate-400 dark:text-slate-500'">
                  @if (step() > s) {
                    <i class="pi pi-check !text-[12px]"></i>
                  } @else {
                    {{ s }}
                  }
                </div>
                @if (s < 3) {
                  <div class="w-10 h-0.5 rounded transition-colors duration-300"
                    [class]="step() > s ? 'bg-emerald-500' : 'bg-slate-200 dark:bg-slate-700'"></div>
                }
              </div>
            }
          </div>

          <!-- Step 1: Email -->
          @if (step() === 1) {
            <h1 class="text-2xl font-bold text-slate-900 dark:text-white mb-2">Forgot password?</h1>
            <p class="text-sm text-slate-500 dark:text-slate-400 mb-8">Enter your email and we'll send you a verification code.</p>

            <form (ngSubmit)="submitEmail()" class="space-y-5">
              <div class="flex flex-col gap-2 w-full">
                <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Email Address</label>
                <div class="flex items-center gap-2">
                  <i class="pi pi-envelope text-slate-400"></i>
                  <input pInputText type="email" [(ngModel)]="email" name="email" required class="w-full" placeholder="your&#64;email.com" />
                </div>
              </div>

              <button pButton type="submit" [disabled]="loading() || !email"
                class="!bg-emerald-600 !text-white !rounded-xl w-full !py-3 hover:!bg-emerald-700 !text-base !font-semibold">
                @if (loading()) {
                  <p-progressSpinner [style]="{'width': '20px', 'height': '20px'}" strokeWidth="4" class="inline-block mr-2" />
                }
                Send Verification Code
              </button>
            </form>
          }

          <!-- Step 2: OTP -->
          @if (step() === 2) {
            <h1 class="text-2xl font-bold text-slate-900 dark:text-white mb-2">Enter verification code</h1>
            <p class="text-sm text-slate-500 dark:text-slate-400 mb-2">We sent a 6-digit code to</p>
            <p class="text-sm font-semibold text-emerald-600 mb-8">{{ email }}</p>

            <form (ngSubmit)="submitOtp()" class="space-y-5">
              <div class="flex justify-center">
                <p-inputOtp [(ngModel)]="otp" name="otp" [length]="6" [integerOnly]="true" />
              </div>

              <button pButton type="submit" [disabled]="loading() || otp.length < 6"
                class="!bg-emerald-600 !text-white !rounded-xl w-full !py-3 hover:!bg-emerald-700 !text-base !font-semibold">
                @if (loading()) {
                  <p-progressSpinner [style]="{'width': '20px', 'height': '20px'}" strokeWidth="4" class="inline-block mr-2" />
                }
                Verify Code
              </button>

              <div class="text-center">
                <button type="button" (click)="resendOtp()" [disabled]="resendCooldown() > 0"
                  class="text-sm text-emerald-600 hover:text-emerald-700 font-medium bg-transparent border-none cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed">
                  @if (resendCooldown() > 0) {
                    Resend in {{ resendCooldown() }}s
                  } @else {
                    Resend code
                  }
                </button>
              </div>
            </form>
          }

          <!-- Step 3: New Password -->
          @if (step() === 3) {
            <h1 class="text-2xl font-bold text-slate-900 dark:text-white mb-2">Set new password</h1>
            <p class="text-sm text-slate-500 dark:text-slate-400 mb-8">Choose a strong password for your account.</p>

            <form (ngSubmit)="submitNewPassword()" class="space-y-5">
              <div class="flex flex-col gap-2 w-full">
                <label class="text-sm font-medium text-slate-700 dark:text-slate-300">New Password</label>
                <p-password [(ngModel)]="newPassword" name="newPassword" [toggleMask]="true"
                  [feedback]="true" styleClass="w-full" inputStyleClass="w-full" />
              </div>
              <div class="flex flex-col gap-2 w-full">
                <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Confirm Password</label>
                <p-password [(ngModel)]="confirmPassword" name="confirmPassword" [toggleMask]="true"
                  [feedback]="false" styleClass="w-full" inputStyleClass="w-full" />
              </div>

              @if (newPassword && confirmPassword && newPassword !== confirmPassword) {
                <p class="text-xs text-red-500 -mt-2">Passwords do not match</p>
              }

              <button pButton type="submit" [disabled]="loading() || !newPassword || newPassword !== confirmPassword || newPassword.length < 6"
                class="!bg-emerald-600 !text-white !rounded-xl w-full !py-3 hover:!bg-emerald-700 !text-base !font-semibold">
                @if (loading()) {
                  <p-progressSpinner [style]="{'width': '20px', 'height': '20px'}" strokeWidth="4" class="inline-block mr-2" />
                }
                Reset Password
              </button>
            </form>
          }
        </div>
      </div>
    </div>
  `,
})
export class ForgotPasswordComponent {
  email = '';
  otp = '';
  newPassword = '';
  confirmPassword = '';

  step = signal(1);
  loading = signal(false);
  resendCooldown = signal(0);

  private messageService = inject(MessageService);
  private router = inject(Router);
  private authService = inject(AuthService);

  private resendTimer: any;

  // Step 1 → send OTP
  submitEmail(): void {
    if (!this.email) return;
    this.loading.set(true);
    this.authService.forgotPassword(this.email).subscribe({
      next: () => {
        this.loading.set(false);
        this.step.set(2);
        this.startResendCooldown();
        this.messageService.add({ severity: 'success', summary: 'Verification code sent to your email.', life: 4000 });
      },
      error: () => {
        this.loading.set(false);
        // Always move to step 2 even on "error" – the backend silently succeeds for security
        this.step.set(2);
        this.startResendCooldown();
        this.messageService.add({ severity: 'info', summary: 'If the email exists, a code was sent.', life: 4000 });
      },
    });
  }

  // Step 2 → verify OTP
  submitOtp(): void {
    if (this.otp.length < 6) return;
    this.loading.set(true);
    this.authService.verifyOtp(this.email, this.otp).subscribe({
      next: () => {
        this.loading.set(false);
        this.step.set(3);
        this.messageService.add({ severity: 'success', summary: 'Code verified!', life: 3000 });
      },
      error: (err: any) => {
        this.loading.set(false);
        const msg = err.error?.message || 'Invalid or expired code. Please try again.';
        this.messageService.add({ severity: 'error', summary: msg, life: 4000 });
      },
    });
  }

  // Step 3 → reset password
  submitNewPassword(): void {
    if (!this.newPassword || this.newPassword !== this.confirmPassword) return;
    this.loading.set(true);
    this.authService.resetPassword(this.email, this.otp, this.newPassword).subscribe({
      next: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'success', summary: 'Password reset successful! Redirecting to login...', life: 3000 });
        setTimeout(() => this.router.navigate(['/login']), 2000);
      },
      error: (err: any) => {
        this.loading.set(false);
        const msg = err.error?.message || 'Failed to reset password.';
        this.messageService.add({ severity: 'error', summary: msg, life: 4000 });
      },
    });
  }

  // Resend OTP
  resendOtp(): void {
    if (this.resendCooldown() > 0) return;
    this.authService.forgotPassword(this.email).subscribe();
    this.startResendCooldown();
    this.messageService.add({ severity: 'info', summary: 'New code sent.', life: 3000 });
  }

  private startResendCooldown(): void {
    this.resendCooldown.set(60);
    clearInterval(this.resendTimer);
    this.resendTimer = setInterval(() => {
      const v = this.resendCooldown();
      if (v <= 1) { clearInterval(this.resendTimer); this.resendCooldown.set(0); }
      else this.resendCooldown.set(v - 1);
    }, 1000);
  }
}
