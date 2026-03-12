import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import {
  ConversationFlow,
  ConversationFlowNode,
  CustomWebhookDispatchRequest,
  CustomWebhookDispatchResult,
  WhatsAppConnectionStatus,
  WhatsAppPhoneNumber,
} from '../../../core/models';
import { ApiService, LanguageService, PermissionService } from '../../../core/services';
import { environment } from '../../../../environments/environment';

type OutsideWindowMode = 'block' | 'allow_text' | 'template';

type CustomWebhookComposerForm = {
  profileName: string;
  token: string;
  to: string;
  contactName: string;
  message: string;
  useSelectedNodeTemplate: boolean;
  selectedFlowId: number | null;
  selectedNodeId: string;
  strictVariables: boolean;
  keepUnresolvedPlaceholders: boolean;
  outsideWindowMode: OutsideWindowMode;
  outsideWindowTemplateName: string;
  outsideWindowTemplateLanguageCode: string;
  outsideWindowTemplateComponentsJson: string;
  whatsAppPhoneNumberId: string;
  phoneNumberId: string;
  dataJson: string;
  enableAttachment: boolean;
  attachmentType: 'image' | 'video' | 'audio' | 'document' | 'sticker';
  attachmentMediaId: string;
  attachmentMediaUrl: string;
  attachmentCaption: string;
  attachmentFileName: string;
  attachmentMimeType: string;
};

type SavedCustomWebhookProfile = {
  id: string;
  name: string;
  savedAtUtc: string;
  form: Omit<CustomWebhookComposerForm, 'profileName'>;
  variableInputs: Record<string, string>;
};

@Component({
  selector: 'app-custom-webhooks',
  standalone: true,
  imports: [FormsModule, TranslateModule, RouterLink],
  templateUrl: './custom-webhooks.component.html',
  styleUrl: './custom-webhooks.component.scss',
})
export class CustomWebhooksComponent implements OnInit {
  private static readonly ProfilesStorageKey = 'custom_webhook_profiles_v1';

  private readonly api = inject(ApiService);
  private readonly translate = inject(TranslateService);
  private readonly route = inject(ActivatedRoute);

  readonly langService = inject(LanguageService);
  readonly perm = inject(PermissionService);

  readonly webhookContextLoading = signal(false);
  readonly webhookContextError = signal('');
  readonly webhookConnectionStatus = signal<WhatsAppConnectionStatus | null>(null);
  readonly webhookPhoneNumbers = signal<WhatsAppPhoneNumber[]>([]);

  readonly flowsLoading = signal(false);
  readonly flowsError = signal('');
  readonly flows = signal<ConversationFlow[]>([]);

  readonly webhookDispatching = signal(false);
  readonly webhookDispatchError = signal('');
  readonly webhookDispatchSuccess = signal('');
  readonly webhookDispatchResult = signal<CustomWebhookDispatchResult | null>(null);

  readonly savedProfiles = signal<SavedCustomWebhookProfile[]>([]);
  readonly customWebhookForm = signal<CustomWebhookComposerForm>(this.createDefaultForm());
  readonly variableInputs = signal<Record<string, string>>({});

  readonly customWebhookEndpoint = computed(() => this.buildCustomWebhookUrlFromApiBase());

  readonly selectedFlow = computed(() => {
    const flowId = this.customWebhookForm().selectedFlowId;
    if (!flowId || flowId <= 0) {
      return null;
    }

    return this.flows().find(item => item.conversationFlowId === flowId) ?? null;
  });

  readonly selectableNodes = computed(() => {
    const flow = this.selectedFlow();
    if (!flow) {
      return [] as ConversationFlowNode[];
    }

    return flow.definition.nodes.filter(node => !!node.bodyText?.trim());
  });

  readonly selectedNodeTemplate = computed(() => {
    const selectedNodeId = this.customWebhookForm().selectedNodeId;
    if (!selectedNodeId) {
      return '';
    }

    const node = this.selectableNodes().find(item => item.id === selectedNodeId);
    return (node?.bodyText ?? '').trim();
  });

  readonly effectiveTemplate = computed(() => {
    const form = this.customWebhookForm();
    if (form.useSelectedNodeTemplate) {
      return this.selectedNodeTemplate();
    }

    return form.message.trim();
  });

