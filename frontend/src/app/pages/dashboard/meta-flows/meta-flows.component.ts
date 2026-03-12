import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { catchError, of, forkJoin } from 'rxjs';
import { ApiService, LanguageService, PermissionService } from '../../../core/services';
import { GraphApiEnvelope, MetaFlowSummary } from '../../../core/models';

type MetaFlowCreateForm = {
  name: string;
  categoriesCsv: string;
  endpointUri: string;
  cloneFlowId: string;
};

type MetaFlowEditorForm = {
  id: string;
  name: string;
  categoriesCsv: string;
  endpointUri: string;
  flowJson: string;
};

type FlowAction = 'publish' | 'deprecate' | 'delete';

export type MetaFlowSelection = {
  id: string;
  name?: string;
  status?: string;
};

@Component({
  selector: 'app-meta-flows',
  standalone: true,
  imports: [FormsModule, TranslateModule],
  templateUrl: './meta-flows.component.html',
  styleUrl: './meta-flows.component.scss',
})
export class MetaFlowsComponent implements OnInit, OnChanges {
  @Input() embedded = false;
  @Input() initialFlowId = '';
  @Input() showSelectButton = false;
  @Input() showCloseButton = false;
  @Output() picked = new EventEmitter<MetaFlowSelection>();
  @Output() closed = new EventEmitter<void>();

  private readonly api = inject(ApiService);
  private readonly translate = inject(TranslateService);

  readonly langService = inject(LanguageService);
  readonly perm = inject(PermissionService);

  readonly loading = signal(false);
  readonly creating = signal(false);
  readonly savingMetadata = signal(false);
  readonly uploadingJson = signal(false);
  readonly actionRunning = signal(false);
  readonly errorMessage = signal('');
  readonly statusMessage = signal('');

  readonly flows = signal<MetaFlowSummary[]>([]);
  readonly selectedFlowId = signal('');
  readonly selectedFlowDetails = signal<Record<string, unknown> | null>(null);
  readonly selectedFlowAssets = signal<Record<string, unknown>[]>([]);

  readonly createForm = signal<MetaFlowCreateForm>({
    name: '',
    categoriesCsv: 'OTHER',
    endpointUri: '',
    cloneFlowId: '',
  });

  readonly editorForm = signal<MetaFlowEditorForm>({
    id: '',
    name: '',
    categoriesCsv: '',
    endpointUri: '',
    flowJson: '',
  });

  readonly selectedFlow = computed(() =>
    this.flows().find(item => item.id === this.selectedFlowId()) ?? null);

  readonly selectedFlowStatus = computed(() => {
    const fromDetails = this.readString(this.selectedFlowDetails(), 'status');
    return (fromDetails || this.selectedFlow()?.status || '').toUpperCase();
  });

  readonly selectedFlowValidationErrors = computed(() => {
    const details = this.selectedFlowDetails();
    if (!details || !('validation_errors' in details)) {
      return '';
    }

    return this.prettyJson((details as Record<string, unknown>)['validation_errors']);
  });

  readonly detailsPreview = computed(() => this.prettyJson(this.selectedFlowDetails()));
  readonly assetsPreview = computed(() => this.prettyJson(this.selectedFlowAssets()));

  ngOnInit(): void {
    this.loadFlows();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['initialFlowId']) {
      return;
    }

    const nextId = this.initialFlowId?.trim();
    if (!nextId) {
      return;
    }

