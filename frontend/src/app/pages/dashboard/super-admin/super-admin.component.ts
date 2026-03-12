import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';
import { SuperAdminService, CreateSubscriptionRequest } from '../../../core/services/super-admin.service';
import { TokenService } from '../../../core/services';
import { SuperAdminCompany, SuperAdminCompanyDetail } from '../../../core/models';

@Component({
  selector: 'app-super-admin',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, TableModule, TagModule, TooltipModule,
    DialogModule, InputTextModule, SelectModule, ProgressSpinnerModule, ToastModule, ConfirmDialogModule, RouterLink,
  ],
  providers: [MessageService, ConfirmationService],
  template: `
    <p-toast />
    <p-confirmDialog />

    <div class="p-6 max-w-7xl mx-auto space-y-6">
      <!-- Header -->
      <div class="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <i class="pi pi-shield text-emerald-500"></i>
            Super Admin Panel
          </h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">Manage all platform companies and subscriptions</p>
        </div>
        <div class="flex items-center gap-3">
          <a routerLink="/dashboard/super-admin/logs" pButton icon="pi pi-database" label="System Logs"
            class="!rounded-xl !bg-slate-800 !text-white"></a>
          <span class="text-sm font-medium text-slate-500 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 px-3 py-1.5 rounded-full">
            {{ companies().length }} companies
          </span>
          <button pButton (click)="loadCompanies()" icon="pi pi-refresh"
            class="!bg-emerald-600 !text-white !rounded-xl" [loading]="loading()"></button>
        </div>
      </div>

      <!-- Table -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 shadow-sm overflow-hidden">
        @if (loading() && companies().length === 0) {
          <div class="flex items-center justify-center py-20">
            <p-progressSpinner [style]="{'width':'40px','height':'40px'}" strokeWidth="4" />
          </div>
        } @else {
          <p-table [value]="companies()" [paginator]="true" [rows]="15" [rowHover]="true"
            [globalFilterFields]="['companyName','ownerEmail']"
            styleClass="p-datatable-sm" [scrollable]="true"
            [showCurrentPageReport]="true" currentPageReportTemplate="{first} - {last} of {totalRecords}">
            <ng-template pTemplate="header">
              <tr>
                <th pSortableColumn="companyId" class="!font-semibold">ID <p-sortIcon field="companyId" /></th>
                <th pSortableColumn="companyName" class="!font-semibold">Company <p-sortIcon field="companyName" /></th>
                <th class="!font-semibold">Owner</th>
                <th pSortableColumn="status" class="!font-semibold">Status <p-sortIcon field="status" /></th>
                <th class="!font-semibold">Users</th>
                <th class="!font-semibold">Subscription</th>
                <th class="!font-semibold">Created</th>
                <th class="!font-semibold text-center">Actions</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-c>
              <tr>
                <td class="text-slate-500">#{{ c.companyId }}</td>
                <td>
                  <span class="font-medium text-slate-900 dark:text-white">{{ c.companyName }}</span>
                </td>
                <td class="text-sm text-slate-500 dark:text-slate-400">{{ c.email }}</td>
                <td>
                  <p-tag [severity]="getStatusSeverity(c.status ?? 'UNKNOWN')" [value]="c.status ?? 'UNKNOWN'" [rounded]="true" />
                </td>
                <td class="text-center">{{ c.userCount }}</td>
                <td>
                  @if (c.activeSubscription) {
                    <span class="text-xs font-medium bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400 px-2 py-1 rounded-full">{{ c.activeSubscription.name }}</span>
                  } @else {
                    <span class="text-xs text-slate-400">No plan</span>
                  }
                </td>
                <td class="text-xs text-slate-400">{{ c.createdAt | date:'mediumDate' }}</td>
                <td class="text-center">
                  <div class="flex items-center justify-center gap-1">
                    <button pButton icon="pi pi-eye" [rounded]="true" [text]="true" severity="info" size="small"
                      pTooltip="View Details" (click)="viewCompany(c.companyId)"></button>
                    @if (c.status === 'ACTIVE') {
                      <button pButton icon="pi pi-pause" [rounded]="true" [text]="true" severity="warn" size="small"
                        pTooltip="Suspend" (click)="confirmSuspend(c)"></button>
                    }
                    @if (c.status === 'SUSPENDED') {
                      <button pButton icon="pi pi-play" [rounded]="true" [text]="true" severity="success" size="small"
                        pTooltip="Activate" (click)="activateCompany(c)"></button>
                    }
                    @if (c.status !== 'DELETED') {
                      <button pButton icon="pi pi-trash" [rounded]="true" [text]="true" severity="danger" size="small"
                        pTooltip="Delete" (click)="confirmDelete(c)"></button>
                    }
                  </div>
                </td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr><td colspan="8" class="text-center py-12 text-slate-400">No companies found</td></tr>
            </ng-template>
          </p-table>
        }
      </div>
    </div>

    <!-- Detail Dialog -->
    <p-dialog [(visible)]="detailVisible" [modal]="true" [style]="{width:'600px'}" [draggable]="false"
      header="Company Details" [closable]="true" styleClass="!rounded-2xl">
      @if (detailLoading()) {
        <div class="flex justify-center py-10">
          <p-progressSpinner [style]="{'width':'30px','height':'30px'}" strokeWidth="4" />
        </div>
      } @else if (detail()) {
        <div class="space-y-5">
          <!-- Basic Info -->
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="text-xs font-semibold text-slate-400 uppercase">Company Name</label>
              <input pInputText [(ngModel)]="editName" class="w-full mt-1" />
            </div>
            <div>
              <label class="text-xs font-semibold text-slate-400 uppercase">Status</label>
                  <p-tag [severity]="getStatusSeverity(detail()!.status ?? 'UNKNOWN')" [value]="detail()!.status ?? 'UNKNOWN'" [rounded]="true" class="mt-1" />
            </div>
            <div>
              <label class="text-xs font-semibold text-slate-400 uppercase">Owner</label>
              <p class="text-sm text-slate-700 dark:text-slate-300 mt-1">{{ detail()!.users[0]?.fullName || 'N/A' }}</p>
              <p class="text-xs text-slate-400">{{ detail()!.users[0]?.email || detail()!.email }}</p>
            </div>
            <div>
              <label class="text-xs font-semibold text-slate-400 uppercase">Users</label>
              <p class="text-sm text-slate-700 dark:text-slate-300 mt-1">{{ detail()!.userCount }}</p>
            </div>
          </div>

          <!-- Subscriptions -->
          <div>
            <h3 class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">Subscriptions</h3>
            @if (detail()!.subscriptions && detail()!.subscriptions.length > 0) {
              <div class="space-y-2">
                @for (sub of detail()!.subscriptions; track sub.planName) {
                  <div class="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-700/30 rounded-xl text-sm">
                    <div>
                      <span class="font-medium text-slate-900 dark:text-white">{{ sub.planName }}</span>
                      <span class="text-xs text-slate-400 ms-2">{{ sub.startDate | date:'mediumDate' }} → {{ sub.endDate | date:'mediumDate' }}</span>
                    </div>
                    <p-tag [value]="sub.isActive ? 'Active' : 'Expired'" [severity]="sub.isActive ? 'success' : 'secondary'" [rounded]="true" />
                  </div>
                }
              </div>
            } @else {
              <p class="text-sm text-slate-400">No subscriptions</p>
            }
          </div>

          <!-- Actions -->
          <div class="flex justify-end gap-2 pt-2 border-t border-slate-200 dark:border-slate-700">
            <button pButton label="Save Name" icon="pi pi-save" (click)="saveCompanyName()"
              class="!bg-emerald-600 !text-white !rounded-xl" [loading]="saving()" size="small"></button>
          </div>
        </div>
      }
    </p-dialog>
  `,
})
export class SuperAdminComponent implements OnInit {
  private superAdminService = inject(SuperAdminService);
  private messageService = inject(MessageService);
  private confirmService = inject(ConfirmationService);
  protected tokenService = inject(TokenService);

