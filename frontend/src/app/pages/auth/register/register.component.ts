import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink, MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule, MatSnackBarModule, MatProgressSpinnerModule],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-emerald-50/30 to-green-50/20 dark:from-slate-950 dark:via-emerald-950/10 dark:to-slate-950 px-4 py-12">
      <div class="w-full max-w-md">
        <!-- Logo -->
        <div class="text-center mb-8">
          <a routerLink="/" class="inline-flex items-center gap-2 no-underline">
            <div class="w-10 h-10 bg-gradient-to-br from-emerald-500 to-green-600 rounded-xl flex items-center justify-center shadow-lg shadow-emerald-500/25">
              <mat-icon class="text-white">chat</mat-icon>
            </div>
            <span class="text-2xl font-bold bg-gradient-to-r from-emerald-600 to-green-600 bg-clip-text text-transparent">WaCloud</span>
          </a>
        </div>

        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-8 shadow-xl shadow-slate-200/50 dark:shadow-none">
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white mb-2">Create your account</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mb-8">Start your 14-day free trial</p>

          <form (ngSubmit)="onRegister()" class="space-y-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Company Name</mat-label>
              <input matInput [(ngModel)]="form.companyName" name="companyName" required>
              <mat-icon matPrefix class="!text-slate-400 mr-2">business</mat-icon>
            </mat-form-field>

            <div class="grid grid-cols-2 gap-3">
              <mat-form-field appearance="outline">
                <mat-label>Company Code</mat-label>
                <input matInput [(ngModel)]="form.companyCode" name="companyCode" required>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Company Email</mat-label>
                <input matInput type="email" [(ngModel)]="form.companyEmail" name="companyEmail" required>
              </mat-form-field>
            </div>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Full Name</mat-label>
              <input matInput [(ngModel)]="form.adminFullName" name="adminFullName" required>
              <mat-icon matPrefix class="!text-slate-400 mr-2">person</mat-icon>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Admin Email</mat-label>
              <input matInput type="email" [(ngModel)]="form.adminEmail" name="adminEmail" required>
              <mat-icon matPrefix class="!text-slate-400 mr-2">email</mat-icon>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Password</mat-label>
              <input matInput [type]="showPassword() ? 'text' : 'password'" [(ngModel)]="form.password" name="password" required>
              <mat-icon matPrefix class="!text-slate-400 mr-2">lock</mat-icon>
              <button mat-icon-button matSuffix type="button" (click)="showPassword.set(!showPassword())">
                <mat-icon class="!text-slate-400">{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
            </mat-form-field>

            <button mat-flat-button type="submit" [disabled]="loading()"
              class="!bg-emerald-600 !text-white !rounded-xl w-full !py-3 hover:!bg-emerald-700 !text-base !font-semibold">
              @if (loading()) {
                <mat-spinner diameter="20" class="!inline-block mr-2"></mat-spinner>
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

  constructor(
    private authService: AuthService,
    private router: Router,
    private snackBar: MatSnackBar,
  ) {}

  onRegister(): void {
    if (!this.form.companyName || !this.form.adminEmail || !this.form.password) return;
    this.loading.set(true);
    this.authService.register(this.form).subscribe({
      next: () => {
        this.loading.set(false);
        this.snackBar.open('Account created successfully!', 'Close', { duration: 3000 });
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err.error?.message || 'Registration failed. Please try again.';
        this.snackBar.open(msg, 'Close', { duration: 4000 });
      },
    });
  }
}
