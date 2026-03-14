import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MultiSelectModule } from 'primeng/multiselect';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { catchError, of, forkJoin } from 'rxjs';
import { ApiService, LanguageService, PermissionService } from '../../../core/services';
import { GraphApiEnvelope, MetaFlowJsonAssetContent, MetaFlowSummary } from '../../../core/models';
import { StructuredDataViewerComponent } from '../../../shared/components/structured-data-viewer/structured-data-viewer.component';

type MetaFlowCreateForm = {
  name: string;
  categories: string[];
  endpointUri: string;
  cloneFlowId: string;
};

type MetaFlowEditorForm = {
  id: string;
  name: string;
  categories: string[];
  endpointUri: string;
  flowJson: string;
};

type MetaFlowBuilderFieldType = 'text' | 'email' | 'phone' | 'checkbox';

type MetaFlowBuilderField = {
  id: string;
  key: string;
  label: string;
  type: MetaFlowBuilderFieldType;
  required: boolean;
  checkboxLabel: string;
};

type MetaFlowBuilderForm = {
  screenId: string;
  title: string;
  heading: string;
  subheading: string;
  submitLabel: string;
  fields: MetaFlowBuilderField[];
};

type CategoryOption = {
  label: string;
  value: string;
};

type FlowAction = 'publish' | 'deprecate' | 'delete';

export type MetaFlowSelection = {
  id: string;
  name?: string;
  status?: string;
  firstScreenId?: string;
};

