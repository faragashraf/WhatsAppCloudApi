import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/services';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, ProgressSpinnerModule, ToastModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-emerald-50/30 to-green-50/20 dark:from-slate-950 dark:via-emerald-950/10 dark:to-slate-950 px-4 py-12">
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
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white mb-2">Create your account</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mb-8">Start your 14-day free trial</p>

          <form (ngSubmit)="onRegister()" class="space-y-4">
            <div class="flex flex-col gap-2 w-full">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Company Name</label>
              <div class="flex items-center gap-2">
                <i class="pi pi-building text-slate-400"></i>
                <input pInputText [(ngModel)]="form.companyName" name="companyName" required class="w-full" />
              </div>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div class="flex flex-col gap-2">
                <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Company Code</label>
                <input pInputText [(ngModel)]="form.companyCode" name="companyCode" required class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Company Email</label>
                <input pInputText type="email" [(ngModel)]="form.companyEmail" name="companyEmail" required class="w-full" />
              </div>
            </div>

            <div class="flex flex-col gap-2 w-full">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Full Name</label>
              <div class="flex items-center gap-2">
                <i class="pi pi-user text-slate-400"></i>
                <input pInputText [(ngModel)]="form.adminFullName" name="adminFullName" required class="w-full" />
              </div>
            </div>

            <div class="flex flex-col gap-2 w-full">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Admin Email</label>
              <div class="flex items-center gap-2">
                <i class="pi pi-envelope text-slate-400"></i>
                <input pInputText type="email" [(ngModel)]="form.adminEmail" name="adminEmail" required class="w-full" />
              </div>
            </div>

            <div class="flex flex-col gap-2 w-full">
              <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Password</label>
              <div class="flex items-center gap-2">
                <i class="pi pi-lock text-slate-400"></i>
                <input pInputText [type]="showPassword() ? 'text' : 'password'" [(ngModel)]="form.password" name="password" required class="w-full" />
                <button pButton [text]="true" [rounded]="true" type="button" (click)="showPassword.set(!showPassword())" class="!text-slate-400">
                  <i [class]="showPassword() ? 'pi pi-eye-slash' : 'pi pi-eye'"></i>
                </button>
              </div>
            </div>

            <button pButton type="submit" [disabled]="loading()"
              class="!bg-emerald-600 !text-white !rounded-xl w-full !py-3 hover:!bg-emerald-700 !text-base !font-semibold">
              @if (loading()) {
                <p-progressSpinner [style]="{'width': '20px', 'height': '20px'}" strokeWidth="4" class="inline-block mr-2" />
              }
              Create Account
            </button>
          </form>

          <p class="text-center text-sm text-slate-500 dark:text-slate-400 mt-6">
            Already have an account?
            <a routerLink="/login" class="text-emerald-600 dark:text-emerald-400 font-semibold hover:underline no-underline ml-1">
              Sign in
            </a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class RegisterComponent {
  form = {
    companyName: '',
    companyCode: '',
    companyEmail: '',
    adminFullName: '',
    adminEmail: '',
    password: '',
  };
  loading = signal(false);
  showPassword = signal(false);

  private messageService = inject(MessageService);

  constructor(
    private authService: AuthService,
    private router: Router,
  ) {}

  onRegister(): void {
    if (!this.form.companyName || !this.form.adminEmail || !this.form.password) return;
    this.loading.set(true);
    this.authService.register(this.form).subscribe({
      next: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'success', summary: 'Account created successfully!', life: 3000 });
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err.error?.message || 'Registration failed. Please try again.';
        this.messageService.add({ severity: 'error', summary: msg, life: 4000 });
      },
    });
  }
}
