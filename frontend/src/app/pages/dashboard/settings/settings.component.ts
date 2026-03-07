import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormsModule } from '@angular/forms';
import { TokenService } from '../../../core/services/token.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatSnackBarModule, MatProgressSpinnerModule, FormsModule],
  template: `
    <div class="space-y-8 max-w-2xl">
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">Settings</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">Manage your account and preferences</p>
      </div>

      <!-- Profile Section -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <h2 class="text-lg font-semibold text-slate-900 dark:text-white mb-6 flex items-center gap-2">
          <mat-icon class="text-emerald-500">person</mat-icon> Profile
        </h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Full Name</label>
            <input type="text" [value]="userName()" disabled
              class="w-full px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 cursor-not-allowed" />
          </div>
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Role</label>
            <input type="text" [value]="userRole()" disabled
              class="w-full px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 cursor-not-allowed" />
          </div>
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Company ID</label>
            <input type="text" [value]="companyId()" disabled
              class="w-full px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 cursor-not-allowed" />
          </div>
        </div>
      </div>

      <!-- Appearance -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <h2 class="text-lg font-semibold text-slate-900 dark:text-white mb-6 flex items-center gap-2">
          <mat-icon class="text-emerald-500">palette</mat-icon> Appearance
        </h2>
        <div class="flex items-center justify-between">
          <div>
            <h4 class="font-medium text-slate-900 dark:text-white">Dark Mode</h4>
            <p class="text-sm text-slate-500 dark:text-slate-400">Switch between light and dark theme</p>
          </div>
          <button mat-flat-button (click)="theme.toggle()"
            class="!rounded-xl"
            [class]="theme.mode() === 'dark' ? '!bg-yellow-500 !text-white' : '!bg-slate-800 !text-white'">
            <mat-icon>{{ theme.mode() === 'dark' ? 'light_mode' : 'dark_mode' }}</mat-icon>
            {{ theme.mode() === 'dark' ? 'Light Mode' : 'Dark Mode' }}
          </button>
        </div>
      </div>

      <!-- Change Password -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <h2 class="text-lg font-semibold text-slate-900 dark:text-white mb-6 flex items-center gap-2">
          <mat-icon class="text-emerald-500">lock</mat-icon> Change Password
        </h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">New Password</label>
            <input type="password" [(ngModel)]="newPassword"
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
          </div>
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Confirm Password</label>
            <input type="password" [(ngModel)]="confirmPassword"
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
          </div>
          <button mat-flat-button class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700" (click)="changePassword()">
            Update Password
          </button>
        </div>
      </div>

      <!-- Danger Zone -->
      <div class="bg-red-50 dark:bg-red-900/10 rounded-2xl border border-red-200 dark:border-red-800/50 p-6">
        <h2 class="text-lg font-semibold text-red-700 dark:text-red-400 mb-4 flex items-center gap-2">
          <mat-icon class="!text-red-500">warning</mat-icon> Danger Zone
        </h2>
        <p class="text-sm text-red-600 dark:text-red-400 mb-4">Once you delete your account, there is no going back.</p>
        <button mat-stroked-button class="!border-red-500 !text-red-600 !rounded-xl hover:!bg-red-50 dark:hover:!bg-red-900/20">
          Delete Account
        </button>
      </div>
    </div>
  `,
})
export class SettingsComponent implements OnInit {
  private token = inject(TokenService);
  private snack = inject(MatSnackBar);
  theme = inject(ThemeService);

  userName = signal('');
  userRole = signal('');
  companyId = signal('');
  newPassword = '';
  confirmPassword = '';

  ngOnInit(): void {
    this.userName.set('User #' + (this.token.userId() || ''));
    this.userRole.set(this.token.role() ?? '');
    this.companyId.set(String(this.token.companyId() ?? ''));
  }

  changePassword(): void {
    if (!this.newPassword) { this.snack.open('Enter a new password', 'OK', { duration: 3000 }); return; }
    if (this.newPassword !== this.confirmPassword) { this.snack.open('Passwords do not match', 'OK', { duration: 3000 }); return; }
    this.snack.open('Password change not implemented yet', 'OK', { duration: 3000 });
  }
}