@Component({
  selector: 'app-meta-flows',
  standalone: true,
  imports: [FormsModule, MultiSelectModule, TranslateModule, StructuredDataViewerComponent],
  templateUrl: './meta-flows.component.html',
  styleUrl: './meta-flows.component.scss',
})
export class MetaFlowsComponent implements OnInit, OnChanges {
  private readonly supportedCategories = [
    'OTHER',
    'SIGN_UP',
    'LEAD_GENERATION',
    'CONTACT_US',
    'CUSTOMER_SUPPORT',
    'SURVEY',
    'APPOINTMENT_BOOKING',
  ] as const;

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
    categories: ['OTHER'],
    endpointUri: '',
    cloneFlowId: '',
  });

  readonly editorForm = signal<MetaFlowEditorForm>({
    id: '',
    name: '',
    categories: [],
    endpointUri: '',
    flowJson: '',
  });

  readonly categoryOptions = computed<CategoryOption[]>(() => {
    this.langService.currentLang();
    return this.supportedCategories.map(value => ({
      value,
      label: this.t(`metaFlows.categories.${value}`),
    }));
  });

  readonly builderForm = signal<MetaFlowBuilderForm>(this.createDefaultBuilderForm());

  readonly builderWarnings = computed(() => {
    this.langService.currentLang();
    return this.validateBuilderForm(this.builderForm());
  });

  readonly selectedFlow = computed(() =>
    this.flows().find(item => item.id === this.selectedFlowId()) ?? null);

  readonly selectedFlowStatus = computed(() => {
    const fromDetails = this.readString(this.selectedFlowDetails(), 'status');
    return (fromDetails || this.selectedFlow()?.status || '').toUpperCase();
  });

  readonly canDeleteSelectedFlow = computed(() =>
    this.selectedFlowStatus() !== 'PUBLISHED');

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
            categories: [],
            endpointUri: '',
            flowJson: '',
          });
          this.builderForm.set(this.createDefaultBuilderForm());
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

    const firstScreenId = this.resolveFirstScreenIdFromFlowJson(this.editorForm().flowJson);
    this.picked.emit({
      id: selected.id,
      name: selected.name,
      status: selected.status,
      firstScreenId: firstScreenId ?? undefined,
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

  updateBuilderFormField<K extends keyof MetaFlowBuilderForm>(field: K, value: MetaFlowBuilderForm[K]): void {
    this.builderForm.update(form => ({
      ...form,
      [field]: value,
    }));
    this.clearFeedback();
  }

  updateBuilderField<K extends keyof MetaFlowBuilderField>(fieldIndex: number, field: K, value: MetaFlowBuilderField[K]): void {
    this.builderForm.update(form => {
      const fields = [...form.fields];
      const target = fields[fieldIndex];
      if (!target) {
        return form;
      }

      const nextValue = field === 'key'
        ? this.sanitizeVariableName(String(value))
        : value;

      fields[fieldIndex] = {
        ...target,
        [field]: nextValue,
      } as MetaFlowBuilderField;

      return {
        ...form,
        fields,
      };
    });
    this.clearFeedback();
  }

  addBuilderField(type: MetaFlowBuilderFieldType): void {
    this.builderForm.update(form => ({
      ...form,
      fields: [...form.fields, this.createBuilderField(type, form.fields.length + 1)],
    }));
    this.clearFeedback();
  }

  moveBuilderField(fieldIndex: number, direction: 'up' | 'down'): void {
    this.builderForm.update(form => {
      const fields = [...form.fields];
      const targetIndex = direction === 'up' ? fieldIndex - 1 : fieldIndex + 1;
      if (fieldIndex < 0 || fieldIndex >= fields.length || targetIndex < 0 || targetIndex >= fields.length) {
        return form;
      }

      const current = fields[fieldIndex];
      fields[fieldIndex] = fields[targetIndex];
      fields[targetIndex] = current;

      return {
        ...form,
        fields,
      };
    });
  }

  removeBuilderField(fieldIndex: number): void {
    this.builderForm.update(form => ({
      ...form,
      fields: form.fields.filter((_, index) => index !== fieldIndex),
    }));
    this.clearFeedback();
  }

  resetBuilder(): void {
    this.builderForm.set(this.createDefaultBuilderForm(this.selectedFlow()?.name ?? null));
    this.applyBuilderToFlowJson(false);
    this.statusMessage.set(this.t('metaFlows.feedback.builderReset'));
    this.errorMessage.set('');
  }

  applyBuilderToFlowJson(withFeedback = true): void {
    const warnings = this.validateBuilderForm(this.builderForm());
    if (warnings.length > 0) {
      this.errorMessage.set(warnings[0]);
      return;
    }

    const generated = this.buildFlowJsonFromBuilder(this.builderForm());
    this.editorForm.update(form => ({
      ...form,
      flowJson: generated,
    }));

    if (withFeedback) {
      this.statusMessage.set(this.t('metaFlows.feedback.builderApplied'));
      this.errorMessage.set('');
    }
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
      categories: this.normalizeCategories(form.categories),
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

    if (form.categories.length > 0) {
      payload['categories'] = this.normalizeCategories(form.categories);
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

    if (action === 'delete' && !this.canDeleteSelectedFlow()) {
      this.errorMessage.set(this.t('metaFlows.feedback.deletePublishedNotAllowed'));
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
      flowJsonAsset: this.api.get<MetaFlowJsonAssetContent>(`/whatsapp/flows/${encodeURIComponent(flowId)}/assets/flow-json`)
        .pipe(catchError(() => of(null))),
    }).subscribe(({ details, assets, flowJsonAsset }) => {
      const detailsObject = details && typeof details === 'object'
        ? (details as Record<string, unknown>)
        : null;

      this.selectedFlowDetails.set(detailsObject);
      this.selectedFlowAssets.set(Array.isArray(assets?.data) ? assets.data as Record<string, unknown>[] : []);
      this.hydrateEditorForm(this.selectedFlow(), detailsObject);
      this.hydrateFlowJsonEditor(flowJsonAsset);
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
      categories: this.normalizeCategories(categories),
    }));
  }

  private hydrateFlowJsonEditor(asset: MetaFlowJsonAssetContent | null): void {
    const remoteFlowJson = asset?.flowJson?.trim() ?? '';
    if (remoteFlowJson) {
      const normalized = this.normalizeJsonText(remoteFlowJson);
      this.editorForm.update(form => ({
        ...form,
        flowJson: normalized,
      }));
      this.hydrateBuilderFromFlowJson(normalized);
      return;
    }

    const existing = this.editorForm().flowJson.trim();
    if (existing) {
      this.hydrateBuilderFromFlowJson(existing);
      return;
    }

    this.builderForm.set(this.createDefaultBuilderForm(this.selectedFlow()?.name ?? null));
    this.applyBuilderToFlowJson(false);
  }

  private createDefaultBuilderForm(flowName?: string | null): MetaFlowBuilderForm {
    const normalizedName = this.sanitizeVariableName(flowName || 'lead_capture_form').toUpperCase();
    const title = (flowName || this.t('metaFlows.formBuilder.defaults.screenTitle')).trim();

    return {
      screenId: normalizedName || 'LEAD_CAPTURE_FORM',
      title,
      heading: title,
      subheading: this.t('metaFlows.formBuilder.defaults.subheading'),
      submitLabel: this.t('metaFlows.formBuilder.defaults.submitLabel'),
      fields: [
        {
          id: `builder_full_name_${Date.now()}_1`,
          key: 'full_name',
          label: this.t('metaFlows.formBuilder.defaults.fullName'),
          type: 'text',
          required: true,
          checkboxLabel: '',
        },
        {
          id: `builder_phone_number_${Date.now()}_2`,
          key: 'phone_number',
          label: this.t('metaFlows.formBuilder.defaults.phoneNumber'),
          type: 'phone',
          required: true,
          checkboxLabel: '',
        },
        {
          id: `builder_has_whatsapp_${Date.now()}_3`,
          key: 'has_whatsapp',
          label: this.t('metaFlows.formBuilder.defaults.hasWhatsapp'),
          type: 'checkbox',
          required: false,
          checkboxLabel: this.t('metaFlows.formBuilder.defaults.hasWhatsapp'),
        },
        {
          id: `builder_email_${Date.now()}_4`,
          key: 'email',
          label: this.t('metaFlows.formBuilder.defaults.email'),
          type: 'email',
          required: false,
          checkboxLabel: '',
        },
      ],
    };
  }

  private createBuilderField(type: MetaFlowBuilderFieldType, position: number): MetaFlowBuilderField {
    const defaultKey = this.sanitizeVariableName(`${type}_${position}`);
    const defaultLabelKey = `metaFlows.formBuilder.fieldTypes.${type}`;

    return {
      id: `builder_${type}_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`,
      key: defaultKey || `field_${position}`,
      label: this.t(defaultLabelKey),
      type,
      required: false,
      checkboxLabel: type === 'checkbox' ? this.t('metaFlows.formBuilder.defaults.checkboxLabel') : '',
    };
  }

  private validateBuilderForm(form: MetaFlowBuilderForm): string[] {
    const warnings: string[] = [];
    const seenKeys = new Set<string>();

    if (form.fields.length === 0) {
      warnings.push(this.t('metaFlows.formBuilder.warnings.noFields'));
      return warnings;
    }

    form.fields.forEach((field, index) => {
      const key = this.sanitizeVariableName(field.key);
      const label = field.label.trim();
      const checkboxLabel = field.checkboxLabel.trim();
      const fieldNumber = index + 1;

      if (!key) {
        warnings.push(this.t('metaFlows.formBuilder.warnings.keyRequired', { index: fieldNumber }));
      } else if (seenKeys.has(key)) {
        warnings.push(this.t('metaFlows.formBuilder.warnings.duplicateKey', { key }));
      } else {
        seenKeys.add(key);
      }

      if (!label) {
        warnings.push(this.t('metaFlows.formBuilder.warnings.labelRequired', { index: fieldNumber }));
      }

      if (field.type === 'checkbox' && !checkboxLabel) {
        warnings.push(this.t('metaFlows.formBuilder.warnings.checkboxLabelRequired', { index: fieldNumber }));
      }
    });

    return warnings;
  }

  private buildFlowJsonFromBuilder(form: MetaFlowBuilderForm): string {
    const title = form.title.trim() || this.t('metaFlows.formBuilder.defaults.screenTitle');
    const heading = form.heading.trim() || title;
    const subheading = form.subheading.trim();
    const submitLabel = form.submitLabel.trim() || this.t('metaFlows.formBuilder.defaults.submitLabel');
    const screenId = this.sanitizeScreenId(form.screenId);
    const formName = `${this.sanitizeVariableName(screenId.toLowerCase()) || 'lead_capture'}_form`;
    const completionPayload = this.buildCompletionPayload(form.fields);

    const inputChildren = form.fields.map(field => {
      const key = this.sanitizeVariableName(field.key);
      const label = field.label.trim() || key;

      if (field.type === 'checkbox') {
        return {
          type: 'OptIn',
          name: key,
          label: field.checkboxLabel.trim() || label,
          required: field.required,
        };
      }

      const inputType = field.type === 'phone' ? 'phone' : field.type;
      return {
        type: 'TextInput',
        name: key,
        label,
        required: field.required,
        'input-type': inputType,
      };
    });

    const layoutChildren: unknown[] = [
      {
        type: 'TextHeading',
        text: heading,
      },
    ];

    if (subheading) {
      layoutChildren.push({
        type: 'TextSubheading',
        text: subheading,
      });
    }

    layoutChildren.push({
      type: 'Form',
      name: formName,
      children: [
        ...inputChildren,
        {
          type: 'Footer',
          label: submitLabel,
          'on-click-action': {
            name: 'complete',
            payload: completionPayload,
          },
        },
      ],
    });

    return JSON.stringify({
      version: '7.1',
      routing_model: {
        [screenId]: [],
      },
      screens: [
        {
          id: screenId,
          title,
          terminal: true,
          success: true,
          layout: {
            type: 'SingleColumnLayout',
            children: layoutChildren,
          },
        },
      ],
    }, null, 2);
  }

  private buildCompletionPayload(fields: MetaFlowBuilderField[]): Record<string, string> {
    const payload: Record<string, string> = {};

    fields.forEach((field, index) => {
      const key = this.sanitizeVariableName(field.key) || `field_${index + 1}`;
      if (payload[key]) {
        return;
      }

      payload[key] = '${form.' + key + '}';
    });

    return payload;
  }

  private hydrateBuilderFromFlowJson(flowJsonRaw: string): void {
    const raw = flowJsonRaw.trim();
    if (!raw) {
      return;
    }

    try {
      const parsed = JSON.parse(raw) as unknown;
      const root = this.asObject(parsed);
      if (!root) {
        return;
      }

      const screens = Array.isArray(root['screens']) ? root['screens'] : [];
      const firstScreen = this.asObject(screens[0]);
      if (!firstScreen) {
        return;
      }

      const layout = this.asObject(firstScreen['layout']);
      const layoutChildren = this.readUnknownArray(layout, 'children');
      const headingNode = layoutChildren
        .map(item => this.asObject(item))
        .find(node => this.readString(node, 'type')?.toLowerCase() === 'textheading') ?? null;
      const subheadingNode = layoutChildren
        .map(item => this.asObject(item))
        .find(node => this.readString(node, 'type')?.toLowerCase() === 'textsubheading') ?? null;
      const formNode = this.findFormNode(layoutChildren);
      if (!formNode) {
        return;
      }

      const formChildren = this.readUnknownArray(formNode, 'children');
      const footerNode = formChildren
        .map(item => this.asObject(item))
        .find(node => this.readString(node, 'type')?.toLowerCase() === 'footer') ?? null;

      const mappedFields: MetaFlowBuilderField[] = formChildren
        .map(item => this.mapBuilderFieldFromFlowComponent(item))
        .filter((field): field is MetaFlowBuilderField => !!field);

      if (mappedFields.length === 0) {
        return;
      }

      const title = this.readString(firstScreen, 'title') ?? this.readString(headingNode, 'text') ?? this.t('metaFlows.formBuilder.defaults.screenTitle');

      this.builderForm.set({
        screenId: this.sanitizeScreenId(this.readString(firstScreen, 'id') ?? 'LEAD_CAPTURE_FORM'),
        title,
        heading: this.readString(headingNode, 'text') ?? title,
        subheading: this.readString(subheadingNode, 'text') ?? '',
        submitLabel: this.readString(footerNode, 'label') ?? this.t('metaFlows.formBuilder.defaults.submitLabel'),
        fields: mappedFields,
      });
    } catch {
      // If JSON isn't parseable, keep current builder state.
    }
  }

  private mapBuilderFieldFromFlowComponent(component: unknown): MetaFlowBuilderField | null {
    const node = this.asObject(component);
    if (!node) {
      return null;
    }

    const type = (this.readString(node, 'type') ?? '').toLowerCase();
    if (type === 'footer' || type === 'textheading' || type === 'textsubheading') {
      return null;
    }

    const key = this.sanitizeVariableName(this.readString(node, 'name') ?? '');
    if (!key) {
      return null;
    }

    const required = this.readBoolean(node, 'required');
    const label = this.readString(node, 'label') ?? key;

    if (type === 'optin' || type === 'checkboxgroup') {
      return {
        id: `builder_${key}_${Math.random().toString(36).slice(2, 6)}`,
        key,
        label,
        type: 'checkbox',
        required,
        checkboxLabel: label,
      };
    }

    if (type === 'textinput') {
      const inputTypeRaw = (this.readString(node, 'input-type') ?? this.readString(node, 'inputType') ?? 'text').toLowerCase();
      const mappedType: MetaFlowBuilderFieldType = inputTypeRaw === 'email'
        ? 'email'
        : inputTypeRaw === 'phone'
          ? 'phone'
          : 'text';

      return {
        id: `builder_${key}_${Math.random().toString(36).slice(2, 6)}`,
        key,
        label,
        type: mappedType,
        required,
        checkboxLabel: '',
      };
    }

    return null;
  }

  private findFormNode(children: unknown[]): Record<string, unknown> | null {
    for (const child of children) {
      const node = this.asObject(child);
      if (!node) {
        continue;
      }

      const type = (this.readString(node, 'type') ?? '').toLowerCase();
      if (type === 'form') {
        return node;
      }

      const nested = this.findFormNode(this.readUnknownArray(node, 'children'));
      if (nested) {
        return nested;
      }
    }

    return null;
  }

  private sanitizeVariableName(raw: string): string {
    return raw
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9_]+/g, '_')
      .replace(/^_+|_+$/g, '');
  }

  private sanitizeScreenId(raw: string): string {
    const normalized = raw
      .trim()
      .toUpperCase()
      .replace(/[^A-Z0-9_]+/g, '_')
      .replace(/^_+|_+$/g, '');

    return normalized || 'LEAD_CAPTURE_FORM';
  }

  private normalizeJsonText(raw: string): string {
    const input = raw.trim();
    if (!input) {
      return '{}';
    }

    try {
      return JSON.stringify(JSON.parse(input) as unknown, null, 2);
    } catch {
      return input;
    }
  }

  private asObject(value: unknown): Record<string, unknown> | null {
    if (!value || typeof value !== 'object' || Array.isArray(value)) {
      return null;
    }

    return value as Record<string, unknown>;
  }

  private readUnknownArray(source: Record<string, unknown> | null, key: string): unknown[] {
    if (!source) {
      return [];
    }

    const value = source[key];
    return Array.isArray(value) ? value : [];
  }

  private readBoolean(source: Record<string, unknown> | null, key: string): boolean {
    if (!source) {
      return false;
    }

    const value = source[key];
    return typeof value === 'boolean' ? value : false;
  }

  private normalizeCategories(raw: string[]): string[] {
    const categories = raw
      .map(item => item.trim().toUpperCase())
      .filter(item => this.supportedCategories.includes(item as typeof this.supportedCategories[number]));

    return categories.length > 0 ? Array.from(new Set(categories)) : ['OTHER'];
  }

  private resolveFirstScreenIdFromFlowJson(flowJsonRaw: string): string | null {
    const raw = flowJsonRaw.trim();
    if (!raw) {
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as unknown;
      const root = this.asObject(parsed);
      if (!root) {
        return null;
      }

      const routingRaw = root['routing_model'];
      const routingModel = this.asObject(routingRaw);
      if (routingModel) {
        const routingKeys = Object.keys(routingModel)
          .map(key => key.trim())
          .filter(key => !!key);

        // If routing_model exists but is empty, treat the flow as not navigable.
        if (routingKeys.length === 0) {
          return null;
        }

        return routingKeys[0];
      }

      const screens = Array.isArray(root['screens']) ? root['screens'] : [];
      const firstScreen = this.asObject(screens[0]);
      return this.readString(firstScreen, 'id');
    } catch {
      return null;
    }
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