  readonly detectedTemplateVariables = computed(() => this.extractTemplateVariables(this.effectiveTemplate()));
  readonly attachmentSupportsCaption = computed(() => {
    const type = this.customWebhookForm().attachmentType;
    return type === 'image' || type === 'video' || type === 'document';
  });

  readonly activeVariableInputs = computed(() => {
    const values = this.variableInputs();
    return this.detectedTemplateVariables().map(name => ({
      name,
      value: values[name] ?? '',
    }));
  });

  readonly generatedVariables = computed<Record<string, unknown>>(() => {
    const values = this.variableInputs();
    const payload: Record<string, unknown> = {};

    for (const key of this.detectedTemplateVariables()) {
      payload[key] = values[key] ?? '';
    }

    return payload;
  });

  readonly requestBodyPreview = computed(() => {
    const form = this.customWebhookForm();
    const preview: Record<string, unknown> = {
      to: form.to.trim() || '201000000000',
      message: this.effectiveTemplate() || 'Hello {{customer_name}}',
      variables: this.generatedVariables(),
      strictVariables: form.strictVariables,
      keepUnresolvedPlaceholders: form.keepUnresolvedPlaceholders,
      outsideWindowAction: form.outsideWindowMode,
    };

    if (form.contactName.trim()) {
      preview['contactName'] = form.contactName.trim();
    }

    if (form.outsideWindowMode === 'template') {
      preview['outsideWindowTemplate'] = {
        templateName: form.outsideWindowTemplateName.trim() || 'my_template_name',
        languageCode: form.outsideWindowTemplateLanguageCode.trim() || 'en_US',
      };
    }

    if (form.outsideWindowMode === 'allow_text') {
      preview['allowOutside24HourWindow'] = true;
    }

    if (form.enableAttachment) {
      const attachment: Record<string, unknown> = {
        type: form.attachmentType,
      };

      if (form.attachmentMediaId.trim()) {
        attachment['mediaId'] = form.attachmentMediaId.trim();
      }

      if (form.attachmentMediaUrl.trim()) {
        attachment['mediaUrl'] = form.attachmentMediaUrl.trim();
      }

      if (this.attachmentSupportsCaption() && form.attachmentCaption.trim()) {
        attachment['caption'] = form.attachmentCaption.trim();
      }

      if (form.attachmentType === 'document' && form.attachmentFileName.trim()) {
        attachment['fileName'] = form.attachmentFileName.trim();
      }

      if (form.attachmentMimeType.trim()) {
        attachment['mimeType'] = form.attachmentMimeType.trim();
      }

      preview['attachment'] = attachment;
    }

    return JSON.stringify(preview, null, 2);
  });

