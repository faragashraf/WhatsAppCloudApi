import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService } from '../../../core/services';
import {
  CompanyUser,
  CompanyUserRoutingSettings,
  UserUpsertRequest,
  UserPermissions,
  DEFAULT_PERMISSIONS,
  FULL_PERMISSIONS,
} from '../../../core/models';
import { TokenService } from '../../../core/services/token.service';
import { catchError, forkJoin, of } from 'rxjs';

type ManagedUser = CompanyUser & {
  canReceiveManualAssignments: boolean;
  canReceiveAutoAssignments: boolean;
  lastAutoAssignedAtUtc: string | null;
};

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [ButtonModule, ProgressSpinnerModule, ToastModule, ToggleSwitchModule, FormsModule, TranslateModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="space-y-6 min-w-0 app-wrap-safe">
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'users.title' | translate }}</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'users.subtitle' | translate }}</p>
        </div>
        <button pButton class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700 w-full sm:!w-auto" (click)="openForm()">
          <i class="pi pi-user-plus"></i> {{ 'users.add' | translate }}
        </button>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-16"><p-progressSpinner [style]="{'width':'36px','height':'36px'}" strokeWidth="4" /></div>
      } @else if (showForm()) {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-8 max-w-2xl">
          <h2 class="text-lg font-bold text-slate-900 dark:text-white mb-6">
            {{ (editingUser() ? 'users.edit' : 'users.new') | translate }}
          </h2>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'users.fullName' | translate }}</label>
              <input type="text" [(ngModel)]="form.fullName"
                class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'users.email' | translate }}</label>
              <input type="email" [(ngModel)]="form.email"
                class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'users.password' | translate }} {{ editingUser() ? ('users.passwordHint' | translate) : '' }}</label>
              <input type="password" [(ngModel)]="form.password"
                class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'users.role' | translate }}</label>
              <select [(ngModel)]="form.role" (ngModelChange)="onRoleChange($event)"
                class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none">
                <option value="Admin">Admin</option>
                <option value="Member">Member</option>
              </select>
            </div>

            <!-- Permissions Matrix (only for non-Admin) -->
            @if (form.role !== 'Admin') {
              <div class="border-t border-slate-200 dark:border-slate-700 pt-4 mt-2">
                <div class="flex items-center justify-between mb-4">
                  <h3 class="text-sm font-bold text-slate-900 dark:text-white">{{ 'users.permissions' | translate }}</h3>
                  <div class="flex gap-2">
                    <button (click)="setAllPermissions(true)" class="text-[11px] px-2 py-1 rounded-lg bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400 hover:bg-emerald-200 transition-colors">
                      {{ 'users.grantAll' | translate }}
                    </button>
                    <button (click)="setAllPermissions(false)" class="text-[11px] px-2 py-1 rounded-lg bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400 hover:bg-slate-200 transition-colors">
                      {{ 'users.revokeAll' | translate }}
                    </button>
                  </div>
                </div>

                <div class="space-y-4">
                  <!-- Contacts -->
                  @for (section of permSections; track section.key) {
                    <div class="bg-slate-50 dark:bg-slate-800/60 rounded-xl p-3">
                      <h4 class="text-xs font-semibold text-slate-600 dark:text-slate-400 uppercase tracking-wider mb-2">
                        {{ 'users.perm.' + section.key | translate }}
                      </h4>
                      <div class="grid grid-cols-2 sm:grid-cols-3 gap-2">
                        @for (p of section.perms; track p) {
                          <label class="flex items-center gap-2 cursor-pointer px-2 py-1.5 rounded-lg hover:bg-white dark:hover:bg-slate-700/50 transition-colors">
                            <p-toggleSwitch [(ngModel)]="formPermissions[p]" />
                            <span class="text-xs text-slate-700 dark:text-slate-300">{{ 'users.perm.' + p | translate }}</span>
                          </label>
                        }
                      </div>
                    </div>
                  }
                </div>
              </div>
            }

            <div class="flex gap-3 pt-2">
              <button pButton class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700" (click)="save()" [disabled]="saving()">
                @if (saving()) { <p-progressSpinner [style]="{'width':'18px','height':'18px'}" strokeWidth="4" /> } @else { {{ 'common.save' | translate }} }
              </button>
              <button pButton [outlined]="true" class="!rounded-xl" (click)="showForm.set(false)">{{ 'common.cancel' | translate }}</button>
            </div>
          </div>
        </div>
      } @else {
        <!-- User Cards -->
        @if (users().length === 0) {
          <div class="text-center py-16 bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50">
            <i class="pi pi-users !text-[48px] text-slate-300 dark:text-slate-600"></i>
            <h3 class="text-lg font-semibold text-slate-900 dark:text-white mt-4">{{ 'users.noUsers' | translate }}</h3>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'users.noUsersHint' | translate }}</p>
          </div>
        } @else {
          <div class="grid gap-4">
            @for (user of users(); track user.companyUserId) {
              <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-5 flex flex-col sm:flex-row sm:items-center gap-4">
                <div class="w-12 h-12 rounded-full bg-emerald-100 dark:bg-emerald-900/30 flex items-center justify-center">
                  <span class="text-lg font-semibold text-emerald-600 dark:text-emerald-400">{{ user.fullName.charAt(0).toUpperCase() }}</span>
                </div>
                <div class="flex-1 min-w-0">
                  <h4 class="font-semibold text-slate-900 dark:text-white truncate">{{ user.fullName }}</h4>
                  <p class="text-sm text-slate-500 dark:text-slate-400 truncate">{{ user.email }}</p>
                  <div class="flex flex-wrap items-center gap-4 mt-2 text-xs text-slate-500 dark:text-slate-400">
                    <label class="flex items-center gap-2">
                      <p-toggleSwitch
                        [ngModel]="user.canReceiveManualAssignments"
                        (ngModelChange)="updateRouting(user, 'manual', $event)" />
                      <span>Manual routing</span>
                    </label>
                    <label class="flex items-center gap-2">
                      <p-toggleSwitch
                        [ngModel]="user.canReceiveAutoAssignments"
                        (ngModelChange)="updateRouting(user, 'auto', $event)" />
                      <span>Auto routing</span>
                    </label>
                    @if (user.lastAutoAssignedAtUtc) {
                      <span class="text-[11px]">Last auto: {{ formatDate(user.lastAutoAssignedAtUtc) }}</span>
                    }
                  </div>
                </div>
                <span class="px-3 py-1 rounded-full text-xs font-semibold"
                  [class]="user.role === 'Admin' ? 'bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-400' : 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400'">
                  {{ user.role }}
                </span>
                <div class="flex gap-1 self-end sm:self-auto">
                  <button pButton [text]="true" [rounded]="true" (click)="editUser(user)"><i class="pi pi-pencil !text-slate-400 hover:!text-emerald-500"></i></button>
                  @if (user.companyUserId !== currentUserId()) {
                    <button pButton [text]="true" [rounded]="true" (click)="deleteUser(user)"><i class="pi pi-trash !text-slate-400 hover:!text-red-500"></i></button>
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
  private messageService = inject(MessageService);
  private token = inject(TokenService);

  users = signal<ManagedUser[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingUser = signal<CompanyUser | null>(null);
  currentUserId = computed(() => this.token.userId());

  form: UserUpsertRequest = { fullName: '', email: '', password: '', role: 'Member', isActive: true };
  formPermissions: Record<string, boolean> = { ...DEFAULT_PERMISSIONS };

  /** Permission sections for the matrix UI */
  readonly permSections = [
    { key: 'contacts', perms: ['contactsView', 'contactsCreate', 'contactsEdit', 'contactsDelete', 'contactsImport'] },
    { key: 'campaigns', perms: ['campaignsView', 'campaignsCreate', 'campaignsEdit', 'campaignsLaunch'] },
    { key: 'automation', perms: ['automationView', 'automationCreate', 'automationEdit', 'automationDelete'] },
    { key: 'conversations', perms: ['conversationsView', 'conversationsSend', 'conversationsAttach', 'conversationsAssign'] },
    { key: 'templates', perms: ['templatesView', 'templatesCreate', 'templatesEdit', 'templatesDelete'] },
    { key: 'messages', perms: ['messagesView'] },
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    forkJoin({
      users: this.api.get<CompanyUser[]>('/users').pipe(catchError(() => of([]))),
      routing: this.api.get<CompanyUserRoutingSettings[]>('/routing/users').pipe(catchError(() => of([]))),
    }).subscribe({
      next: ({ users, routing }) => {
        const routingMap = new Map(routing.map(item => [item.companyUserId, item]));
        this.users.set(users.map(user => {
          const settings = routingMap.get(user.companyUserId);
          return {
            ...user,
            canReceiveManualAssignments: settings?.canReceiveManualAssignments ?? true,
            canReceiveAutoAssignments: settings?.canReceiveAutoAssignments ?? true,
            lastAutoAssignedAtUtc: settings?.lastAutoAssignedAtUtc ?? null,
          };
        }));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.editingUser.set(null);
    this.form = { fullName: '', email: '', password: '', role: 'Member', isActive: true };
    this.formPermissions = { ...DEFAULT_PERMISSIONS };
    this.showForm.set(true);
  }

  editUser(u: CompanyUser): void {
    this.editingUser.set(u);
    this.form = { fullName: u.fullName, email: u.email, password: '', role: u.role, isActive: u.isActive };
    // Parse stored permissions or use defaults
    if (u.permissionsJson) {
      try { this.formPermissions = { ...DEFAULT_PERMISSIONS, ...JSON.parse(u.permissionsJson) }; }
      catch { this.formPermissions = { ...DEFAULT_PERMISSIONS }; }
    } else {
      this.formPermissions = u.role === 'Admin' ? { ...FULL_PERMISSIONS } : { ...DEFAULT_PERMISSIONS };
    }
    this.showForm.set(true);
  }

  onRoleChange(role: string): void {
    if (role === 'Admin') {
      this.formPermissions = { ...FULL_PERMISSIONS };
    }
  }

  setAllPermissions(value: boolean): void {
    for (const key of Object.keys(this.formPermissions)) {
      this.formPermissions[key] = value;
    }
    // Force re-render by spreading
    this.formPermissions = { ...this.formPermissions };
  }

  save(): void {
    this.saving.set(true);
    const ed = this.editingUser();
    // Attach permissions to the form only for non-Admin
    const body: UserUpsertRequest = { ...this.form };
    if (this.form.role !== 'Admin') {
      body.permissions = this.formPermissions as unknown as UserPermissions;
    }
    const obs = ed
      ? this.api.put<CompanyUser>(`/users/${ed.companyUserId}`, body)
      : this.api.post<CompanyUser>('/users', body);
    obs.subscribe({
      next: () => { this.messageService.add({severity:'success', summary: 'User saved', life: 3000}); this.showForm.set(false); this.saving.set(false); this.load(); },
      error: (e) => { this.messageService.add({severity:'error', summary: e?.error?.message || 'Error saving user', life: 4000}); this.saving.set(false); },
    });
  }

  deleteUser(u: CompanyUser): void {
    if (!confirm(`Delete user "${u.fullName}"?`)) return;
    this.api.delete(`/users/${u.companyUserId}`).subscribe({
      next: () => { this.messageService.add({severity:'success', summary: 'User deleted', life: 3000}); this.load(); },
      error: () => this.messageService.add({severity:'error', summary: 'Error deleting user', life: 4000}),
    });
  }

  updateRouting(user: ManagedUser, mode: 'manual' | 'auto', value: boolean): void {
    const nextManual = mode === 'manual' ? value : user.canReceiveManualAssignments;
    const nextAuto = mode === 'auto' ? value : user.canReceiveAutoAssignments;

    this.api.put<CompanyUserRoutingSettings>(`/routing/users/${user.companyUserId}`, {
      canReceiveManualAssignments: nextManual,
      canReceiveAutoAssignments: nextAuto,
    }).subscribe({
      next: (updated) => {
        this.users.update(items => items.map(item => item.companyUserId === user.companyUserId
          ? {
              ...item,
              canReceiveManualAssignments: updated.canReceiveManualAssignments,
              canReceiveAutoAssignments: updated.canReceiveAutoAssignments,
              lastAutoAssignedAtUtc: updated.lastAutoAssignedAtUtc,
            }
          : item));
      },
      error: (e) => {
        this.messageService.add({ severity: 'error', summary: e?.error?.message || 'Error updating routing', life: 4000 });
        this.load();
      },
    });
  }

  formatDate(value: string | null): string {
    return value ? new Date(value).toLocaleString() : '-';
  }
}
