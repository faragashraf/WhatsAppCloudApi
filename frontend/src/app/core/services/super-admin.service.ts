import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import {
  SuperAdminCompany,
  SuperAdminCompanyDetail,
  SuperAdminApiLogFeed,
  SuperAdminApiLogDetails,
  SuperAdminCompanyUserOption,
} from '../models';

export interface CreateSubscriptionRequest {
  planName: string;
  maxPhoneNumbers: number;
  maxUsersPerCompany: number;
  monthlyMessageLimit: number;
  startDateUtc: string;
  endDateUtc: string;
}

@Injectable({ providedIn: 'root' })
export class SuperAdminService {
  private api = inject(ApiService);

  getCompanies(): Observable<SuperAdminCompany[]> {
    return this.api.get<SuperAdminCompany[]>('/super-admin/companies');
  }

  getCompany(id: number): Observable<SuperAdminCompanyDetail> {
    return this.api.get<SuperAdminCompanyDetail>(`/super-admin/companies/${id}`);
  }

  updateCompany(id: number, data: { companyName: string; email: string; phone?: string }): Observable<any> {
    return this.api.put(`/super-admin/companies/${id}`, data);
  }

  suspendCompany(id: number, reason: string): Observable<any> {
    return this.api.post(`/super-admin/companies/${id}/suspend`, { reason });
  }

  activateCompany(id: number): Observable<any> {
    return this.api.post(`/super-admin/companies/${id}/activate`, {});
  }

  deleteCompany(id: number): Observable<any> {
    return this.api.delete(`/super-admin/companies/${id}`);
  }

  createSubscription(companyId: number, sub: CreateSubscriptionRequest): Observable<any> {
    return this.api.post(`/super-admin/companies/${companyId}/subscriptions`, sub);
  }

  getPlans(): Observable<any[]> {
    return this.api.get<any[]>('/super-admin/plans');
  }

  getApiLogs(query?: {
    take?: number;
    afterId?: number;
    companyId?: number | null;
    companyUserId?: number | null;
  }): Observable<SuperAdminApiLogFeed> {
    const params: Record<string, string | number | boolean> = {};
    if (query?.take) params['take'] = query.take;
    if (query?.afterId && query.afterId > 0) params['afterId'] = query.afterId;
    if (query?.companyId && query.companyId > 0) params['companyId'] = query.companyId;
    if (query?.companyUserId && query.companyUserId > 0) params['companyUserId'] = query.companyUserId;

    return this.api.get<SuperAdminApiLogFeed>('/super-admin/logs/api', params);
  }

  getApiLogDetails(apiLogId: number): Observable<SuperAdminApiLogDetails> {
    return this.api.get<SuperAdminApiLogDetails>(`/super-admin/logs/api/${apiLogId}`);
  }

  getCompanyUsersForLogs(companyId: number): Observable<SuperAdminCompanyUserOption[]> {
    return this.api.get<SuperAdminCompanyUserOption[]>('/super-admin/logs/company-users', { companyId });
  }
}