    if (this.flows().some(item => item.id === nextId)) {
      this.selectFlow(nextId);
    }
  }

  loadFlows(preferredFlowId?: string): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.api.get<GraphApiEnvelope<MetaFlowSummary>>('/whatsapp/flows').subscribe({
      next: response => {
        const list = Array.isArray(response?.data) ? response.data : [];
        const sorted = [...list].sort((left, right) =>
          (left.name ?? '').localeCompare(right.name ?? '', undefined, { sensitivity: 'base' }));

        this.flows.set(sorted);
        this.loading.set(false);

        const preferred = preferredFlowId || this.selectedFlowId();
        const hasPreferred = preferred && sorted.some(item => item.id === preferred);
        if (hasPreferred) {
          this.selectFlow(preferred);
          return;
        }

        if (sorted.length > 0) {
          this.selectFlow(sorted[0].id);
        } else {
          this.selectedFlowId.set('');
          this.selectedFlowDetails.set(null);
          this.selectedFlowAssets.set([]);
          this.editorForm.set({
            id: '',
            name: '',
            categoriesCsv: '',
            endpointUri: '',
            flowJson: '',
          });
        }
      },
      error: error => {
        this.loading.set(false);
        this.errorMessage.set(error?.error?.message ?? this.t('metaFlows.feedback.loadError'));
      },
    });
  }

  selectFlow(flowId: string): void {
    const flow = this.flows().find(item => item.id === flowId);
    if (!flow) {
      return;
    }

    this.selectedFlowId.set(flow.id);
    this.hydrateEditorForm(flow, null);
    this.refreshSelectedFlowContext(flow.id);
  }

  pickSelectedFlow(): void {
    const selected = this.selectedFlow();
    if (!selected) {
      return;
    }

    this.picked.emit({
      id: selected.id,
      name: selected.name,
      status: selected.status,
    });
  }

  requestClose(): void {
    this.closed.emit();
  }

  updateCreateField<K extends keyof MetaFlowCreateForm>(field: K, value: MetaFlowCreateForm[K]): void {
    this.createForm.update(form => ({
      ...form,
      [field]: value,
    }));
    this.clearFeedback();
  }

  updateEditorField<K extends keyof MetaFlowEditorForm>(field: K, value: MetaFlowEditorForm[K]): void {
    this.editorForm.update(form => ({
      ...form,
      [field]: value,
    }));
    this.clearFeedback();
  }

  createFlow(): void {
    const form = this.createForm();
    const name = form.name.trim();
    if (!name) {
      this.errorMessage.set(this.t('metaFlows.feedback.nameRequired'));
      return;
    }

    this.creating.set(true);
    this.clearFeedback();

    const payload: Record<string, unknown> = {
      name,
      categories: this.parseCategories(form.categoriesCsv),
    };

    const endpointUri = form.endpointUri.trim();
    if (endpointUri) {
      payload['endpointUri'] = endpointUri;
    }

    const cloneFlowId = form.cloneFlowId.trim();
    if (cloneFlowId) {
      payload['cloneFlowId'] = cloneFlowId;
    }

    this.api.post<GraphApiEnvelope<Record<string, unknown>>>('/whatsapp/flows/create', payload).subscribe({
      next: response => {
        this.creating.set(false);
        const createdFlowId = this.readString(response as Record<string, unknown>, 'id')
          || this.readFirstDataId(response);

        this.createForm.set({
          ...form,
          name: '',
          cloneFlowId: '',
        });

        this.statusMessage.set(this.t('metaFlows.feedback.createSuccess'));
        this.loadFlows(createdFlowId ?? undefined);
      },
      error: error => {
        this.creating.set(false);
        this.errorMessage.set(error?.error?.message ?? this.t('metaFlows.feedback.createError'));
      },
    });
  }

  saveMetadata(): void {
    const flowId = this.selectedFlowId();
    if (!flowId) {
      return;
    }

    const form = this.editorForm();
    const payload: Record<string, unknown> = {};

    const name = form.name.trim();
    if (name) {
      payload['name'] = name;
    }

    const categoriesRaw = form.categoriesCsv.trim();
    if (categoriesRaw) {
      const categories = this.parseCategories(categoriesRaw);
      payload['categories'] = categories;
    }

    const endpointUri = form.endpointUri.trim();
    if (endpointUri) {
      payload['endpointUri'] = endpointUri;
    }

    if (Object.keys(payload).length === 0) {
      this.errorMessage.set(this.t('metaFlows.feedback.noMetadataChanges'));
      return;
    }

    this.savingMetadata.set(true);
    this.clearFeedback();

    this.api.post<GraphApiEnvelope<Record<string, unknown>>>(`/whatsapp/flows/${encodeURIComponent(flowId)}/metadata`, payload).subscribe({
      next: () => {
        this.savingMetadata.set(false);
        this.statusMessage.set(this.t('metaFlows.feedback.metadataSaved'));
        this.loadFlows(flowId);
      },
      error: error => {
        this.savingMetadata.set(false);
        this.errorMessage.set(error?.error?.message ?? this.t('metaFlows.feedback.metadataError'));
      },
    });
  }

  uploadFlowJson(): void {
    const flowId = this.selectedFlowId();
    if (!flowId) {
      return;
    }

    const raw = this.editorForm().flowJson.trim();
    if (!raw) {
      this.errorMessage.set(this.t('metaFlows.feedback.flowJsonRequired'));
      return;
    }

    let normalizedJson = raw;
    try {
      normalizedJson = JSON.stringify(JSON.parse(raw) as unknown);
    } catch {
      this.errorMessage.set(this.t('metaFlows.feedback.flowJsonInvalid'));
      return;
    }

    this.uploadingJson.set(true);
    this.clearFeedback();

    this.api.post<GraphApiEnvelope<Record<string, unknown>>>(
      `/whatsapp/flows/${encodeURIComponent(flowId)}/assets/flow-json`,
      { flowJson: normalizedJson }).subscribe({
      next: () => {
        this.uploadingJson.set(false);
        this.statusMessage.set(this.t('metaFlows.feedback.flowJsonUploaded'));
        this.refreshSelectedFlowContext(flowId);
      },
      error: error => {
        this.uploadingJson.set(false);
        this.errorMessage.set(error?.error?.message ?? this.t('metaFlows.feedback.flowJsonUploadError'));
      },
    });
  }

  publishSelected(): void {
    this.runFlowAction('publish');
  }

  deprecateSelected(): void {
    this.runFlowAction('deprecate');
  }

  deleteSelected(): void {
    this.runFlowAction('delete');
  }

  copyValue(value: string, successKey: string): void {
    if (!value) {
      return;
    }

    navigator.clipboard.writeText(value).then(
      () => this.statusMessage.set(this.t(successKey)),
      () => this.errorMessage.set(this.t('metaFlows.feedback.copyFailed')));
  }

  private runFlowAction(action: FlowAction): void {
    const flow = this.selectedFlow();
    if (!flow) {
      return;
    }

    if (action === 'delete') {
      const confirmed = confirm(this.t('metaFlows.deleteConfirm', { name: flow.name ?? flow.id }));
      if (!confirmed) {
        return;
      }
    }

    this.actionRunning.set(true);
    this.clearFeedback();

    const encodedId = encodeURIComponent(flow.id);
    const request$ = action === 'delete'
      ? this.api.delete<GraphApiEnvelope<Record<string, unknown>>>(`/whatsapp/flows/${encodedId}`)
      : this.api.post<GraphApiEnvelope<Record<string, unknown>>>(`/whatsapp/flows/${encodedId}/${action}`);

    request$.subscribe({
      next: () => {
        this.actionRunning.set(false);
        this.statusMessage.set(this.t(`metaFlows.feedback.${action}Success`));
        this.loadFlows(action === 'delete' ? undefined : flow.id);
      },
      error: error => {
        this.actionRunning.set(false);
        this.errorMessage.set(error?.error?.message ?? this.t(`metaFlows.feedback.${action}Error`));
      },
    });
  }

  private refreshSelectedFlowContext(flowId: string): void {
    forkJoin({
      details: this.api.get<GraphApiEnvelope<Record<string, unknown>>>(`/whatsapp/flows/${encodeURIComponent(flowId)}`)
        .pipe(catchError(() => of(null))),
      assets: this.api.get<GraphApiEnvelope<Record<string, unknown>>>(`/whatsapp/flows/${encodeURIComponent(flowId)}/assets`)
        .pipe(catchError(() => of(null))),
    }).subscribe(({ details, assets }) => {
      const detailsObject = details && typeof details === 'object'
        ? (details as Record<string, unknown>)
        : null;

      this.selectedFlowDetails.set(detailsObject);
      this.selectedFlowAssets.set(Array.isArray(assets?.data) ? assets.data as Record<string, unknown>[] : []);
      this.hydrateEditorForm(this.selectedFlow(), detailsObject);
    });
  }

  private hydrateEditorForm(flow: MetaFlowSummary | null, details: Record<string, unknown> | null): void {
    const categories = this.readStringArray(details, 'categories')
      ?? flow?.categories
      ?? [];

    this.editorForm.update(form => ({
      ...form,
      id: flow?.id ?? this.readString(details, 'id') ?? form.id,
      name: this.readString(details, 'name') ?? flow?.name ?? form.name,
      endpointUri: this.readString(details, 'endpoint_uri')
        ?? this.readString(details, 'endpointUri')
        ?? flow?.endpoint_uri
        ?? form.endpointUri,
      categoriesCsv: categories.join(', '),
    }));
  }

  private parseCategories(raw: string): string[] {
    const categories = raw
      .split(',')
      .map(item => item.trim().toUpperCase())
      .filter(item => !!item);

    return categories.length > 0 ? Array.from(new Set(categories)) : ['OTHER'];
  }

  private readFirstDataId(envelope: GraphApiEnvelope<Record<string, unknown>> | null | undefined): string | null {
    if (!envelope || !Array.isArray(envelope.data) || envelope.data.length === 0) {
      return null;
    }

    const first = envelope.data[0];
    if (!first || typeof first !== 'object') {
      return null;
    }

    return this.readString(first as Record<string, unknown>, 'id');
  }

  private readString(source: Record<string, unknown> | null | undefined, key: string): string | null {
    if (!source || !(key in source)) {
      return null;
    }

    const value = source[key];
    return typeof value === 'string' && value.trim() ? value.trim() : null;
  }

  private readStringArray(source: Record<string, unknown> | null | undefined, key: string): string[] | null {
    if (!source || !(key in source)) {
      return null;
    }

    const value = source[key];
    if (!Array.isArray(value)) {
      return null;
    }

    return value
      .filter(item => typeof item === 'string' && item.trim())
      .map(item => String(item).trim());
  }

  private prettyJson(value: unknown): string {
    if (value === null || value === undefined) {
      return '{}';
    }

    try {
      return JSON.stringify(value, null, 2);
    } catch {
      return '{}';
    }
  }

  private clearFeedback(): void {
    this.errorMessage.set('');
    this.statusMessage.set('');
  }

  private t(key: string, params?: Record<string, unknown>): string {
    this.langService.currentLang();
    return this.translate.instant(key, params);
  }
}
