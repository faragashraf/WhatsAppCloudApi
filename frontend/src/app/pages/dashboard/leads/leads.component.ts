import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import {
  LeadDashboardSummary,
  LeadDepartment,
  LeadQueryParams,
  LeadRecord,
  PagedResult,
  RoutingTeam,
  UpsertLeadDepartmentRequest,
} from '../../../core/models';
import { ApiService, PermissionService, TokenService } from '../../../core/services';

@Component({
  selector: 'app-leads',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, DatePipe],
  templateUrl: './leads.component.html',
  styleUrl: './leads.component.scss',
})
export class LeadsComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly perm = inject(PermissionService);
  private readonly token = inject(TokenService);

  readonly loadingSummary = signal(false);
  readonly loadingLeads = signal(false);
  readonly loadingDepartments = signal(false);
  readonly savingDepartment = signal(false);
  readonly creatingDepartment = signal(false);

  readonly summary = signal<LeadDashboardSummary | null>(null);
  readonly leads = signal<LeadRecord[]>([]);
  readonly departments = signal<LeadDepartment[]>([]);
  readonly teams = signal<RoutingTeam[]>([]);

  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly errorMessage = signal('');

  filters = {
    source: '',
    status: '',
    leadDepartmentId: '',
  };

  newDepartment: UpsertLeadDepartmentRequest = {
    departmentKey: '',
    nameAr: '',
    nameEn: '',
    routingTeamId: null,
    isActive: true,
    sortOrder: 0,
  };

  readonly isAdmin = computed(() => this.token.role() === 'Admin');
  readonly totalPages = computed(() => {
    const size = this.pageSize();
    const count = this.totalCount();
    return size > 0 ? Math.max(1, Math.ceil(count / size)) : 1;
  });

  ngOnInit(): void {
    this.reloadAll();
    if (this.isAdmin()) {
      this.loadTeams();
    }
  }

  reloadAll(): void {
    this.loadSummary();
    this.loadDepartments();
    this.loadLeads();
  }

  applyFilters(): void {
    this.page.set(1);
    this.loadLeads();
  }

  clearFilters(): void {
    this.filters = {
      source: '',
      status: '',
      leadDepartmentId: '',
    };
    this.page.set(1);
    this.loadLeads();
  }

  goToPage(page: number): void {
    const clamped = Math.min(Math.max(1, page), this.totalPages());
    if (clamped === this.page()) {
      return;
    }

    this.page.set(clamped);
    this.loadLeads();
  }

  onPageSizeChange(value: string | number): void {
    const parsed = Number(value);
    this.pageSize.set(Number.isFinite(parsed) && parsed > 0 ? parsed : 25);
    this.page.set(1);
    this.loadLeads();
  }

  saveDepartment(department: LeadDepartment): void {
    if (!this.isAdmin()) {
      return;
    }

    const payload: UpsertLeadDepartmentRequest = {
      departmentKey: department.departmentKey.trim(),
      nameAr: department.nameAr.trim(),
      nameEn: department.nameEn.trim(),
      routingTeamId: department.routingTeamId,
      isActive: department.isActive,
      sortOrder: department.sortOrder,
    };

    this.savingDepartment.set(true);
    this.errorMessage.set('');

    this.api.put<LeadDepartment>(`/leads/departments/${department.leadDepartmentId}`, payload).subscribe({
      next: updated => {
        this.savingDepartment.set(false);
        this.departments.update(items => items.map(item =>
          item.leadDepartmentId === updated.leadDepartmentId ? updated : item));
      },
      error: error => {
        this.savingDepartment.set(false);
        this.errorMessage.set(error?.error?.message ?? 'Failed to update department.');
      },
    });
  }

  createDepartment(): void {
    if (!this.isAdmin()) {
      return;
    }

    const payload: UpsertLeadDepartmentRequest = {
      departmentKey: this.newDepartment.departmentKey.trim(),
      nameAr: this.newDepartment.nameAr.trim(),
      nameEn: this.newDepartment.nameEn.trim(),
      routingTeamId: this.newDepartment.routingTeamId,
      isActive: this.newDepartment.isActive,
      sortOrder: this.newDepartment.sortOrder,
    };

    if (!payload.departmentKey || !payload.nameAr || !payload.nameEn) {
      return;
    }

    this.creatingDepartment.set(true);
    this.errorMessage.set('');

    this.api.post<LeadDepartment>('/leads/departments', payload).subscribe({
      next: created => {
        this.creatingDepartment.set(false);
        this.departments.update(items => [...items, created].sort((a, b) => a.sortOrder - b.sortOrder));
        this.newDepartment = {
          departmentKey: '',
          nameAr: '',
          nameEn: '',
          routingTeamId: null,
          isActive: true,
          sortOrder: 0,
        };
      },
      error: error => {
        this.creatingDepartment.set(false);
        this.errorMessage.set(error?.error?.message ?? 'Failed to create department.');
      },
    });
  }

  seedDefaultDepartments(): void {
    if (!this.isAdmin()) {
      return;
    }

    this.api.post<boolean>('/leads/departments/seed-defaults', {}).subscribe({
      next: () => this.loadDepartments(),
      error: error => {
        this.errorMessage.set(error?.error?.message ?? 'Failed to seed default departments.');
      },
    });
  }

  private loadSummary(): void {
    this.loadingSummary.set(true);
    this.api.get<LeadDashboardSummary>('/leads/summary').subscribe({
      next: summary => {
        this.summary.set(summary);
        this.loadingSummary.set(false);
      },
      error: error => {
        this.loadingSummary.set(false);
        this.errorMessage.set(error?.error?.message ?? 'Failed to load lead summary.');
      },
    });
  }

  private loadLeads(): void {
    this.loadingLeads.set(true);
    this.errorMessage.set('');

    const params: LeadQueryParams = {
      page: this.page(),
      pageSize: this.pageSize(),
    };

    if (this.filters.source) {
      params.source = this.filters.source;
    }

    if (this.filters.status) {
      params.status = this.filters.status;
    }

    if (this.filters.leadDepartmentId) {
      params.leadDepartmentId = Number(this.filters.leadDepartmentId);
    }

    this.api.get<PagedResult<LeadRecord>>('/leads', this.toQueryParams(params)).subscribe({
      next: pageResult => {
        this.leads.set(pageResult.items ?? []);
        this.totalCount.set(pageResult.totalCount ?? 0);
        this.loadingLeads.set(false);
      },
      error: error => {
        this.loadingLeads.set(false);
        this.errorMessage.set(error?.error?.message ?? 'Failed to load leads.');
      },
    });
  }

  private loadDepartments(): void {
    this.loadingDepartments.set(true);
    this.api.get<LeadDepartment[]>('/leads/departments').subscribe({
      next: rows => {
        this.departments.set((rows ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder));
        this.loadingDepartments.set(false);
      },
      error: error => {
        this.loadingDepartments.set(false);
        this.errorMessage.set(error?.error?.message ?? 'Failed to load departments.');
      },
    });
  }

  private loadTeams(): void {
    this.api.get<RoutingTeam[]>('/routing/teams').subscribe({
      next: rows => this.teams.set(rows ?? []),
    });
  }

  private toQueryParams(query: LeadQueryParams): Record<string, string | number | boolean> {
    const params: Record<string, string | number | boolean> = {};
    for (const [key, value] of Object.entries(query)) {
      if (value === null || value === undefined || value === '') {
        continue;
      }

      params[key] = value as string | number | boolean;
    }

    return params;
  }
}