  readonly curlPreview = computed(() => {
    const endpoint = this.customWebhookEndpoint();
    const token = this.customWebhookForm().token.trim() || '<WEBHOOK_TOKEN>';
    const payload = this.requestBodyPreview().replace(/'/g, "'\\''");

    return [
      `curl -X POST "${endpoint}?token=${encodeURIComponent(token)}" \\\n  -H "Content-Type: application/json" \\\n  -d '${payload}'`
    ].join('');
  });

  private readonly syncVariableInputsEffect = effect(() => {
    const keys = this.detectedTemplateVariables();
    this.variableInputs.update(current => {
      const next: Record<string, string> = {};
      for (const key of keys) {
        next[key] = current[key] ?? '';
      }

      return next;
    });
  });

  private readonly ensureSelectedNodeEffect = effect(() => {
    const form = this.customWebhookForm();
    const nodes = this.selectableNodes();

    if (nodes.length === 0) {
      if (form.selectedNodeId) {
        this.patchWebhookForm({ selectedNodeId: '' }, false);
      }
      return;
    }

    if (!form.selectedNodeId || !nodes.some(item => item.id === form.selectedNodeId)) {
      this.patchWebhookForm({ selectedNodeId: nodes[0].id }, false);
    }
  });

  ngOnInit(): void {
    this.loadSavedProfiles();
    this.applyDeepLinkParams();
    this.loadFlows();
    this.loadWebhookContext();
  }

  loadWebhookContext(): void {
    if (!this.perm.isAdmin) {
      return;
    }

    this.webhookContextLoading.set(true);
    this.webhookContextError.set('');

    let pending = 2;
    const complete = () => {
      pending -= 1;
      if (pending <= 0) {
        this.webhookContextLoading.set(false);
      }
    };

    this.api.get<WhatsAppConnectionStatus>('/company/connection-status').subscribe({
      next: status => {
        this.webhookConnectionStatus.set(status);
        const token = status?.verifyToken?.trim();
        if (token && !this.customWebhookForm().token.trim()) {
          this.patchWebhookForm({ token }, false);
        }

        complete();
      },
      error: () => {
        this.webhookContextError.set(this.t('customWebhooks.feedback.contextLoadError'));
        complete();
      },
    });

    this.api.get<WhatsAppPhoneNumber[]>('/phone-numbers').subscribe({
      next: numbers => {
        const list = numbers ?? [];
        this.webhookPhoneNumbers.set(list);

        const defaultPhone = list.find(item => item.isDefault) ?? list[0];
        if (defaultPhone) {
          this.patchWebhookForm({
            whatsAppPhoneNumberId: this.customWebhookForm().whatsAppPhoneNumberId || String(defaultPhone.whatsAppPhoneNumberId),
            phoneNumberId: this.customWebhookForm().phoneNumberId || defaultPhone.phoneNumberId,
          }, false);
        }

        complete();
      },
      error: () => {
        this.webhookContextError.set(this.t('customWebhooks.feedback.contextLoadError'));
        complete();
      },
    });
  }

  loadFlows(): void {
    this.flowsLoading.set(true);
    this.flowsError.set('');

    this.api.get<ConversationFlow[]>('/conversation-flows').subscribe({
      next: flows => {
        const list = flows ?? [];
        this.flows.set(list);
        this.flowsLoading.set(false);

        const currentFlowId = this.customWebhookForm().selectedFlowId;
        if (currentFlowId && list.some(item => item.conversationFlowId === currentFlowId)) {
          return;
        }

        const firstUsableFlow = list.find(item => this.getMessageNodes(item).length > 0);
        if (!firstUsableFlow) {
          return;
        }

        const firstNode = this.getMessageNodes(firstUsableFlow)[0];
        this.patchWebhookForm({
          selectedFlowId: firstUsableFlow.conversationFlowId,
          selectedNodeId: firstNode?.id ?? '',
        }, false);
      },
      error: () => {
        this.flowsLoading.set(false);
        this.flowsError.set(this.t('customWebhooks.feedback.flowsLoadError'));
      },
    });
  }

  updateWebhookField<K extends keyof CustomWebhookComposerForm>(field: K, value: CustomWebhookComposerForm[K]): void {
    this.patchWebhookForm({ [field]: value } as Partial<CustomWebhookComposerForm>);
  }

  onFlowChanged(value: string): void {
    const flowId = Number(value);
    if (!Number.isFinite(flowId) || flowId <= 0) {
      this.patchWebhookForm({
        selectedFlowId: null,
        selectedNodeId: '',
      });
      return;
    }

    const flow = this.flows().find(item => item.conversationFlowId === flowId);
    const firstNode = flow ? this.getMessageNodes(flow)[0] : null;

    this.patchWebhookForm({
      selectedFlowId: flowId,
      selectedNodeId: firstNode?.id ?? '',
    });
  }

  updateVariableInput(name: string, value: string): void {
    this.variableInputs.update(current => ({
      ...current,
      [name]: value,
    }));

    this.clearDispatchFeedback();
  }

  copyWebhookValue(value: string, successKey: string): void {
    if (!value) {
      return;
    }

    navigator.clipboard.writeText(value).then(
      () => this.webhookDispatchSuccess.set(this.t(successKey)),
      () => this.webhookDispatchError.set(this.t('customWebhooks.feedback.copyFailed')),
    );
  }

  saveProfile(): void {
    const form = this.customWebhookForm();
    const name = form.profileName.trim();

    if (!name) {
      this.webhookDispatchError.set(this.t('customWebhooks.feedback.profileNameRequired'));
      return;
    }

    const { profileName: _, ...profileForm } = form;
    const profiles = this.savedProfiles();
    const existing = profiles.find(item => item.name.toLowerCase() === name.toLowerCase());
    const id = existing?.id ?? `profile_${Math.random().toString(36).slice(2, 10)}`;

    const nextProfile: SavedCustomWebhookProfile = {
      id,
      name,
      savedAtUtc: new Date().toISOString(),
      form: profileForm,
      variableInputs: this.variableInputs(),
    };

    const updated = existing
      ? profiles.map(item => item.id === id ? nextProfile : item)
      : [nextProfile, ...profiles];

    this.savedProfiles.set(updated);
    this.persistProfiles(updated);
    this.webhookDispatchSuccess.set(this.t('customWebhooks.feedback.profileSaved'));
    this.webhookDispatchError.set('');
  }

  loadProfile(profileId: string): void {
    const profile = this.savedProfiles().find(item => item.id === profileId);
    if (!profile) {
      return;
    }

    const defaults = this.createDefaultForm();
    this.customWebhookForm.set({
      ...defaults,
      ...profile.form,
      profileName: profile.name,
    });
    this.variableInputs.set(profile.variableInputs ?? {});
    this.clearDispatchFeedback();
    this.webhookDispatchSuccess.set(this.t('customWebhooks.feedback.profileLoaded'));
  }

  deleteProfile(profileId: string): void {
    const next = this.savedProfiles().filter(item => item.id !== profileId);
    this.savedProfiles.set(next);
    this.persistProfiles(next);
    this.webhookDispatchSuccess.set(this.t('customWebhooks.feedback.profileDeleted'));
    this.webhookDispatchError.set('');
  }

  formatSavedAt(savedAtUtc: string): string {
    const parsed = new Date(savedAtUtc);
    if (Number.isNaN(parsed.getTime())) {
      return savedAtUtc;
    }

    const locale = this.langService.isRtl() ? 'ar-EG' : 'en-US';
    return new Intl.DateTimeFormat(locale, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(parsed);
  }

  dispatchCustomWebhook(): void {
    const form = this.customWebhookForm();
    const token = form.token.trim();
    const to = form.to.trim();
    const template = this.effectiveTemplate();

    this.clearDispatchFeedback();

    if (!token) {
      this.webhookDispatchError.set(this.t('customWebhooks.feedback.tokenRequired'));
      return;
    }

    if (!to) {
      this.webhookDispatchError.set(this.t('customWebhooks.feedback.toRequired'));
      return;
    }

    if (!template) {
      this.webhookDispatchError.set(this.t('customWebhooks.feedback.templateRequired'));
      return;
    }

    const parsedData = this.tryParseOptionalJson(form.dataJson, 'customWebhooks.feedback.dataJsonInvalid');
    if (parsedData === undefined && form.dataJson.trim()) {
      return;
    }

    let parsedTemplateComponents: unknown[] | null | undefined;
    if (form.outsideWindowMode === 'template') {
      if (!form.outsideWindowTemplateName.trim()) {
        this.webhookDispatchError.set(this.t('customWebhooks.feedback.templateNameRequired'));
        return;
      }

      parsedTemplateComponents = this.tryParseTemplateComponentsJson(
        form.outsideWindowTemplateComponentsJson,
        'customWebhooks.feedback.templateComponentsInvalid');

      if (parsedTemplateComponents === null) {
        return;
      }
    }

    let attachmentPayload: CustomWebhookDispatchRequest['attachment'] | undefined;
    if (form.enableAttachment) {
      const mediaId = form.attachmentMediaId.trim();
      const mediaUrl = form.attachmentMediaUrl.trim();

      if (!mediaId && !mediaUrl) {
        this.webhookDispatchError.set(this.t('customWebhooks.feedback.attachmentReferenceRequired'));
        return;
      }

      if (mediaId && mediaUrl) {
        this.webhookDispatchError.set(this.t('customWebhooks.feedback.attachmentReferenceExclusive'));
        return;
      }

      if (mediaUrl && !/^https?:\/\//i.test(mediaUrl)) {
        this.webhookDispatchError.set(this.t('customWebhooks.feedback.attachmentUrlInvalid'));
        return;
      }

      attachmentPayload = {
        type: form.attachmentType,
        mediaId: mediaId || undefined,
        mediaUrl: mediaUrl || undefined,
        caption: this.attachmentSupportsCaption() ? (form.attachmentCaption.trim() || undefined) : undefined,
        fileName: form.attachmentType === 'document' ? (form.attachmentFileName.trim() || undefined) : undefined,
        mimeType: form.attachmentMimeType.trim() || undefined,
      };
    }

    const payload: CustomWebhookDispatchRequest = {
      to,
      message: template,
      contactName: form.contactName.trim() || undefined,
      strictVariables: form.strictVariables,
      keepUnresolvedPlaceholders: form.keepUnresolvedPlaceholders,
      allowOutside24HourWindow: form.outsideWindowMode === 'allow_text',
      outsideWindowAction: form.outsideWindowMode,
      variables: this.generatedVariables(),
      data: parsedData,
    };

    if (attachmentPayload) {
      payload.attachment = attachmentPayload;
    }

    if (form.outsideWindowMode === 'template') {
      payload.outsideWindowTemplate = {
        templateName: form.outsideWindowTemplateName.trim(),
        languageCode: form.outsideWindowTemplateLanguageCode.trim() || 'en_US',
        components: parsedTemplateComponents ?? [],
      };
    }

    const whatsAppPhoneNumberId = Number(form.whatsAppPhoneNumberId);
    if (Number.isFinite(whatsAppPhoneNumberId) && whatsAppPhoneNumberId > 0) {
      payload.whatsAppPhoneNumberId = whatsAppPhoneNumberId;
    }

    const phoneNumberId = form.phoneNumberId.trim();
    if (phoneNumberId) {
      payload.phoneNumberId = phoneNumberId;
    }

    this.webhookDispatching.set(true);
    this.api.postRaw<CustomWebhookDispatchResult>(`/webhook/custom/dispatch?token=${encodeURIComponent(token)}`, payload).subscribe({
      next: response => {
        this.webhookDispatching.set(false);

        if (response.success && response.data) {
          this.webhookDispatchResult.set(response.data);
          this.webhookDispatchSuccess.set(this.t('customWebhooks.feedback.dispatchSuccess'));
          return;
        }

        const details = response.error?.details ? ` ${response.error.details}` : '';
        this.webhookDispatchError.set((response.message || this.t('customWebhooks.feedback.dispatchError')) + details);
      },
      error: error => {
        this.webhookDispatching.set(false);
        this.webhookDispatchError.set(error?.error?.message ?? this.t('customWebhooks.feedback.dispatchError'));
      },
    });
  }

  private applyDeepLinkParams(): void {
    const flowIdParam = Number(this.route.snapshot.queryParamMap.get('flowId'));
    const nodeIdParam = this.route.snapshot.queryParamMap.get('nodeId');

    const patch: Partial<CustomWebhookComposerForm> = {};

    if (Number.isFinite(flowIdParam) && flowIdParam > 0) {
      patch.selectedFlowId = flowIdParam;
      patch.useSelectedNodeTemplate = true;
    }

    if (nodeIdParam) {
      patch.selectedNodeId = nodeIdParam;
      patch.useSelectedNodeTemplate = true;
    }

    if (Object.keys(patch).length > 0) {
      this.patchWebhookForm(patch, false);
    }
  }

  private tryParseOptionalJson(raw: string, errorKey: string): unknown {
    const input = raw.trim();
    if (!input) {
      return undefined;
    }

    try {
      return JSON.parse(input) as unknown;
    } catch {
      this.webhookDispatchError.set(this.t(errorKey));
      return undefined;
    }
  }

  private tryParseTemplateComponentsJson(raw: string, errorKey: string): unknown[] | null {
    const input = raw.trim();
    if (!input) {
      return [];
    }

    try {
      const parsed = JSON.parse(input) as unknown;
      if (Array.isArray(parsed)) {
        return parsed;
      }

      this.webhookDispatchError.set(this.t(errorKey));
      return null;
    } catch {
      this.webhookDispatchError.set(this.t(errorKey));
      return null;
    }
  }

  private patchWebhookForm(patch: Partial<CustomWebhookComposerForm>, clearFeedback = true): void {
    this.customWebhookForm.update(form => {
      let changed = false;
      for (const [key, value] of Object.entries(patch)) {
        const current = (form as Record<string, unknown>)[key];
        if (current !== value) {
          changed = true;
          break;
        }
      }

      if (!changed) {
        return form;
      }

      return {
        ...form,
        ...patch,
      };
    });

    if (clearFeedback) {
      this.clearDispatchFeedback();
    }
  }

  private clearDispatchFeedback(): void {
    this.webhookDispatchError.set('');
    this.webhookDispatchSuccess.set('');
    this.webhookDispatchResult.set(null);
  }

  private createDefaultForm(): CustomWebhookComposerForm {
    const isRtl = this.langService.isRtl();

    return {
      profileName: '',
      token: '',
      to: '',
      contactName: '',
      message: isRtl
        ? 'مرحبًا {{customer_name}}، طلبك {{request_id|default:N/A}} حالته الآن {{status|upper}}.'
        : 'Hello {{customer_name}}, your request {{request_id|default:N/A}} is now {{status|upper}}.',
      useSelectedNodeTemplate: true,
      selectedFlowId: null,
      selectedNodeId: '',
      strictVariables: true,
      keepUnresolvedPlaceholders: false,
      outsideWindowMode: 'block',
      outsideWindowTemplateName: '',
      outsideWindowTemplateLanguageCode: 'en_US',
      outsideWindowTemplateComponentsJson: '[\n  {\n    "type": "body",\n    "parameters": [\n      { "type": "text", "text": "{{customer_name}}" }\n    ]\n  }\n]',
      whatsAppPhoneNumberId: '',
      phoneNumberId: '',
      dataJson: '',
      enableAttachment: false,
      attachmentType: 'document',
      attachmentMediaId: '',
      attachmentMediaUrl: '',
      attachmentCaption: '',
      attachmentFileName: '',
      attachmentMimeType: '',
    };
  }

  private loadSavedProfiles(): void {
    if (typeof window === 'undefined') {
      return;
    }

    try {
      const raw = window.localStorage.getItem(CustomWebhooksComponent.ProfilesStorageKey);
      if (!raw) {
        this.savedProfiles.set([]);
        return;
      }

      const parsed = JSON.parse(raw) as SavedCustomWebhookProfile[];
      if (!Array.isArray(parsed)) {
        this.savedProfiles.set([]);
        return;
      }

      this.savedProfiles.set(parsed);
    } catch {
      this.savedProfiles.set([]);
    }
  }

  private persistProfiles(profiles: SavedCustomWebhookProfile[]): void {
    if (typeof window === 'undefined') {
      return;
    }

    try {
      window.localStorage.setItem(CustomWebhooksComponent.ProfilesStorageKey, JSON.stringify(profiles));
    } catch {
      // Ignore local storage quota errors to avoid blocking dispatch usage.
    }
  }

  private getMessageNodes(flow: ConversationFlow): ConversationFlowNode[] {
    return flow.definition.nodes.filter(node => !!node.bodyText?.trim());
  }

  private buildCustomWebhookUrlFromApiBase(): string {
    const apiUrl = environment.apiUrl.trim();
    const normalizedApiPath = apiUrl.replace(/\/+$/, '').replace(/\/api$/, '/api');

    if (/^https?:\/\//i.test(normalizedApiPath)) {
      return normalizedApiPath.replace(/\/api$/, '') + '/api/webhook/custom/dispatch';
    }

    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    const relativePath = normalizedApiPath.startsWith('/') ? normalizedApiPath : `/${normalizedApiPath}`;
    return `${origin}${relativePath.replace(/\/api$/, '')}/api/webhook/custom/dispatch`;
  }

  private extractTemplateVariables(template: string): string[] {
    if (!template) {
      return [];
    }

    const regex = /{{\s*([^{}]+?)\s*}}/g;
    const variables = new Set<string>();
    let match: RegExpExecArray | null;

    while ((match = regex.exec(template)) !== null) {
      const expression = match[1]?.trim();
      if (!expression) {
        continue;
      }

      const variableName = expression.split('|', 1)[0].trim();
      if (variableName) {
        variables.add(variableName);
      }
    }

    return Array.from(variables).sort((left, right) => left.localeCompare(right));
  }

  private t(key: string, params?: Record<string, unknown>): string {
    this.langService.currentLang();
    return this.translate.instant(key, params);
  }
}
