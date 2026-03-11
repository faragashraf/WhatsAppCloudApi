import { Component, inject, OnInit, signal, ViewChild } from '@angular/core';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MenuModule, Menu } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService, PermissionService } from '../../../core/services';
import {
  CompanyUserRoutingSettings,
  Contact,
  ContactProfile,
  ContactUpsertRequest,
  PagedResult,
} from '../../../core/models';

@Component({
  selector: 'app-contacts',
  standalone: true,
  imports: [ProgressSpinnerModule, MenuModule, FormsModule, TranslateModule],
  template: `
    <div class="space-y-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'contacts.title' | translate }}</h1>
        <div class="flex items-center gap-2">
          @if (perm.has('contactsImport')) {
          <button
            (click)="showImportModal.set(true)"
            class="flex items-center gap-2 px-4 py-2 bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 rounded-xl text-sm font-medium transition-colors text-slate-700 dark:text-slate-200">
            <i class="pi pi-upload !text-[18px]"></i>
            {{ 'contacts.import' | translate }}
          </button>
          }
          @if (perm.has('contactsCreate')) {
          <button
            (click)="openCreateForm()"
            class="flex items-center gap-2 px-4 py-2 bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white rounded-xl text-sm font-medium transition-colors">
            <i class="pi pi-user-plus !text-[18px]"></i>
            {{ 'contacts.add' | translate }}
          </button>
          }
        </div>
      </div>

      <!-- Search -->
      <div class="relative max-w-md">
        <i class="pi pi-search absolute start-3 top-2.5 !text-[18px] text-slate-400"></i>
        <input
          [(ngModel)]="searchQuery"
          (input)="loadContacts()"
          [placeholder]="'contacts.search' | translate"
          class="w-full ps-10 pe-4 py-2.5 bg-white dark:bg-slate-800 rounded-xl text-sm border border-slate-200 dark:border-slate-700 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
      </div>

      <!-- Table -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 overflow-hidden">
        @if (loading()) {
          <div class="flex justify-center py-12"><p-progressSpinner [style]="{'width':'32px','height':'32px'}" strokeWidth="4" /></div>
        } @else if (contacts().length === 0) {
          <div class="text-center py-12 text-slate-400">
            <i class="pi pi-users !text-[48px] mb-2"></i>
            <p>{{ 'contacts.noContacts' | translate }}</p>
          </div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-slate-200 dark:border-slate-700/50 text-start">
                  <th class="px-4 py-3 text-start font-semibold text-slate-500 dark:text-slate-400">{{ 'contacts.name' | translate }}</th>
                  <th class="px-4 py-3 text-start font-semibold text-slate-500 dark:text-slate-400">{{ 'contacts.phone' | translate }}</th>
                  <th class="px-4 py-3 text-start font-semibold text-slate-500 dark:text-slate-400">{{ 'contacts.email' | translate }}</th>
                  <th class="px-4 py-3 text-start font-semibold text-slate-500 dark:text-slate-400">{{ 'contacts.tags' | translate }}</th>
                  <th class="px-4 py-3 text-start font-semibold text-slate-500 dark:text-slate-400">Owner</th>
                  <th class="px-4 py-3 text-start font-semibold text-slate-500 dark:text-slate-400">Last seen</th>
                  <th class="px-4 py-3 text-start font-semibold text-slate-500 dark:text-slate-400">{{ 'contacts.source' | translate }}</th>
                  <th class="px-4 py-3"></th>
                </tr>
              </thead>
              <tbody>
                @for (contact of contacts(); track contact.contactId) {
                  <tr class="border-b border-slate-100 dark:border-slate-700/30 hover:bg-slate-50 dark:hover:bg-slate-700/20 transition-colors">
                    <td class="px-4 py-3 font-medium text-slate-900 dark:text-white">{{ contact.name }}</td>
                    <td class="px-4 py-3 text-slate-600 dark:text-slate-300 dir-ltr">{{ contact.phoneNumber }}</td>
                    <td class="px-4 py-3 text-slate-600 dark:text-slate-300">{{ contact.email || '-' }}</td>
                    <td class="px-4 py-3">
                      @if (contact.tags) {
                        @for (tag of contact.tags.split(','); track tag) {
                          <span class="inline-block px-2 py-0.5 bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-300 text-xs rounded-full me-1">{{ tag.trim() }}</span>
                        }
                      }
                    </td>
                    <td class="px-4 py-3 text-slate-600 dark:text-slate-300">
                      {{ contact.ownerUser?.fullName || 'Unowned' }}
                    </td>
                    <td class="px-4 py-3 text-slate-500 text-xs">
                      {{ formatDate(contact.lastSeenAtUtc || contact.updatedAtUtc || contact.createdAtUtc) }}
                    </td>
                    <td class="px-4 py-3 text-slate-500 text-xs">{{ contact.source || '-' }}</td>
                    <td class="px-4 py-3">
                      <button (click)="openRowMenu($event, contact)" class="p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors">
                        <i class="pi pi-ellipsis-v !text-[18px] text-slate-400"></i>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Pagination -->
          <div class="flex items-center justify-between px-4 py-3 border-t border-slate-200 dark:border-slate-700/50">
            <span class="text-sm text-slate-500">{{ 'common.total' | translate }}: {{ totalCount() }}</span>
            <div class="flex items-center gap-2">
              <button (click)="prevPage()" [disabled]="page() <= 1" class="p-1.5 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 disabled:opacity-30 transition-colors">
                <i class="pi pi-chevron-left"></i>
              </button>
              <span class="text-sm text-slate-600 dark:text-slate-300">{{ page() }}</span>
              <button (click)="nextPage()" [disabled]="!hasNext()" class="p-1.5 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 disabled:opacity-30 transition-colors">
                <i class="pi pi-chevron-right"></i>
              </button>
            </div>
          </div>
        }
      </div>

      <!-- Create/Edit Modal -->
      @if (showForm()) {
        <div class="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4" (click)="showForm.set(false)">
          <div class="bg-white dark:bg-slate-800 rounded-2xl w-full max-w-lg p-6 space-y-4" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-bold text-slate-900 dark:text-white">
              {{ (editingContact() ? 'contacts.edit' : 'contacts.add') | translate }}
            </h3>
            <div class="space-y-3">
              <input [(ngModel)]="formData.name" [placeholder]="'contacts.name' | translate"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              <input [(ngModel)]="formData.phoneNumber" [placeholder]="'contacts.phone' | translate" dir="ltr"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              <input [(ngModel)]="formData.email" [placeholder]="'contacts.email' | translate"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              <input [(ngModel)]="formData.tags" [placeholder]="'contacts.tags' | translate"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              <textarea [(ngModel)]="formData.notes" [placeholder]="'contacts.notes' | translate" rows="3"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white resize-none"></textarea>
            </div>
            <div class="flex justify-end gap-2 pt-2">
              <button (click)="showForm.set(false)" class="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-xl transition-colors">
                {{ 'common.cancel' | translate }}
              </button>
              <button (click)="saveContact()" class="px-4 py-2 text-sm bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white rounded-xl transition-colors">
                {{ 'common.save' | translate }}
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Import Modal -->
      @if (showImportModal()) {
        <div class="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4" (click)="showImportModal.set(false)">
          <div class="bg-white dark:bg-slate-800 rounded-2xl w-full max-w-md p-6 space-y-4" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-bold text-slate-900 dark:text-white">{{ 'contacts.import' | translate }}</h3>
            <p class="text-sm text-slate-500">{{ 'contacts.importHint' | translate }}</p>
            <input type="file" accept=".csv" (change)="onFileSelected($event)"
              class="w-full text-sm text-slate-600 file:bg-emerald-500 file:text-white file:rounded-lg file:px-4 file:py-2 file:border-0 file:text-sm file:me-3" />
            <div class="flex justify-end gap-2 pt-2">
              <button (click)="showImportModal.set(false)" class="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-xl transition-colors">
                {{ 'common.cancel' | translate }}
              </button>
              <button (click)="importContacts()" [disabled]="!importFile" class="px-4 py-2 text-sm bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white rounded-xl transition-colors disabled:opacity-50">
                {{ 'contacts.import' | translate }}
              </button>
            </div>
          </div>
        </div>
      }

      @if (showProfile()) {
        <div class="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4" (click)="closeProfile()">
          <div class="bg-white dark:bg-slate-800 rounded-2xl w-full max-w-4xl max-h-[90vh] overflow-y-auto p-6 space-y-6" (click)="$event.stopPropagation()">
            @if (profileLoading()) {
              <div class="flex justify-center py-16"><p-progressSpinner [style]="{'width':'32px','height':'32px'}" strokeWidth="4" /></div>
            } @else if (selectedProfile(); as profile) {
              <div class="flex flex-col md:flex-row md:items-start md:justify-between gap-4">
                <div>
                  <h3 class="text-xl font-bold text-slate-900 dark:text-white">{{ profile.name }}</h3>
                  <p class="text-sm text-slate-500 dark:text-slate-400 mt-1 dir-ltr">{{ profile.phoneNumber }}</p>
                  @if (profile.email) {
                    <p class="text-sm text-slate-500 dark:text-slate-400">{{ profile.email }}</p>
                  }
                </div>
                <button (click)="closeProfile()" class="px-3 py-2 text-sm rounded-xl hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-500">
                  Close
                </button>
              </div>

              <div class="grid md:grid-cols-3 gap-4">
                <div class="rounded-2xl border border-slate-200 dark:border-slate-700/50 p-4">
                  <div class="text-xs uppercase tracking-wide text-slate-400 mb-2">Owner</div>
                  <div class="font-semibold text-slate-900 dark:text-white">{{ profile.owner?.fullName || 'Unowned' }}</div>
                  <div class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ profile.owner?.email || 'No active owner assigned' }}</div>
                  @if (perm.isAdmin) {
                    <div class="mt-4 space-y-2">
                      <select [(ngModel)]="selectedOwnerId"
                        class="w-full px-3 py-2 rounded-xl bg-slate-100 dark:bg-slate-700 text-sm border-0 text-slate-900 dark:text-white">
                        <option [ngValue]="null">Unowned</option>
                        @for (user of ownerOptions(); track user.companyUserId) {
                          <option [ngValue]="user.companyUserId">{{ user.fullName }}</option>
                        }
                      </select>
                      <button (click)="saveOwner()" class="w-full px-3 py-2 rounded-xl bg-emerald-600 text-white text-sm hover:bg-emerald-700 transition-colors">
                        Save owner
                      </button>
                    </div>
                  }
                </div>

                <div class="rounded-2xl border border-slate-200 dark:border-slate-700/50 p-4">
                  <div class="text-xs uppercase tracking-wide text-slate-400 mb-2">Activity</div>
                  <div class="space-y-2 text-sm">
                    <div class="flex items-center justify-between gap-3">
                      <span class="text-slate-500">First seen</span>
                      <span class="text-slate-900 dark:text-white">{{ formatDate(profile.firstSeenAtUtc) }}</span>
                    </div>
                    <div class="flex items-center justify-between gap-3">
                      <span class="text-slate-500">Last inbound</span>
                      <span class="text-slate-900 dark:text-white">{{ formatDate(profile.lastInboundMessageAtUtc) }}</span>
                    </div>
                    <div class="flex items-center justify-between gap-3">
                      <span class="text-slate-500">Last outbound</span>
                      <span class="text-slate-900 dark:text-white">{{ formatDate(profile.lastOutboundMessageAtUtc) }}</span>
                    </div>
                    <div class="flex items-center justify-between gap-3">
                      <span class="text-slate-500">Last seen</span>
                      <span class="text-slate-900 dark:text-white">{{ formatDate(profile.lastSeenAtUtc) }}</span>
                    </div>
                  </div>
                </div>

                <div class="rounded-2xl border border-slate-200 dark:border-slate-700/50 p-4">
                  <div class="text-xs uppercase tracking-wide text-slate-400 mb-2">Summary</div>
                  <div class="space-y-2 text-sm">
                    <div class="flex items-center justify-between gap-3">
                      <span class="text-slate-500">Conversations</span>
                      <span class="text-slate-900 dark:text-white">{{ profile.conversationCount }}</span>
                    </div>
                    <div class="flex items-center justify-between gap-3">
                      <span class="text-slate-500">Outbound messages</span>
                      <span class="text-slate-900 dark:text-white">{{ profile.messageCount }}</span>
                    </div>
                    <div class="flex items-center justify-between gap-3">
                      <span class="text-slate-500">Source</span>
                      <span class="text-slate-900 dark:text-white">{{ profile.source || '-' }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <div class="grid lg:grid-cols-2 gap-6">
                <div class="rounded-2xl border border-slate-200 dark:border-slate-700/50 p-4">
                  <h4 class="font-semibold text-slate-900 dark:text-white mb-3">Recent conversations</h4>
                  @if (profile.recentConversations.length === 0) {
                    <p class="text-sm text-slate-500 dark:text-slate-400">No conversations yet.</p>
                  } @else {
                    <div class="space-y-3">
                      @for (conversation of profile.recentConversations; track conversation.conversationId) {
                        <div class="rounded-xl bg-slate-50 dark:bg-slate-700/30 px-4 py-3">
                          <div class="flex items-center justify-between gap-3">
                            <div class="font-medium text-slate-900 dark:text-white">{{ conversation.contactName || conversation.contactNumber }}</div>
                            <div class="text-xs text-slate-500">{{ formatDate(conversation.lastMessageAtUtc) }}</div>
                          </div>
                          <div class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ conversation.lastMessageContent || 'No preview' }}</div>
                          <div class="flex flex-wrap gap-3 mt-2 text-xs text-slate-500 dark:text-slate-400">
                            <span>Status: {{ conversation.status || 'OPEN' }}</span>
                            <span>Unread: {{ conversation.unreadCount }}</span>
                            <span>Assignee: {{ conversation.assignedUserName || 'Unassigned' }}</span>
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>

                <div class="rounded-2xl border border-slate-200 dark:border-slate-700/50 p-4">
                  <h4 class="font-semibold text-slate-900 dark:text-white mb-3">Assignment history</h4>
                  @if (profile.assignmentHistory.length === 0) {
                    <p class="text-sm text-slate-500 dark:text-slate-400">No assignment changes recorded yet.</p>
                  } @else {
                    <div class="space-y-3">
                      @for (entry of profile.assignmentHistory; track entry.conversationAssignmentHistoryId) {
                        <div class="rounded-xl bg-slate-50 dark:bg-slate-700/30 px-4 py-3">
                          <div class="flex items-center justify-between gap-3">
                            <div class="font-medium text-slate-900 dark:text-white">{{ entry.reason }}</div>
                            <div class="text-xs text-slate-500">{{ formatDate(entry.changedAtUtc) }}</div>
                          </div>
                          <div class="text-sm text-slate-500 dark:text-slate-400 mt-1">
                            Assignee: {{ entry.previousAssignedUserName || 'Unassigned' }} -> {{ entry.newAssignedUserName || 'Unassigned' }}
                          </div>
                          <div class="text-sm text-slate-500 dark:text-slate-400">
                            Owner: {{ entry.previousOwnerUserName || 'Unowned' }} -> {{ entry.newOwnerUserName || 'Unowned' }}
                          </div>
                          <div class="text-xs text-slate-400 mt-2">
                            {{ entry.assignmentMode }} by {{ entry.changedByUserName || 'System' }}
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      }

      <p-menu #rowMenu [model]="rowMenuItems" [popup]="true" />
    </div>
  `,
})
export class ContactsComponent implements OnInit {
  private api = inject(ApiService);
  readonly perm = inject(PermissionService);

