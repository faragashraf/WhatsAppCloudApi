import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ApiService } from '../../../core/services';
import { Message, PagedResult } from '../../../core/models';

interface MessageAttachmentPreview {
  icon: string;
  label: string;
  fileName: string | null;
  caption: string | null;
}

interface MessageTemplatePreview {
  name: string;
  language: string | null;
  parameters: string[];
}

interface MessagePayloadPreview {
  previewText: string | null;
  attachment: MessageAttachmentPreview | null;
  template: MessageTemplatePreview | null;
  usedStructuredFallback: boolean;
}

interface MessageDetailCardItem {
  label: string;
  value: string;
  mono?: boolean;
}

interface MessageStatusTheme {
  badgeClass: string;
  icon: string;
}

interface MessageDetailViewModel {
  recipient: string;
  initials: string;
  createdAtLabel: string;
  createdTimeLabel: string;
  statusLabel: string;
  statusTheme: MessageStatusTheme;
  typeLabel: string;
  typeIcon: string;
  headline: string;
  previewText: string | null;
  previewDirection: 'ltr' | 'rtl';
  attachment: MessageAttachmentPreview | null;
  template: MessageTemplatePreview | null;
  detailItems: MessageDetailCardItem[];
  error: string | null;
  structuredNote: boolean;
}