  companies = signal<SuperAdminCompany[]>([]);
  loading = signal(false);

  detail = signal<SuperAdminCompanyDetail | null>(null);
  detailVisible = false;
  detailLoading = signal(false);
  editName = '';
  saving = signal(false);

  ngOnInit(): void {
    this.loadCompanies();
  }

  loadCompanies(): void {
    this.loading.set(true);
    this.superAdminService.getCompanies().subscribe({
      next: (data) => { this.companies.set(data); this.loading.set(false); },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: 'Failed to load companies', life: 4000 });
      },
    });
  }

  viewCompany(id: number): void {
    this.detailVisible = true;
    this.detailLoading.set(true);
    this.superAdminService.getCompany(id).subscribe({
      next: (data) => { this.detail.set(data); this.editName = data.companyName; this.detailLoading.set(false); },
      error: () => { this.detailLoading.set(false); this.messageService.add({ severity: 'error', summary: 'Failed to load details', life: 4000 }); },
    });
  }

  saveCompanyName(): void {
    const d = this.detail();
    if (!d) return;
    this.saving.set(true);
    this.superAdminService.updateCompany(d.companyId, { companyName: this.editName, email: d.email }).subscribe({
      next: () => {
        this.saving.set(false);
        this.messageService.add({ severity: 'success', summary: 'Company updated', life: 3000 });
        this.loadCompanies();
      },
      error: () => { this.saving.set(false); this.messageService.add({ severity: 'error', summary: 'Update failed', life: 4000 }); },
    });
  }

  confirmSuspend(c: SuperAdminCompany): void {
    this.confirmService.confirm({
      message: `Are you sure you want to suspend "${c.companyName}"?`,
      header: 'Suspend Company',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: '!bg-yellow-500 !text-white !rounded-xl !border-none',
      rejectButtonStyleClass: '!rounded-xl',
      accept: () => {
        this.superAdminService.suspendCompany(c.companyId, 'Suspended by super admin').subscribe({
          next: () => { this.messageService.add({ severity: 'warn', summary: `${c.companyName} suspended`, life: 3000 }); this.loadCompanies(); },
          error: () => this.messageService.add({ severity: 'error', summary: 'Suspend failed', life: 4000 }),
        });
      },
    });
  }

  activateCompany(c: SuperAdminCompany): void {
    this.superAdminService.activateCompany(c.companyId).subscribe({
      next: () => { this.messageService.add({ severity: 'success', summary: `${c.companyName} activated`, life: 3000 }); this.loadCompanies(); },
      error: () => this.messageService.add({ severity: 'error', summary: 'Activation failed', life: 4000 }),
    });
  }

  confirmDelete(c: SuperAdminCompany): void {
    this.confirmService.confirm({
      message: `Are you sure you want to permanently delete "${c.companyName}"? This action cannot be undone.`,
      header: 'Delete Company',
      icon: 'pi pi-trash',
      acceptButtonStyleClass: '!bg-red-500 !text-white !rounded-xl !border-none',
      rejectButtonStyleClass: '!rounded-xl',
      accept: () => {
        this.superAdminService.deleteCompany(c.companyId).subscribe({
          next: () => { this.messageService.add({ severity: 'success', summary: `${c.companyName} deleted`, life: 3000 }); this.loadCompanies(); },
          error: () => this.messageService.add({ severity: 'error', summary: 'Delete failed', life: 4000 }),
        });
      },
    });
  }

  getStatusSeverity(status: string): 'success' | 'warn' | 'danger' | 'secondary' {
    switch (status) {
      case 'ACTIVE': return 'success';
      case 'SUSPENDED': return 'warn';
      case 'DELETED': return 'danger';
      default: return 'secondary';
    }
  }
}
