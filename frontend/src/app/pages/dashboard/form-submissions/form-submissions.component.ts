import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import {
  ConversationFlow,
  ConversationFlowFormSubmission,
  ConversationFlowFormSubmissionList,
} from '../../../core/models';
import { ApiService, LanguageService, PermissionService } from '../../../core/services';

type SubmissionFilters = {
  flowId: number | null;
  source: string | null;
  conversationId: number | null;
  contactId: number | null;
};

type SubmissionEntry = {
  key: string;
  value: string;
};

@Component({
  selector: 'app-form-submissions',
  standalone: true,
  imports: [FormsModule, TranslateModule, RouterLink],
  templateUrl: './form-submissions.component.html',
  styleUrl: './form-submissions.component.scss',
})
export class FormSubmissionsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);

  readonly langService = inject(LanguageService);
  readonly perm = inject(PermissionService);

  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly exportingExcel = signal(false);
  readonly flowsLoading = signal(false);
  readonly errorMessage = signal('');
  readonly statusMessage = signal('');

  readonly flows = signal<ConversationFlow[]>([]);
  readonly submissions = signal<ConversationFlowFormSubmission[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly filters = signal<SubmissionFilters>(this.defaultFilters());
  readonly expandedRows = signal<Record<number, boolean>>({});

  readonly pageSizeOptions = [10, 25, 50, 100];
  readonly sourceOptions = ['meta_flow', 'form'];

  private readonly technicalFieldKeys = new Set(['flow_token']);

  readonly totalPages = computed(() => {
    const total = this.totalCount();
    const size = this.pageSize();
    if (size <= 0) {
      return 1;
    }

    return Math.max(1, Math.ceil(total / size));
  });

  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() < this.totalPages());

  readonly pageFrom = computed(() => {
    if (this.totalCount() === 0) {
      return 0;
    }

    return ((this.page() - 1) * this.pageSize()) + 1;
  });

  readonly pageTo = computed(() => {
    if (this.totalCount() === 0) {
      return 0;
    }

    return Math.min(this.totalCount(), this.page() * this.pageSize());
  });

  readonly flowNameById = computed(() => {
    const lookup = new Map<number, string>();
    for (const flow of this.flows()) {
      lookup.set(flow.conversationFlowId, flow.name);
    }
    return lookup;
  });

  readonly stats = computed(() => {
    const items = this.submissions();
    const form = items.filter(item => this.isFormSource(item.source)).length;
    const metaFlow = items.filter(item => this.isMetaFlowSource(item.source)).length;
    const withUserFields = items.filter(item => this.getUserEntries(item).length > 0).length;
    const latestCreatedAtUtc = items[0]?.createdAtUtc ?? null;

    return {
      pageCount: items.length,
      form,
      metaFlow,
      withUserFields,
      latestCreatedAtUtc,
    };
  });

  ngOnInit(): void {
    this.applyDeepLinkFilters();
    this.loadFlows();
    this.loadSubmissions();
  }

  refresh(): void {
    this.loadSubmissions(true);
  }

  async exportExcel(): Promise<void> {
    if (this.exportingExcel()) {
      return;
    }

    this.exportingExcel.set(true);
    this.errorMessage.set('');

    try {
      const items = await this.fetchAllSubmissionsForExport();
      if (items.length === 0) {
        this.statusMessage.set(this.t('formSubmissions.exportEmpty'));
        return;
      }

      const rows = this.buildExportRows(items);
      const xlsx = await import('xlsx');
      const worksheet = xlsx.utils.json_to_sheet(rows);
      const workbook = xlsx.utils.book_new();
      xlsx.utils.book_append_sheet(workbook, worksheet, 'Submissions');

      const stamp = this.buildFileTimestamp();
      xlsx.writeFile(workbook, `flow-form-submissions-${stamp}.xlsx`);

      this.statusMessage.set(this.t('formSubmissions.feedback.exportSuccess', { count: items.length }));
    } catch (error) {
      console.error('Failed to export form submissions.', error);
      this.errorMessage.set(this.t('formSubmissions.feedback.exportError'));
    } finally {
      this.exportingExcel.set(false);
    }
  }

  applyFilters(): void {
    this.page.set(1);
    this.loadSubmissions();
  }

  clearFilters(): void {
    this.filters.set(this.defaultFilters());
    this.page.set(1);
    this.errorMessage.set('');
    this.statusMessage.set('');
    this.loadSubmissions();
  }

  updateFlowFilter(value: string): void {
    const numeric = Number(value);
    this.filters.update(current => ({
      ...current,
      flowId: Number.isFinite(numeric) && numeric > 0 ? numeric : null,
    }));
  }

  updateSourceFilter(value: string): void {
    const source = value.trim().toLowerCase();
    this.filters.update(current => ({
      ...current,
      source: source ? source : null,
    }));
  }

  updateConversationFilter(value: string): void {
    const numeric = Number(value);
    this.filters.update(current => ({
      ...current,
      conversationId: Number.isFinite(numeric) && numeric > 0 ? numeric : null,
    }));
  }

  updateContactFilter(value: string): void {
    const numeric = Number(value);
    this.filters.update(current => ({
      ...current,
      contactId: Number.isFinite(numeric) && numeric > 0 ? numeric : null,
    }));
  }

  updatePageSize(value: string): void {
    const numeric = Number(value);
    this.pageSize.set(Number.isFinite(numeric) && numeric > 0 ? numeric : 25);
    this.page.set(1);
    this.loadSubmissions();
  }

  goToPreviousPage(): void {
    if (!this.hasPreviousPage()) {
      return;
    }

    this.page.update(current => Math.max(1, current - 1));
    this.loadSubmissions();
  }

  goToNextPage(): void {
    if (!this.hasNextPage()) {
      return;
    }

    this.page.update(current => current + 1);
    this.loadSubmissions();
  }

  toggleDetails(submissionId: number): void {
    this.expandedRows.update(current => ({
      ...current,
      [submissionId]: !current[submissionId],
    }));
  }

  isDetailsOpen(submissionId: number): boolean {
    return !!this.expandedRows()[submissionId];
  }

  flowName(flowId: number): string {
    return this.flowNameById().get(flowId) ?? `${this.t('formSubmissions.flowPrefix')} #${flowId}`;
  }

  sourceLabel(source: string): string {
    const normalized = this.normalizeKey(source);
    if (normalized === 'meta_flow') {
      return this.t('formSubmissions.sourceMetaFlow');
    }

    if (normalized === 'form') {
      return this.t('formSubmissions.sourceForm');
    }

    return source || this.t('formSubmissions.unknown');
  }

  isMetaFlowSource(source: string): boolean {
    return this.normalizeKey(source) === 'meta_flow';
  }

  isFormSource(source: string): boolean {
    return this.normalizeKey(source) === 'form';
  }

  formatUtc(utcValue: string | null | undefined): string {
    if (!utcValue) {
      return '-';
    }

    const value = new Date(utcValue);
    if (Number.isNaN(value.getTime())) {
      return utcValue;
    }

    return new Intl.DateTimeFormat(this.langService.currentLang(), {
      year: 'numeric',
      month: 'short',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    }).format(value);
  }

  getUserEntries(item: ConversationFlowFormSubmission): SubmissionEntry[] {
    const values = item.extractedValues ?? {};
    return Object.entries(values)
      .filter(([key]) => !this.isTechnicalFieldKey(key))
      .map(([key, value]) => ({ key, value: this.normalizeDisplayValue(value) }))
      .sort((a, b) => a.key.localeCompare(b.key));
  }

  getTechnicalEntries(item: ConversationFlowFormSubmission): SubmissionEntry[] {
    const values = item.extractedValues ?? {};
    return Object.entries(values)
      .filter(([key]) => this.isTechnicalFieldKey(key))
      .map(([key, value]) => ({ key, value: this.normalizeDisplayValue(value) }))
      .sort((a, b) => a.key.localeCompare(b.key));
  }

  hasTechnicalOnlyValues(item: ConversationFlowFormSubmission): boolean {
    return this.getUserEntries(item).length === 0 && this.getTechnicalEntries(item).length > 0;
  }

  userFieldCount(item: ConversationFlowFormSubmission): number {
    return this.getUserEntries(item).length;
  }

  formatPayloadJson(payloadJson: string | null): string {
    if (!payloadJson || !payloadJson.trim()) {
      return '';
    }

    try {
      return JSON.stringify(JSON.parse(payloadJson), null, 2);
    } catch {
      return payloadJson;
    }
  }

  trackSubmission(_: number, item: ConversationFlowFormSubmission): number {
    return item.conversationFlowFormSubmissionId;
  }

  private loadFlows(): void {
    this.flowsLoading.set(true);

    this.api.get<ConversationFlow[]>('/conversation-flows').subscribe({
      next: flows => {
        this.flowsLoading.set(false);
        this.flows.set(flows ?? []);
      },
      error: () => {
        this.flowsLoading.set(false);
      },
    });
  }

  private loadSubmissions(showRefreshState = false): void {
    if (showRefreshState) {
      this.refreshing.set(true);
    } else {
      this.loading.set(true);
    }

    this.errorMessage.set('');
    this.statusMessage.set('');

    this.api.get<ConversationFlowFormSubmissionList>('/conversation-flows/submissions', this.buildQueryParams()).subscribe({
      next: result => {
        this.loading.set(false);
        this.refreshing.set(false);

        const totalCount = Math.max(0, result?.totalCount ?? 0);
        const items = result?.items ?? [];
        this.totalCount.set(totalCount);
        this.submissions.set(items);
        this.expandedRows.set({});

        const maxPage = Math.max(1, Math.ceil(totalCount / this.pageSize()));
        if (this.page() > maxPage) {
          this.page.set(maxPage);
          this.loadSubmissions();
          return;
        }

        this.statusMessage.set(this.t('formSubmissions.feedback.loaded', { count: items.length }));
      },
      error: error => {
        this.loading.set(false);
        this.refreshing.set(false);
        this.errorMessage.set(error?.error?.message ?? this.t('formSubmissions.feedback.loadError'));
      },
    });
  }

  private buildQueryParams(page = this.page(), pageSize = this.pageSize()): Record<string, string | number | boolean> {
    const params: Record<string, string | number | boolean> = {
      page,
      pageSize,
    };

    const filter = this.filters();
    if (filter.flowId && filter.flowId > 0) {
      params['flowId'] = filter.flowId;
    }

    if (filter.conversationId && filter.conversationId > 0) {
      params['conversationId'] = filter.conversationId;
    }

    if (filter.contactId && filter.contactId > 0) {
      params['contactId'] = filter.contactId;
    }

    if (filter.source?.trim()) {
      params['source'] = filter.source.trim();
    }

    return params;
  }

  private applyDeepLinkFilters(): void {
    const flowIdRaw = this.route.snapshot.queryParamMap.get('flowId');
    const conversationIdRaw = this.route.snapshot.queryParamMap.get('conversationId');
    const contactIdRaw = this.route.snapshot.queryParamMap.get('contactId');
    const sourceRaw = this.route.snapshot.queryParamMap.get('source');
    const pageSizeRaw = this.route.snapshot.queryParamMap.get('pageSize');

    const flowId = this.parsePositiveNumber(flowIdRaw);
    const conversationId = this.parsePositiveNumber(conversationIdRaw);
    const contactId = this.parsePositiveNumber(contactIdRaw);
    const source = sourceRaw?.trim().toLowerCase() || null;
    const pageSize = this.parsePositiveNumber(pageSizeRaw);

    this.filters.set({
      flowId,
      conversationId,
      contactId,
      source: source || null,
    });

    if (pageSize) {
      this.pageSize.set(pageSize);
    }
  }

  private parsePositiveNumber(value: string | null): number | null {
    const numeric = Number(value);
    return Number.isFinite(numeric) && numeric > 0 ? numeric : null;
  }

  private isTechnicalFieldKey(key: string): boolean {
    const normalizedKey = this.normalizeKey(key);
    return this.technicalFieldKeys.has(normalizedKey) || normalizedKey.endsWith('_flow_token');
  }

  private normalizeDisplayValue(value: string): string {
    return value ?? '';
  }

  private normalizeKey(value: string): string {
    return (value || '').trim().toLowerCase();
  }

  private defaultFilters(): SubmissionFilters {
    return {
      flowId: null,
      source: null,
      conversationId: null,
      contactId: null,
    };
  }

  private t(key: string, params?: Record<string, unknown>): string {
    return this.translate.instant(key, params);
  }

  private async fetchAllSubmissionsForExport(): Promise<ConversationFlowFormSubmission[]> {
    const allItems: ConversationFlowFormSubmission[] = [];
    const maxPageSize = 200;
    let currentPage = 1;
    let totalCount = 0;

    do {
      const result = await firstValueFrom(this.api.get<ConversationFlowFormSubmissionList>(
        '/conversation-flows/submissions',
        this.buildQueryParams(currentPage, maxPageSize)));

      totalCount = Math.max(0, result?.totalCount ?? 0);
      const items = result?.items ?? [];
      if (items.length === 0) {
        break;
      }

      allItems.push(...items);
      currentPage += 1;
    } while (allItems.length < totalCount);

    return allItems;
  }

  private buildExportRows(items: ConversationFlowFormSubmission[]): Record<string, string | number>[] {
    const userKeys = new Set<string>();
    const technicalKeys = new Set<string>();

    for (const item of items) {
      const values = item.extractedValues ?? {};
      for (const [key] of Object.entries(values)) {
        if (this.isTechnicalFieldKey(key)) {
          technicalKeys.add(key);
        } else {
          userKeys.add(key);
        }
      }
    }

    const orderedUserKeys = Array.from(userKeys).sort((a, b) => a.localeCompare(b));
    const orderedTechnicalKeys = Array.from(technicalKeys).sort((a, b) => a.localeCompare(b));

    return items.map(item => {
      const row: Record<string, string | number> = {
        SubmissionId: item.conversationFlowFormSubmissionId,
        FlowId: item.conversationFlowId,
        FlowName: this.flowName(item.conversationFlowId),
        SessionId: item.conversationFlowSessionId ?? '',
        ConversationId: item.conversationId ?? '',
        ContactId: item.contactId ?? '',
        Source: this.sourceLabel(item.source),
        SourceRaw: item.source,
        NodeId: item.nodeId,
        InboundMessageType: item.inboundMessageType ?? '',
        MetaMessageId: item.metaMessageId ?? '',
        CreatedAtUtc: item.createdAtUtc,
        CreatedAtLocal: this.formatUtc(item.createdAtUtc),
      };

      const values = item.extractedValues ?? {};
      for (const key of orderedUserKeys) {
        row[`User.${key}`] = values[key] ?? '';
      }

      for (const key of orderedTechnicalKeys) {
        row[`Technical.${key}`] = values[key] ?? '';
      }

      row['PayloadJson'] = this.formatPayloadJson(item.payloadJson);
      return row;
    });
  }

  private buildFileTimestamp(): string {
    const now = new Date();
    const yyyy = now.getFullYear();
    const mm = String(now.getMonth() + 1).padStart(2, '0');
    const dd = String(now.getDate()).padStart(2, '0');
    const hh = String(now.getHours()).padStart(2, '0');
    const min = String(now.getMinutes()).padStart(2, '0');
    return `${yyyy}${mm}${dd}-${hh}${min}`;
  }
}