@Component({
  selector: 'app-messages',
  standalone: true,
  imports: [
    FormsModule,
    NgClass,
    ProgressSpinnerModule,
    InputTextModule,
    SelectModule,
    ButtonModule,
    DialogModule,
    TranslateModule,
  ],
  templateUrl: './messages.component.html',
  styleUrls: ['./messages.component.scss'],
})
export class MessagesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly translate = inject(TranslateService);
  protected readonly Math = Math;

  private static readonly RTL_REGEX = /[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF\u0590-\u05FF]/;
  private readonly payloadPreviewCache = new Map<number, MessagePayloadPreview>();

  messages = signal<Message[]>([]);
  totalCount = signal(0);
  hasNext = signal(false);
  loading = signal(true);
  page = signal(1);
  pageSize = 20;
  searchQuery = '';
  statusFilter = '';
  typeFilter = '';

  showDetail = false;
  selectedMessage = signal<Message | null>(null);
  detailView = computed<MessageDetailViewModel | null>(() => {
    const msg = this.selectedMessage();
    return msg ? this.buildDetailView(msg) : null;
  });

  statusOptions = [
    { label: 'All', value: '' },
    { label: 'Sent', value: 'SENT' },
    { label: 'Pending', value: 'PENDING' },
    { label: 'Failed', value: 'FAILED' },
  ];

  typeOptions = [
    { label: 'All', value: '' },
    { label: 'Text', value: 'TEXT' },
    { label: 'Template', value: 'TEMPLATE' },
    { label: 'Image', value: 'IMAGE' },
    { label: 'Video', value: 'VIDEO' },
    { label: 'Audio', value: 'AUDIO' },
    { label: 'Document', value: 'DOCUMENT' },
    { label: 'Sticker', value: 'STICKER' },
    { label: 'Interactive', value: 'INTERACTIVE' },
    { label: 'Button', value: 'BUTTON' },
    { label: 'Location', value: 'LOCATION' },
    { label: 'Contacts', value: 'CONTACTS' },
    { label: 'Reaction', value: 'REACTION' },
    { label: 'Order', value: 'ORDER' },
    { label: 'System', value: 'SYSTEM' },
    { label: 'Welcome Request', value: 'REQUEST_WELCOME' },
    { label: 'Unknown', value: 'UNKNOWN' },
  ];

  ngOnInit(): void {
    this.loadMessages();
  }

  loadMessages(): void {
    this.loading.set(true);

    const params: Record<string, string | number> = {
      page: this.page(),
      pageSize: this.pageSize,
    };

    if (this.statusFilter) params['status'] = this.statusFilter;
    if (this.typeFilter) params['type'] = this.typeFilter;
    if (this.searchQuery) params['search'] = this.searchQuery;

    this.api.get<PagedResult<Message>>('/messages', params).subscribe({
      next: (result) => {
        this.messages.set(result.items ?? []);
        this.totalCount.set(result.totalCount ?? 0);
        this.hasNext.set(result.hasNext ?? false);
        this.loading.set(false);
      },
      error: () => {
        this.messages.set([]);
        this.totalCount.set(0);
        this.hasNext.set(false);
        this.loading.set(false);
      },
    });
  }

  goToPage(nextPage: number): void {
    this.page.set(nextPage);
    this.loadMessages();
  }

  openDetail(msg: Message): void {
    this.selectedMessage.set(msg);
    this.showDetail = true;
  }

  closeDetail(): void {
    this.showDetail = false;
    this.selectedMessage.set(null);
  }

  getFriendlyTypeLabel(type: string): string {
    switch (type?.trim().toUpperCase()) {
      case 'TEXT':
        return this.translate.instant('sendMessage.textMessage');
      case 'TEMPLATE':
        return this.translate.instant('sendMessage.templateMessage');
      case 'IMAGE':
        return this.translate.instant('inbox.media.image');
      case 'VIDEO':
        return this.translate.instant('inbox.media.video');
      case 'AUDIO':
        return this.translate.instant('inbox.media.audio');
      case 'DOCUMENT':
        return this.translate.instant('inbox.media.document');
      case 'STICKER':
        return this.translate.instant('inbox.media.sticker');
      case 'INTERACTIVE':
        return this.translate.instant('inbox.media.interactive');
      case 'BUTTON':
        return this.translate.instant('inbox.media.button');
      case 'LOCATION':
        return this.translate.instant('inbox.media.location');
      case 'CONTACTS':
        return this.translate.instant('inbox.media.contacts');
      case 'REACTION':
        return this.translate.instant('inbox.media.reaction');
      case 'ORDER':
        return this.translate.instant('inbox.media.order');
      case 'SYSTEM':
        return this.translate.instant('inbox.media.system');
      case 'REQUEST_WELCOME':
        return this.translate.instant('inbox.media.request_welcome');
      case 'UNKNOWN':
        return this.translate.instant('inbox.media.unknown');
      default:
        return this.startCase(type || 'Message');
    }
  }

  getStatusLabel(status: string): string {
    switch (status?.trim().toUpperCase()) {
      case 'SENT':
        return this.translate.instant('messages.sent');
      case 'FAILED':
        return this.translate.instant('messages.failed');
      case 'PENDING':
        return this.translate.instant('messages.pending');
      default:
        return this.startCase(status || 'Unknown');
    }
  }

  formatListTimestamp(dateStr: string | null): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString([], {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  private buildDetailView(msg: Message): MessageDetailViewModel {
    const preview = this.parseMessagePreview(msg);
    const detailItems: MessageDetailCardItem[] = [
      { label: this.translate.instant('messages.detail.recipient'), value: msg.toNumber || '-', mono: true },
      { label: this.translate.instant('messages.detail.messageId'), value: `#${msg.messageId}`, mono: true },
      { label: this.translate.instant('messages.detail.type'), value: this.getFriendlyTypeLabel(msg.messageType) },
      { label: 'Source', value: msg.source || 'DIRECT' },
      { label: this.translate.instant('messages.detail.created'), value: this.formatDateTime(msg.createdAtUtc) },
    ];

    if (msg.contactId) {
      detailItems.push({
        label: 'Contact',
        value: msg.contactName ? `${msg.contactName} (#${msg.contactId})` : `#${msg.contactId}`,
      });
    }

    if (msg.conversationId) {
      detailItems.push({
        label: 'Conversation',
        value: msg.conversationContactNumber
          ? `${msg.conversationContactNumber} (#${msg.conversationId})`
          : `#${msg.conversationId}`,
      });
    }

    if (msg.externalMessageId) {
      detailItems.push({
        label: this.translate.instant('messages.detail.externalId'),
        value: msg.externalMessageId,
        mono: true,
      });
    }

    if (msg.updatedAtUtc) {
      detailItems.push({
        label: this.translate.instant('messages.detail.updated'),
        value: this.formatDateTime(msg.updatedAtUtc),
      });
    }

    const previewText = preview.previewText ?? (!preview.template && !preview.attachment
      ? this.translate.instant('messages.detail.structuredMessage')
      : null);

    return {
      recipient: msg.toNumber || '-',
      initials: this.getInitials(msg.toNumber),
      createdAtLabel: this.formatDateTime(msg.createdAtUtc),
      createdTimeLabel: this.formatTime(msg.createdAtUtc),
      statusLabel: this.getStatusLabel(msg.status),
      statusTheme: this.getStatusTheme(msg.status),
      typeLabel: this.getFriendlyTypeLabel(msg.messageType),
      typeIcon: this.getTypeIcon(msg.messageType),
      headline: preview.template?.name || preview.attachment?.fileName || this.getFriendlyTypeLabel(msg.messageType),
      previewText,
      previewDirection: this.detectDir(previewText),
      attachment: preview.attachment,
      template: preview.template,
      detailItems,
      error: msg.failureReason,
      structuredNote: preview.usedStructuredFallback,
    };
  }

  private parseMessagePreview(msg: Message): MessagePayloadPreview {
    const cached = this.payloadPreviewCache.get(msg.messageId);
    if (cached) return cached;

    const raw = (msg.messageBody ?? '').trim();
    if (!raw) {
      const emptyPreview: MessagePayloadPreview = {
        previewText: null,
        attachment: null,
        template: null,
        usedStructuredFallback: false,
      };
      this.payloadPreviewCache.set(msg.messageId, emptyPreview);
      return emptyPreview;
    }

    try {
      const parsed = JSON.parse(raw) as unknown;
      const preview = this.buildPreviewFromPayload(parsed, msg);
      this.payloadPreviewCache.set(msg.messageId, preview);
      return preview;
    } catch {
      const fallbackText = this.toDisplayText(raw);
      const preview: MessagePayloadPreview = {
        previewText: fallbackText,
        attachment: null,
        template: null,
        usedStructuredFallback: fallbackText === null,
      };
      this.payloadPreviewCache.set(msg.messageId, preview);
      return preview;
    }
  }

  private buildPreviewFromPayload(payload: unknown, msg: Message): MessagePayloadPreview {
    const root = this.asObject(payload);
    if (!root) {
      return {
        previewText: this.toDisplayText(msg.messageBody),
        attachment: null,
        template: null,
        usedStructuredFallback: false,
      };
    }

    const normalizedType = (this.readString(root, 'type') || msg.messageType || '').toUpperCase();

    switch (normalizedType) {
      case 'TEXT':
        return {
          previewText: this.readNestedString(root, ['text', 'body']) || this.readString(root, 'body') || this.toDisplayText(msg.messageBody),
          attachment: null,
          template: null,
          usedStructuredFallback: false,
        };
      case 'TEMPLATE':
        return this.buildTemplatePreview(root);
      case 'IMAGE':
      case 'VIDEO':
      case 'AUDIO':
      case 'DOCUMENT':
      case 'STICKER':
        return this.buildMediaPreview(root, normalizedType);
      case 'INTERACTIVE':
        return this.buildInteractivePreview(root);
      case 'BUTTON':
        return this.buildButtonPreview(root);
      case 'LOCATION':
        return this.buildLocationPreview(root);
      case 'CONTACTS':
        return this.buildContactsPreview(root);
      case 'REACTION':
        return this.buildReactionPreview(root);
      case 'ORDER':
        return this.buildOrderPreview(root);
      case 'SYSTEM':
        return this.buildSystemPreview(root);
      case 'REQUEST_WELCOME':
        return this.buildWelcomePreview(root, normalizedType);
      default: {
        const previewText = this.extractFallbackPreviewText(root, normalizedType)
          || this.toDisplayText(msg.messageBody);

        return {
          previewText,
          attachment: null,
          template: null,
          usedStructuredFallback: previewText === null,
        };
      }
    }
  }

  private buildTemplatePreview(root: Record<string, unknown>): MessagePayloadPreview {
    const templateNode = this.asObject(root['template']);
    const rawComponents = templateNode?.['components'];
    const componentList = Array.isArray(rawComponents) ? rawComponents : [];
    const parameters = componentList
      .flatMap(component => this.extractTemplateValues(component))
      .filter(value => !!value)
      .slice(0, 6);

    return {
      previewText: null,
      attachment: null,
      template: {
        name: this.readString(templateNode, 'name') || this.translate.instant('messages.detail.template'),
        language: this.readNestedString(templateNode, ['language', 'code']),
        parameters,
      },
      usedStructuredFallback: true,
    };
  }

  private buildMediaPreview(root: Record<string, unknown>, normalizedType: string): MessagePayloadPreview {
    const mediaNode = this.asObject(root[normalizedType.toLowerCase()]);
    const caption = this.readString(mediaNode, 'caption');
    const fileName = this.readString(mediaNode, 'filename');

    return {
      previewText: caption,
      attachment: {
        icon: this.getMediaIcon(normalizedType),
        label: this.getFriendlyTypeLabel(normalizedType),
        fileName,
        caption,
      },
      template: null,
      usedStructuredFallback: !caption,
    };
  }

  private buildInteractivePreview(root: Record<string, unknown>): MessagePayloadPreview {
    const interactiveNode = this.asObject(root['interactive']);
    const interactiveType = this.readString(interactiveNode, 'type');
    const actionNode = this.asObject(interactiveNode?.['action']);
    const buttonItems = this.readArray(actionNode, 'buttons')
      .map(item => this.readNestedString(this.asObject(item), ['reply', 'title']) || this.readString(this.asObject(item), 'title'))
      .filter((value): value is string => !!value)
      .slice(0, 2);

    const sectionRows = this.readArray(actionNode, 'sections')
      .flatMap(section => this.readArray(this.asObject(section), 'rows'))
      .map(row => this.readString(this.asObject(row), 'title'))
      .filter((value): value is string => !!value)
      .slice(0, 2);

    const previewText = this.combinePreviewParts([
      this.readNestedString(interactiveNode, ['body', 'text']),
      this.readNestedString(actionNode, ['button', 'text']),
      buttonItems.length > 0 ? buttonItems.join(', ') : null,
      sectionRows.length > 0 ? sectionRows.join(', ') : null,
      this.extractFallbackPreviewText(root, 'INTERACTIVE'),
    ]);

    const title = interactiveType
      ? `${this.getFriendlyTypeLabel('INTERACTIVE')} - ${this.startCase(interactiveType)}`
      : this.getFriendlyTypeLabel('INTERACTIVE');

    return this.buildStructuredCardPreview('INTERACTIVE', previewText, title);
  }

  private buildButtonPreview(root: Record<string, unknown>): MessagePayloadPreview {
    const buttonNode = this.asObject(root['button']);
    const previewText = this.combinePreviewParts([
      this.readString(buttonNode, 'text'),
      this.readString(buttonNode, 'payload'),
      this.extractFallbackPreviewText(root, 'BUTTON'),
    ]);

    return this.buildStructuredCardPreview('BUTTON', previewText, this.getFriendlyTypeLabel('BUTTON'));
  }

  private buildLocationPreview(root: Record<string, unknown>): MessagePayloadPreview {
    const locationNode = this.asObject(root['location']) ?? root;
    const latitude = this.readScalarAsString(locationNode, 'latitude');
    const longitude = this.readScalarAsString(locationNode, 'longitude');
    const previewText = this.combinePreviewParts([
      this.readString(locationNode, 'name'),
      this.readString(locationNode, 'address'),
      latitude && longitude ? `${latitude}, ${longitude}` : null,
      this.extractFallbackPreviewText(root, 'LOCATION'),
    ]);

    return this.buildStructuredCardPreview('LOCATION', previewText, this.getFriendlyTypeLabel('LOCATION'));
  }

  private buildContactsPreview(root: Record<string, unknown>): MessagePayloadPreview {
    const contactItems = this.readArray(root, 'contacts');
    const names = contactItems
      .map(item => {
        const node = this.asObject(item);
        const nameNode = this.asObject(node?.['name']);
        return this.readString(nameNode, 'formatted_name')
          || this.readString(nameNode, 'first_name')
          || this.readString(node, 'name');
      })
      .filter((value): value is string => !!value)
      .slice(0, 3);

    const suffix = contactItems.length > names.length ? ` +${contactItems.length - names.length}` : '';
    const previewText = this.combinePreviewParts([
      names.length > 0 ? names.join(', ') + suffix : null,
      contactItems.length > 0 && names.length === 0 ? `${contactItems.length} ${this.getFriendlyTypeLabel('CONTACTS')}` : null,
      this.extractFallbackPreviewText(root, 'CONTACTS'),
    ]);

    return this.buildStructuredCardPreview('CONTACTS', previewText, this.getFriendlyTypeLabel('CONTACTS'));
  }

  private buildReactionPreview(root: Record<string, unknown>): MessagePayloadPreview {
    const reactionNode = this.asObject(root['reaction']) ?? root;
    const emoji = this.readString(reactionNode, 'emoji');
    const messageId = this.readString(reactionNode, 'message_id') || this.readString(reactionNode, 'messageId');
    const previewText = this.combinePreviewParts([
      emoji ? (messageId ? `${emoji} (${messageId})` : emoji) : messageId,
      this.extractFallbackPreviewText(root, 'REACTION'),
    ]);

    return this.buildStructuredCardPreview('REACTION', previewText, this.getFriendlyTypeLabel('REACTION'));
  }

  private buildOrderPreview(root: Record<string, unknown>): MessagePayloadPreview {
    const orderNode = this.asObject(root['order']) ?? root;
    const productItems = this.readArray(orderNode, 'product_items').length > 0
      ? this.readArray(orderNode, 'product_items')
      : this.readArray(orderNode, 'productItems');

    const itemCount = productItems.reduce<number>((sum, item) => {
      const quantity = this.readScalarNumber(this.asObject(item), 'quantity');
      return sum + (quantity && quantity > 0 ? quantity : 1);
    }, 0);

    const previewText = this.combinePreviewParts([
      this.readString(orderNode, 'text'),
      itemCount > 0 ? `${itemCount} item(s)` : null,
      this.readString(orderNode, 'catalog_id') || this.readString(orderNode, 'catalogId'),
      this.extractFallbackPreviewText(root, 'ORDER'),
    ]);

    return this.buildStructuredCardPreview('ORDER', previewText, this.getFriendlyTypeLabel('ORDER'));
  }

  private buildSystemPreview(root: Record<string, unknown>): MessagePayloadPreview {
    const systemNode = this.asObject(root['system']) ?? root;
    const previewText = this.combinePreviewParts([
      this.readString(systemNode, 'body'),
      this.readString(systemNode, 'type'),
      this.readString(systemNode, 'new_wa_id') || this.readString(systemNode, 'newWaId'),
      this.extractFallbackPreviewText(root, 'SYSTEM'),
    ]);

    return this.buildStructuredCardPreview('SYSTEM', previewText, this.getFriendlyTypeLabel('SYSTEM'));
  }

  private buildWelcomePreview(root: Record<string, unknown>, normalizedType: string): MessagePayloadPreview {
    const previewText = this.extractFallbackPreviewText(root, normalizedType)
      || this.getFriendlyTypeLabel(normalizedType);

    return this.buildStructuredCardPreview(normalizedType, previewText, this.getFriendlyTypeLabel(normalizedType));
  }

  private buildStructuredCardPreview(normalizedType: string, previewText: string | null, title: string | null): MessagePayloadPreview {
    return {
      previewText,
      attachment: {
        icon: this.getMediaIcon(normalizedType),
        label: this.getFriendlyTypeLabel(normalizedType),
        fileName: title,
        caption: null,
      },
      template: null,
      usedStructuredFallback: !previewText,
    };
  }

  private extractFallbackPreviewText(root: Record<string, unknown>, normalizedType: string): string | null {
    const typeNode = this.asObject(root[normalizedType.toLowerCase()]);
    return this.readNestedString(root, ['text', 'body'])
      || this.readString(root, 'body')
      || this.readString(root, 'caption')
      || this.readNestedString(typeNode, ['body', 'text'])
      || this.readNestedString(typeNode, ['header', 'text'])
      || this.readString(typeNode, 'text')
      || this.readString(typeNode, 'caption')
      || this.readString(typeNode, 'title')
      || this.readString(typeNode, 'name')
      || null;
  }

  private readArray(source: Record<string, unknown> | null | undefined, key: string): unknown[] {
    if (!source) return [];
    const value = source[key];
    return Array.isArray(value) ? value : [];
  }

  private combinePreviewParts(parts: Array<string | null | undefined>): string | null {
    const values = parts
      .map(item => item?.trim())
      .filter((item): item is string => !!item);

    return values.length > 0 ? values.join(' | ') : null;
  }

  private extractTemplateValues(component: unknown): string[] {
    const node = this.asObject(component);
    if (!node) return [];

    const directText = this.readString(node, 'text');
    if (directText) return [directText];

    const parameters = Array.isArray(node['parameters']) ? node['parameters'] : [];
    return parameters
      .map(parameter => this.extractParameterValue(parameter))
      .filter((value): value is string => !!value);
  }

  private extractParameterValue(parameter: unknown): string | null {
    const node = this.asObject(parameter);
    if (!node) return null;

    return this.readString(node, 'text')
      || this.readString(node, 'payload')
      || this.readString(node, 'url')
      || this.readNestedString(node, ['currency', 'fallback_value'])
      || this.readNestedString(node, ['date_time', 'fallback_value'])
      || this.readNestedString(node, ['image', 'link'])
      || this.readNestedString(node, ['video', 'link'])
      || this.readNestedString(node, ['document', 'link'])
      || this.readNestedString(node, ['document', 'filename'])
      || null;
  }

  private getStatusTheme(status: string): MessageStatusTheme {
    switch (status?.trim().toUpperCase()) {
      case 'FAILED':
        return { badgeClass: 'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-200', icon: 'pi-times-circle' };
      case 'PENDING':
        return { badgeClass: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-200', icon: 'pi-clock' };
      default:
        return { badgeClass: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-200', icon: 'pi-check-circle' };
    }
  }

  private getTypeIcon(type: string): string {
    switch (type?.trim().toUpperCase()) {
      case 'TEXT':
        return 'pi-comment';
      case 'TEMPLATE':
        return 'pi-clone';
      default:
        return this.getMediaIcon(type);
    }
  }

  private getInitials(value: string | null): string {
    if (!value) return '?';
    if (/^\+?\d/.test(value)) return value.slice(-2);
    return value
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map(part => part[0])
      .join('')
      .toUpperCase();
  }

  private getMediaIcon(type: string): string {
    switch (type?.trim().toUpperCase()) {
      case 'TEXT': return 'pi-comment';
      case 'TEMPLATE': return 'pi-clone';
      case 'IMAGE': return 'pi-image';
      case 'VIDEO': return 'pi-video';
      case 'AUDIO': return 'pi-volume-up';
      case 'DOCUMENT': return 'pi-file';
      case 'STICKER': return 'pi-face-smile';
      case 'INTERACTIVE': return 'pi-th-large';
      case 'BUTTON': return 'pi-stop-circle';
      case 'LOCATION': return 'pi-map-marker';
      case 'CONTACTS': return 'pi-users';
      case 'REACTION': return 'pi-heart';
      case 'ORDER': return 'pi-shopping-bag';
      case 'SYSTEM': return 'pi-cog';
      case 'REQUEST_WELCOME': return 'pi-megaphone';
      default: return 'pi-paperclip';
    }
  }

  private formatDateTime(dateStr: string | null): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
  }

  private formatTime(dateStr: string | null): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  private detectDir(text: string | null): 'ltr' | 'rtl' {
    if (!text) return 'ltr';
    const firstMeaningful = text.replace(/[\s\d\p{P}\p{S}]/gu, '').charAt(0);
    return MessagesComponent.RTL_REGEX.test(firstMeaningful) ? 'rtl' : 'ltr';
  }

  private asObject(value: unknown): Record<string, unknown> | null {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return null;
    return value as Record<string, unknown>;
  }

  private readString(source: Record<string, unknown> | null | undefined, key: string): string | null {
    if (!source) return null;
    const value = source[key];
    return typeof value === 'string' && value.trim() ? value.trim() : null;
  }

  private readScalarAsString(source: Record<string, unknown> | null | undefined, key: string): string | null {
    if (!source) return null;
    const value = source[key];
    if (typeof value === 'string' && value.trim()) return value.trim();
    if (typeof value === 'number' && Number.isFinite(value)) return String(value);
    return null;
  }

  private readScalarNumber(source: Record<string, unknown> | null | undefined, key: string): number | null {
    if (!source) return null;
    const value = source[key];
    if (typeof value === 'number' && Number.isFinite(value)) return value;
    if (typeof value === 'string' && value.trim()) {
      const parsed = Number(value);
      if (Number.isFinite(parsed)) return parsed;
    }
    return null;
  }

  private readNestedString(source: Record<string, unknown> | null | undefined, path: string[]): string | null {
    let current: unknown = source;
    for (const segment of path) {
      const node = this.asObject(current);
      if (!node) return null;
      current = node[segment];
    }
    return typeof current === 'string' && current.trim() ? current.trim() : null;
  }

  private toDisplayText(raw: string | null | undefined): string | null {
    const trimmed = raw?.trim();
    if (!trimmed) return null;
    if ((trimmed.startsWith('{') && trimmed.endsWith('}')) || (trimmed.startsWith('[') && trimmed.endsWith(']'))) return null;
    return trimmed;
  }

  private startCase(value: string): string {
    return value
      .toLowerCase()
      .split(/[\s_-]+/)
      .filter(Boolean)
      .map(part => part[0].toUpperCase() + part.slice(1))
      .join(' ');
  }
}