  loading = signal(true);
  contacts = signal<Contact[]>([]);
  totalCount = signal(0);
  hasNext = signal(false);
  page = signal(1);
  searchQuery = '';

  showForm = signal(false);
  showImportModal = signal(false);
  showProfile = signal(false);
  profileLoading = signal(false);
  editingContact = signal<Contact | null>(null);
  selectedProfile = signal<ContactProfile | null>(null);
  ownerOptions = signal<CompanyUserRoutingSettings[]>([]);
  selectedOwnerId: number | null = null;
  formData: ContactUpsertRequest = { name: '', phoneNumber: '' };
  importFile: File | null = null;

  @ViewChild('rowMenu') rowMenu!: Menu;
  rowMenuItems: MenuItem[] = [];

  ngOnInit(): void {
    this.loadContacts();
  }

  loadContacts(): void {
    const params: Record<string, string> = { page: String(this.page()), pageSize: '20' };
    if (this.searchQuery) params['search'] = this.searchQuery;

    this.api.get<PagedResult<Contact>>('/contacts', params).subscribe({
      next: (r) => {
        this.contacts.set(r?.items ?? []);
        this.totalCount.set(r?.totalCount ?? 0);
        this.hasNext.set(r?.hasNext ?? false);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openRowMenu(event: Event, contact: Contact): void {
    this.rowMenuItems = [{ label: 'Profile', icon: 'pi pi-id-card', command: () => this.openProfile(contact) }];
    if (this.perm.has('contactsEdit')) {
      this.rowMenuItems.push({ label: 'Edit', icon: 'pi pi-pencil', command: () => this.editContact(contact) });
    }
    if (this.perm.has('contactsDelete')) {
      this.rowMenuItems.push({ label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteContact(contact) });
    }
    if (this.rowMenuItems.length === 0) return;
    this.rowMenu.toggle(event);
  }

  openCreateForm(): void {
    this.editingContact.set(null);
    this.formData = { name: '', phoneNumber: '' };
    this.showForm.set(true);
  }

  editContact(c: Contact): void {
    this.editingContact.set(c);
    this.formData = { name: c.name, phoneNumber: c.phoneNumber, email: c.email ?? undefined, tags: c.tags ?? undefined, notes: c.notes ?? undefined };
    this.showForm.set(true);
  }

  saveContact(): void {
    const editing = this.editingContact();
    const obs$ = editing
      ? this.api.put(`/contacts/${editing.contactId}`, this.formData)
      : this.api.post('/contacts', this.formData);

    obs$.subscribe({
      next: () => { this.showForm.set(false); this.loadContacts(); },
    });
  }

  deleteContact(c: Contact): void {
    if (!confirm('Delete this contact?')) return;
    this.api.delete(`/contacts/${c.contactId}`).subscribe({ next: () => this.loadContacts() });
  }

  openProfile(contact: Contact): void {
    this.showProfile.set(true);
    this.profileLoading.set(true);
    this.selectedProfile.set(null);

    if (this.perm.isAdmin && this.ownerOptions().length === 0) {
      this.api.get<CompanyUserRoutingSettings[]>('/routing/users').subscribe({
        next: (users) => this.ownerOptions.set(users.filter(user => user.isActive && user.canReceiveManualAssignments)),
      });
    }

    this.api.get<ContactProfile>(`/contacts/${contact.contactId}/profile`).subscribe({
      next: (profile) => {
        this.selectedProfile.set(profile);
        this.selectedOwnerId = profile.owner?.companyUserId ?? null;
        this.profileLoading.set(false);
      },
      error: () => this.profileLoading.set(false),
    });
  }

  closeProfile(): void {
    this.showProfile.set(false);
    this.profileLoading.set(false);
    this.selectedProfile.set(null);
    this.selectedOwnerId = null;
  }

  saveOwner(): void {
    const profile = this.selectedProfile();
    if (!profile || !this.perm.isAdmin) return;

    this.api.put(`/contacts/${profile.contactId}/owner`, { userId: this.selectedOwnerId }).subscribe({
      next: () => {
        this.profileLoading.set(true);
        this.api.get<ContactProfile>(`/contacts/${profile.contactId}/profile`).subscribe({
          next: (nextProfile) => {
            this.selectedProfile.set(nextProfile);
            this.selectedOwnerId = nextProfile.owner?.companyUserId ?? null;
            this.profileLoading.set(false);
          },
          error: () => this.profileLoading.set(false),
        });
        this.loadContacts();
      },
    });
  }

  onFileSelected(event: Event): void {
    this.importFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  importContacts(): void {
    if (!this.importFile) return;
    const reader = new FileReader();
    reader.onload = () => {
      const base64 = (reader.result as string).split(',')[1];
      this.api.post('/contacts/import', { csvBase64: base64, skipDuplicates: true }).subscribe({
        next: () => { this.showImportModal.set(false); this.loadContacts(); },
      });
    };
    reader.readAsDataURL(this.importFile);
  }

  prevPage(): void { if (this.page() > 1) { this.page.set(this.page() - 1); this.loadContacts(); } }
  nextPage(): void { if (this.hasNext()) { this.page.set(this.page() + 1); this.loadContacts(); } }

  formatDate(value: string | null | undefined): string {
    return value ? new Date(value).toLocaleString() : '-';
  }
}

