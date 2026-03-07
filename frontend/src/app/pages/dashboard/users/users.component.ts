import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services';
import { CompanyUser, UserUpsertRequest } from '../../../core/models';
import { TokenService } from '../../../core/services/token.service';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatSnackBarModule, FormsModule],
  template: `
    <div class="space-y-6">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">Team Members</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">Manage users in your company</p>
        </div>
        <button mat-flat-button class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700" (click)="openForm()">
          <mat-icon>person_add</mat-icon> Add User
        </button>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-16"><mat-spinner diameter="36"></mat-spinner></div>
      } @else if (showForm()) {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-8 max-w-lg">
          <h2 class="text-lg font-bold text-slate-900 dark:text-white mb-6">
            {{ editingUser() ? 'Edit User' : 'New User' }}
          </h2>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Full Name</label>
              <input type="text" [(ngModel)]="form.fullName"
                class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Email</label>
              <input type="email" [(ngModel)]="form.email"
                class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Password {{ editingUser() ? '(leave blank to keep)' : '' }}</label>
              <input type="password" [(ngModel)]="form.password"
                class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Role</label>
              <select [(ngModel)]="form.role"
                class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none">
                <option value="Admin">Admin</option>
                <option value="User">User</option>
              </select>
            </div>
            <div class="flex gap-3 pt-2">
              <button mat-flat-button class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700" (click)="save()" [disabled]="saving()">
                @if (saving()) { <mat-spinner diameter="18"></mat-spinner> } @else { Save }
              </button>
              <button mat-stroked-button class="!rounded-xl" (click)="showForm.set(false)">Cancel</button>
            </div>
          </div>
        </div>
      } @else {
        <!-- User Cards -->
        @if (users().length === 0) {
          <div class="text-center py-16 bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50">
            <mat-icon class="!text-[48px] text-slate-300 dark:text-slate-600">group</mat-icon>
            <h3 class="text-lg font-semibold text-slate-900 dark:text-white mt-4">No team members</h3>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">Add your first team member</p>
          </div>
        } @else {
          <div class="grid gap-4">
            @for (user of users(); track user.companyUserId) {
              <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-5 flex items-center gap-4">
                <div class="w-12 h-12 rounded-full bg-emerald-100 dark:bg-emerald-900/30 flex items-center justify-center">
                  <span class="text-lg font-semibold text-emerald-600 dark:text-emerald-400">{{ user.fullName.charAt(0).toUpperCase() }}</span>
                </div>
                <div class="flex-1 min-w-0">
                  <h4 class="font-semibold text-slate-900 dark:text-white truncate">{{ user.fullName }}</h4>
                  <p class="text-sm text-slate-500 dark:text-slate-400 truncate">{{ user.email }}</p>
                </div>
                <span class="px-3 py-1 rounded-full text-xs font-semibold"
                  [class]="user.role === 'Admin' ? 'bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-400' : 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400'">
                  {{ user.role }}
                </span>
                <div class="flex gap-1">
                  <button mat-icon-button (click)="editUser(user)"><mat-icon class="!text-slate-400 hover:!text-emerald-500">edit</mat-icon></button>
                  @if (user.companyUserId !== currentUserId()) {
                    <button mat-icon-button (click)="deleteUser(user)"><mat-icon class="!text-slate-400 hover:!text-red-500">delete</mat-icon></button>
                  }
                </div>
              </div>
            }
          </div>
        }
      }
    </div>
  `,
})
export class UsersComponent implements OnInit {
  private api = inject(ApiService);
  private snack = inject(MatSnackBar);
  private token = inject(TokenService);

  users = signal<CompanyUser[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingUser = signal<CompanyUser | null>(null);
  currentUserId = computed(() => this.token.userId());

  form: UserUpsertRequest = { fullName: '', email: '', password: '', role: 'User', isActive: true };

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.get<CompanyUser[]>('/users').subscribe({
      next: (u) => { this.users.set(u); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.editingUser.set(null);
    this.form = { fullName: '', email: '', password: '', role: 'User', isActive: true };
    this.showForm.set(true);
  }

  editUser(u: CompanyUser): void {
    this.editingUser.set(u);
    this.form = { fullName: u.fullName, email: u.email, password: '', role: u.role, isActive: u.isActive };
    this.showForm.set(true);
  }

  save(): void {
    this.saving.set(true);
    const ed = this.editingUser();
    const obs = ed
      ? this.api.put<CompanyUser>(`/users/${ed.companyUserId}`, this.form)
      : this.api.post<CompanyUser>('/users', this.form);
    obs.subscribe({
      next: () => { this.snack.open('User saved', 'OK', { duration: 3000 }); this.showForm.set(false); this.saving.set(false); this.load(); },
      error: (e) => { this.snack.open(e?.error?.message || 'Error saving user', 'OK', { duration: 4000 }); this.saving.set(false); },
    });
  }

  deleteUser(u: CompanyUser): void {
    if (!confirm(`Delete user "${u.fullName}"?`)) return;
    this.api.delete(`/users/${u.companyUserId}`).subscribe({
      next: () => { this.snack.open('User deleted', 'OK', { duration: 3000 }); this.load(); },
      error: () => this.snack.open('Error deleting user', 'OK', { duration: 4000 }),
    });
  }
}
