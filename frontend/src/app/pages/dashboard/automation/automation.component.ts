import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import {
  AssignableUser,
  CustomWebhookDispatchRequest,
  CustomWebhookDispatchResult,
  ConversationFlow,
  ConversationFlowEdge,
  ConversationFlowNode,
  ConversationFlowOption,
  ConversationFlowUpsertRequest,
  RoutingTeam,
  WhatsAppConnectionStatus,
  WhatsAppPhoneNumber,
} from '../../../core/models';
import { ApiService, LanguageService, PermissionService, ThemeService } from '../../../core/services';
import { environment } from '../../../../environments/environment';
import { MetaFlowSelection, MetaFlowsComponent } from '../meta-flows/meta-flows.component';

type FlowEditorState = ConversationFlow & {
  isNew?: boolean;
};

type EdgeLine = {
  id: string;
  path: string;
  labelX: number;
  labelY: number;
  label: string;
};

type HorizontalSide = 'left' | 'right';
type NodeConnectionSides = {
  incoming: HorizontalSide;
  outgoing: HorizontalSide;
};

type DragState = {
  nodeId: string;
  startClientX: number;
  startClientY: number;
  originalX: number;
  originalY: number;
};

type CustomWebhookComposerForm = {
  token: string;
  to: string;
  contactName: string;
  message: string;
  strictVariables: boolean;
  keepUnresolvedPlaceholders: boolean;
  allowOutside24HourWindow: boolean;
  whatsAppPhoneNumberId: string;
  phoneNumberId: string;
  variablesJson: string;
  dataJson: string;
  useSelectedNodeTemplate: boolean;
};

@Component({
  selector: 'app-automation',
  standalone: true,
  imports: [FormsModule, TranslateModule, RouterLink, DialogModule, MetaFlowsComponent],
  templateUrl: './automation.component.html',
  styleUrl: './automation.component.scss',
})
export class AutomationComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly translate = inject(TranslateService);
  readonly langService = inject(LanguageService);
  readonly perm = inject(PermissionService);
  readonly theme = inject(ThemeService);
  @ViewChild('canvasPanel') private canvasPanelRef?: ElementRef<HTMLDivElement>;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly publishing = signal(false);
  readonly flows = signal<ConversationFlow[]>([]);
  readonly workingFlow = signal<FlowEditorState | null>(null);
  readonly selectedNodeId = signal<string | null>(null);
  readonly agents = signal<AssignableUser[]>([]);
  readonly routingTeams = signal<RoutingTeam[]>([]);
  readonly statusMessage = signal('');
  readonly errorMessage = signal('');
  readonly builderDialogVisible = signal(false);
  readonly canvasSize = signal({ width: 1480, height: 920 });
  readonly webhookContextLoading = signal(false);
  readonly webhookContextError = signal('');
  readonly webhookConnectionStatus = signal<WhatsAppConnectionStatus | null>(null);
  readonly webhookPhoneNumbers = signal<WhatsAppPhoneNumber[]>([]);
  readonly webhookDispatching = signal(false);
  readonly webhookDispatchError = signal('');
  readonly webhookDispatchSuccess = signal('');
  readonly webhookDispatchResult = signal<CustomWebhookDispatchResult | null>(null);
  readonly customWebhookForm = signal<CustomWebhookComposerForm>(this.createDefaultWebhookForm());
  readonly metaFlowStudioOpen = signal(false);

  private dragState: DragState | null = null;
  private removeMoveListener?: () => void;
  private removeUpListener?: () => void;
  private resizeObserver?: ResizeObserver;
  private readonly nodeWidth = 248;
  private readonly nodeHeight = 124;
  private readonly canvasPadding = 24;
  private readonly onWindowResize = () => this.updateCanvasViewport();

  readonly selectedNode = computed(() => {
    const flow = this.workingFlow();
    const nodeId = this.selectedNodeId();
    return flow?.definition.nodes.find(node => node.id === nodeId) ?? null;
  });

  readonly edgeLines = computed<EdgeLine[]>(() => {
    const flow = this.workingFlow();
    if (!flow) {
      return [];
    }

    const nodes = new Map(flow.definition.nodes.map(node => [node.id, node]));
    const sidesByNodeId = this.resolveNodeConnectionSides(flow, nodes);

    return flow.definition.edges.flatMap(edge => {
      const source = nodes.get(edge.sourceNodeId);
      const target = nodes.get(edge.targetNodeId);
      if (!source || !target) {
        return [];
      }

      const sourceSides = sidesByNodeId.get(source.id) ?? { incoming: 'left', outgoing: 'right' };
      const targetSides = sidesByNodeId.get(target.id) ?? { incoming: 'left', outgoing: 'right' };

      const startX = sourceSides.outgoing === 'right' ? source.x + this.nodeWidth : source.x;
      const startY = source.y + (this.nodeHeight / 2);
      const endX = targetSides.incoming === 'right' ? target.x + this.nodeWidth : target.x;
      const endY = target.y + (this.nodeHeight / 2);

      const horizontalDistance = Math.abs(endX - startX);
      const controlOffset = Math.min(240, Math.max(88, horizontalDistance * 0.48));
      const verticalDistance = Math.abs(endY - startY);

      let c1X = startX;
      let c2X = endX;
      let c1Y = startY;
      let c2Y = endY;

      if (sourceSides.outgoing !== targetSides.incoming) {
        const forward = sourceSides.outgoing === 'right';
        const backArcLift = Math.min(220, Math.max(96, 72 + (verticalDistance * 0.55)));

        c1X = forward ? startX + controlOffset : startX - controlOffset;
        c2X = forward ? endX - controlOffset : endX + controlOffset;
        c1Y = forward ? startY : startY - backArcLift;
        c2Y = forward ? endY : endY - backArcLift;
      } else {
        const sideSign = sourceSides.outgoing === 'right' ? 1 : -1;
        const sideOffset = controlOffset + 72;
        const sameSideLift = Math.min(240, Math.max(110, 90 + (verticalDistance * 0.45)));

        c1X = startX + (sideSign * sideOffset);
        c2X = endX + (sideSign * sideOffset);
        c1Y = startY - sameSideLift;
        c2Y = endY - sameSideLift;
      }

      const path = `M ${startX} ${startY} C ${c1X} ${c1Y}, ${c2X} ${c2Y}, ${endX} ${endY}`;
      const midPoint = this.cubicBezierPointAtHalf(startX, startY, c1X, c1Y, c2X, c2Y, endX, endY);

      return [{
        id: edge.id,
        path,
        labelX: midPoint.x,
        labelY: midPoint.y - 10,
        label: edge.label ?? edge.sourceHandle ?? '',
      }];
    });
  });

  readonly canvasViewBox = computed(() => `0 0 ${this.canvasSize().width} ${this.canvasSize().height}`);
  readonly customWebhookEndpoint = computed(() => this.buildCustomWebhookUrlFromApiBase());
  readonly selectedNodeTemplate = computed(() => {
    const node = this.selectedNode();
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

  ngOnInit(): void {
    this.loadFlows();
    this.loadAgents();
    this.loadTeams();
  }

  ngAfterViewInit(): void {
    this.initializeCanvasViewport();
  }

  ngOnDestroy(): void {
    this.stopDragging();
    this.resizeObserver?.disconnect();
    window.removeEventListener('resize', this.onWindowResize);
  }

  loadFlows(preferredFlowId?: number): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.api.get<ConversationFlow[]>('/conversation-flows').subscribe({
      next: flows => {
        const items = flows ?? [];
        this.flows.set(items);
        this.loading.set(false);

        if (preferredFlowId) {
          const preferred = items.find(item => item.conversationFlowId === preferredFlowId);
          if (preferred) {
            this.selectFlow(preferred);
            return;
          }
        }

        if (!this.workingFlow() && items.length > 0) {
          this.selectFlow(items[0]);
        }
      },
      error: error => {
        this.loading.set(false);
        this.errorMessage.set(error?.error?.message ?? this.t('automation.builder.feedback.loadFlowsError'));
      },
    });
  }

  loadAgents(): void {
    this.api.get<AssignableUser[]>('/users/agents').subscribe({
      next: users => this.agents.set(users ?? []),
    });
  }

  loadTeams(): void {
    this.api.get<RoutingTeam[]>('/routing/teams').subscribe({
      next: teams => this.routingTeams.set(teams ?? []),
    });
  }

  createFlow(): void {
    const startPosition = this.clampPosition(80, 140);
    const endPosition = this.clampPosition(420, 140);
    const startNode = this.buildNode('start', startPosition.x, startPosition.y);
    const endNode = this.buildNode('end', endPosition.x, endPosition.y);

    const flow: FlowEditorState = {
      conversationFlowId: 0,
      companyId: 0,
      name: this.t('automation.builder.newFlowName'),
      description: '',
      entryTriggerType: 'any_message',
      entryTriggerValue: '',
      isActive: true,
      isPublished: false,
      draftVersion: 1,
      publishedVersion: null,
      triggerCount: 0,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: null,
      publishedAtUtc: null,
      activeSessionCount: 0,
      definition: {
        nodes: [startNode, endNode],
        edges: [{
          id: this.createEdgeId(startNode.id, endNode.id, 'default'),
          sourceNodeId: startNode.id,
          targetNodeId: endNode.id,
          sourceHandle: 'default',
          label: '',
        }],
      },
      isNew: true,
    };

    this.workingFlow.set(flow);
    this.selectedNodeId.set(startNode.id);
    this.statusMessage.set('');
    this.errorMessage.set('');
    this.openBuilderWorkspace();
    this.queueCanvasClamp();
  }

  selectFlow(flow: ConversationFlow): void {
    const cloned = this.cloneFlow(flow);
    this.workingFlow.set(cloned);
    this.selectedNodeId.set(cloned.definition.nodes[0]?.id ?? null);
    this.statusMessage.set('');
    this.errorMessage.set('');
    this.openBuilderWorkspace();
    this.queueCanvasClamp();
  }

  private validateFlowBeforePersist(flow: FlowEditorState): string | null {
    const invalidMetaNode = flow.definition.nodes.find(node =>
      node.type === 'meta_flow' && !(node.metaFlowId || '').trim());

    if (invalidMetaNode) {
      return this.t('automation.builder.metaFlowBindingRequired', {
        node: invalidMetaNode.title || invalidMetaNode.id,
      });
    }

    const invalidMetaActionNode = flow.definition.nodes.find(node =>
      node.type === 'meta_flow' && (node.metaFlowAction || 'navigate').trim().toLowerCase() !== 'navigate');

    if (invalidMetaActionNode) {
      return this.t('automation.builder.metaFlowActionRestricted', {
        node: invalidMetaActionNode.title || invalidMetaActionNode.id,
      });
    }

    const invalidMetaScreenNode = flow.definition.nodes.find(node =>
      node.type === 'meta_flow' && !(node.metaFlowScreen || '').trim());

    if (invalidMetaScreenNode) {
      return this.t('automation.builder.metaFlowScreenRequired', {
        node: invalidMetaScreenNode.title || invalidMetaScreenNode.id,
      });
    }

    const invalidAssignUserNode = flow.definition.nodes.find(node => {
      if (node.type !== 'assign_agent') {
        return false;
      }

      return (node.assignMode || 'auto') === 'specific' && !node.assignToUserId;
    });
    if (invalidAssignUserNode) {
      return this.t('automation.builder.assignUserRequired', {
        node: invalidAssignUserNode.title || invalidAssignUserNode.id,
      });
    }

    const invalidAssignTeamNode = flow.definition.nodes.find(node => {
      if (node.type !== 'assign_agent') {
        return false;
      }

      const mode = (node.assignMode || 'auto').toLowerCase();
      if (mode !== 'specific_team' && mode !== 'team_auto') {
        return false;
      }

      return !node.assignToTeamId;
    });
    if (invalidAssignTeamNode) {
      return this.t('automation.builder.assignTeamRequired', {
        node: invalidAssignTeamNode.title || invalidAssignTeamNode.id,
      });
    }

    return null;
  }

  saveFlow(): void {
    const flow = this.workingFlow();
    if (!flow) {
      return;
    }

    const validationError = this.validateFlowBeforePersist(flow);
    if (validationError) {
      this.errorMessage.set(validationError);
      this.statusMessage.set('');
      return;
    }

    this.saving.set(true);
    this.errorMessage.set('');
    this.statusMessage.set('');

    const payload: ConversationFlowUpsertRequest = {
      name: flow.name.trim(),
      description: flow.description?.trim() || undefined,
      entryTriggerType: flow.entryTriggerType,
      entryTriggerValue: flow.entryTriggerType === 'any_message' ? undefined : (flow.entryTriggerValue?.trim() || undefined),
      isActive: flow.isActive,
      definition: flow.definition,
    };

    const request$ = flow.conversationFlowId > 0
      ? this.api.put<ConversationFlow>(`/conversation-flows/${flow.conversationFlowId}`, payload)
      : this.api.post<ConversationFlow>('/conversation-flows', payload);

    request$.subscribe({
      next: saved => {
        this.saving.set(false);
        this.statusMessage.set(this.t('automation.builder.feedback.saveSuccess'));
        this.loadFlows(saved.conversationFlowId);
      },
      error: error => {
        this.saving.set(false);
        this.errorMessage.set(error?.error?.message ?? this.t('automation.builder.feedback.saveError'));
      },
    });
  }

  publishFlow(): void {
    const flow = this.workingFlow();
    if (!flow || flow.conversationFlowId <= 0) {
      return;
    }

    const validationError = this.validateFlowBeforePersist(flow);
    if (validationError) {
      this.errorMessage.set(validationError);
      this.statusMessage.set('');
      return;
    }

    this.publishing.set(true);
    this.errorMessage.set('');
    this.statusMessage.set('');

    this.api.post<ConversationFlow>(`/conversation-flows/${flow.conversationFlowId}/publish`).subscribe({
      next: saved => {
        this.publishing.set(false);
        this.statusMessage.set(this.t('automation.builder.feedback.publishSuccess'));
        this.loadFlows(saved.conversationFlowId);
      },
      error: error => {
        this.publishing.set(false);
        this.errorMessage.set(error?.error?.message ?? this.t('automation.builder.feedback.publishError'));
      },
    });
  }

  toggleFlow(): void {
    const flow = this.workingFlow();
    if (!flow || flow.conversationFlowId <= 0) {
      return;
    }

    this.api.post<boolean>(`/conversation-flows/${flow.conversationFlowId}/toggle`).subscribe({
      next: isActive => {
        this.mutateFlow(next => {
          next.isActive = isActive;
        });
        this.statusMessage.set(this.t(isActive
          ? 'automation.builder.feedback.flowActivated'
          : 'automation.builder.feedback.flowPaused'));
        this.loadFlows(flow.conversationFlowId);
      },
      error: error => {
        this.errorMessage.set(error?.error?.message ?? this.t('automation.builder.feedback.toggleError'));
      },
    });
  }

  deleteFlow(flowId: number): void {
    if (!confirm(this.t('automation.builder.deleteFlowConfirm'))) {
      return;
    }

    this.api.delete<boolean>(`/conversation-flows/${flowId}`).subscribe({
      next: () => {
        this.statusMessage.set(this.t('automation.builder.feedback.deleteSuccess'));
        this.workingFlow.set(null);
        this.selectedNodeId.set(null);
        this.loadFlows();
      },
      error: error => {
        this.errorMessage.set(error?.error?.message ?? this.t('automation.builder.feedback.deleteError'));
      },
    });
  }

  addNode(type: string): void {
    const flow = this.workingFlow();
    if (!flow) {
      return;
    }

    const index = flow.definition.nodes.length;
    const nextPosition = this.clampPosition(140 + ((index % 3) * 320), 120 + (Math.floor(index / 3) * 180));
    const node = this.buildNode(type, nextPosition.x, nextPosition.y);

    this.mutateFlow(next => {
      next.definition.nodes.push(node);
      if (this.selectedNodeId()) {
        this.upsertEdge(next.definition.edges, this.selectedNodeId()!, node.id, 'default', '');
      }
    });

    this.selectedNodeId.set(node.id);

    if (type === 'meta_flow') {
      this.metaFlowStudioOpen.set(true);
    }
  }

  removeNode(nodeId: string): void {
    this.mutateFlow(flow => {
      flow.definition.nodes = flow.definition.nodes.filter(node => node.id !== nodeId || node.type === 'start');
      flow.definition.edges = flow.definition.edges.filter(edge => edge.sourceNodeId !== nodeId && edge.targetNodeId !== nodeId);
    });

    if (this.selectedNodeId() === nodeId) {
      this.selectedNodeId.set(this.workingFlow()?.definition.nodes[0]?.id ?? null);
    }
  }

  updateFlowField(field: 'name' | 'description' | 'entryTriggerType' | 'entryTriggerValue', value: string): void {
    this.mutateFlow(flow => {
      if (field === 'entryTriggerValue' && flow.entryTriggerType === 'any_message') {
        flow.entryTriggerValue = '';
        return;
      }

      (flow[field] as string | null) = value;
      if (field === 'entryTriggerType' && value === 'any_message') {
        flow.entryTriggerValue = '';
      }
    });
  }

  updateNodeField(field: keyof ConversationFlowNode, value: unknown): void {
    const nodeId = this.selectedNodeId();
    if (!nodeId) {
      return;
    }

    this.mutateFlow(flow => {
      const node = flow.definition.nodes.find(item => item.id === nodeId);
      if (!node) {
        return;
      }

      (node[field] as unknown) = value;
    });
  }

  openMetaFlowStudio(): void {
    this.metaFlowStudioOpen.set(true);
  }

  closeMetaFlowStudio(): void {
    this.metaFlowStudioOpen.set(false);
  }

  applyMetaFlowFromStudio(selection: MetaFlowSelection): void {
    if (!selection?.id) {
      return;
    }

    const status = (selection.status ?? '').toLowerCase();
    const suggestedMode = status.includes('draft') ? 'draft' : 'published';

    this.mutateFlow(flow => {
      const node = flow.definition.nodes.find(item => item.id === this.selectedNodeId());
      if (!node || node.type !== 'meta_flow') {
        return;
      }

      node.metaFlowId = selection.id;
      node.metaFlowName = selection.name ?? node.metaFlowName ?? '';
      node.metaFlowScreen = selection.firstScreenId ?? '';
      if (!node.metaFlowMode) {
        node.metaFlowMode = suggestedMode;
      }
      node.metaFlowAction = 'navigate';
      if (!node.metaFlowCta) {
        node.metaFlowCta = this.t('automation.builder.nodeDefaults.metaFlowCta');
      }
    });

    this.metaFlowStudioOpen.set(false);
    this.statusMessage.set(this.t('automation.builder.metaFlowStudioPicked', {
      flow: selection.name || selection.id,
    }));
    this.errorMessage.set('');
  }

  clearNodeMetaFlowBinding(): void {
    this.mutateFlow(flow => {
      const node = flow.definition.nodes.find(item => item.id === this.selectedNodeId());
      if (!node || node.type !== 'meta_flow') {
        return;
      }

      node.metaFlowId = '';
      node.metaFlowName = '';
    });
  }

  updateOptionField(nodeId: string, optionIndex: number, field: keyof ConversationFlowOption, value: string): void {
    this.mutateFlow(flow => {
      const node = flow.definition.nodes.find(item => item.id === nodeId);
      const option = node?.options[optionIndex];
      if (!node || !option) {
        return;
      }

      if (field === 'id') {
        const nextId = this.sanitizeId(value);
        const previousId = option.id;
        option.id = nextId;
        flow.definition.edges
          .filter(edge => edge.sourceNodeId === nodeId && edge.sourceHandle === previousId)
          .forEach(edge => edge.sourceHandle = nextId);
        return;
      }

      (option[field] as string | null) = value;
      if (field === 'label' && !option.id) {
        option.id = this.sanitizeId(value);
      }
    });
  }

  addOption(nodeId: string): void {
    this.mutateFlow(flow => {
      const node = flow.definition.nodes.find(item => item.id === nodeId);
      if (!node) {
        return;
      }

      const nextNumber = node.options.length + 1;
      node.options.push({
        id: `option_${nextNumber}`,
        label: this.t('automation.builder.optionLabel', { count: nextNumber }),
        description: '',
      });
    });
  }

  removeOption(nodeId: string, optionIndex: number): void {
    this.mutateFlow(flow => {
      const node = flow.definition.nodes.find(item => item.id === nodeId);
      if (!node) {
        return;
      }

      const removed = node.options.splice(optionIndex, 1)[0];
      if (!removed) {
        return;
      }

      flow.definition.edges = flow.definition.edges.filter(edge => !(edge.sourceNodeId === nodeId && edge.sourceHandle === removed.id));
    });
  }

  getDefaultTarget(nodeId: string): string {
    return this.workingFlow()?.definition.edges.find(edge => edge.sourceNodeId === nodeId && (edge.sourceHandle === 'default' || !edge.sourceHandle))?.targetNodeId ?? '';
  }

  setDefaultTarget(nodeId: string, targetNodeId: string): void {
    this.mutateFlow(flow => {
      if (!targetNodeId) {
        flow.definition.edges = flow.definition.edges.filter(edge => !(edge.sourceNodeId === nodeId && (edge.sourceHandle === 'default' || !edge.sourceHandle)));
        return;
      }

      this.upsertEdge(flow.definition.edges, nodeId, targetNodeId, 'default', '');
    });
  }

  getOptionTarget(nodeId: string, optionId: string): string {
    return this.workingFlow()?.definition.edges.find(edge => edge.sourceNodeId === nodeId && edge.sourceHandle === optionId)?.targetNodeId ?? '';
  }

  setOptionTarget(nodeId: string, optionId: string, targetNodeId: string): void {
    this.mutateFlow(flow => {
      if (!targetNodeId) {
        flow.definition.edges = flow.definition.edges.filter(edge => !(edge.sourceNodeId === nodeId && edge.sourceHandle === optionId));
        return;
      }

      this.upsertEdge(flow.definition.edges, nodeId, targetNodeId, optionId, optionId);
    });
  }

  selectNode(nodeId: string): void {
    this.selectedNodeId.set(nodeId);
  }

  startDrag(event: PointerEvent, nodeId: string): void {
    if (event.button !== 0) {
      return;
    }

    const node = this.workingFlow()?.definition.nodes.find(item => item.id === nodeId);
    if (!node) {
      return;
    }

    this.dragState = {
      nodeId,
      startClientX: event.clientX,
      startClientY: event.clientY,
      originalX: node.x,
      originalY: node.y,
    };

    const onMove = (moveEvent: PointerEvent) => {
      if (!this.dragState) {
        return;
      }

      const deltaX = moveEvent.clientX - this.dragState.startClientX;
      const deltaY = moveEvent.clientY - this.dragState.startClientY;
      this.mutateFlow(flow => {
        const target = flow.definition.nodes.find(item => item.id === nodeId);
        if (!target) {
          return;
        }

        const nextPosition = this.clampPosition(this.dragState!.originalX + deltaX, this.dragState!.originalY + deltaY);
        target.x = nextPosition.x;
        target.y = nextPosition.y;
      });
    };

    const onUp = () => this.stopDragging();
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);

    this.removeMoveListener = () => window.removeEventListener('pointermove', onMove);
    this.removeUpListener = () => window.removeEventListener('pointerup', onUp);
  }

  stopDragging(): void {
    this.dragState = null;
    this.removeMoveListener?.();
    this.removeUpListener?.();
    this.removeMoveListener = undefined;
    this.removeUpListener = undefined;
  }

  availableTargets(nodeId: string): ConversationFlowNode[] {
    return (this.workingFlow()?.definition.nodes ?? []).filter(node => node.id !== nodeId);
  }

  supportsDefaultTarget(type: string): boolean {
    return ['start', 'message', 'capture_text', 'meta_flow', 'external_link'].includes(type);
  }

  showOptionTargets(type: string): boolean {
    return type === 'menu';
  }

  canDeleteNode(type: string): boolean {
    return type !== 'start';
  }

  nodeTypeLabel(type: string): string {
    return this.t(this.nodeTypeKey(type));
  }

  nodePreview(node: ConversationFlowNode): string {
    if (node.type === 'menu') {
      return this.t('automation.builder.previews.optionsCount', { count: node.options.length });
    }

    if (node.type === 'meta_flow') {
      const configured = (node.metaFlowId || node.metaFlowName || '').trim();
      return configured || this.t('automation.builder.previews.metaFlowUnconfigured');
    }

    if (node.type === 'assign_agent') {
      if (node.assignMode === 'specific') {
        const agentName = this.agents().find(agent => agent.companyUserId === node.assignToUserId)?.fullName
          ?? `#${node.assignToUserId ?? '-'}`;

        return this.t('automation.builder.previews.assignSpecific', { target: agentName });
      }

      if (node.assignMode === 'specific_team') {
        const teamName = this.routingTeams().find(team => team.routingTeamId === node.assignToTeamId)?.name
          ?? `#${node.assignToTeamId ?? '-'}`;
        return this.t('automation.builder.previews.assignTeamOnly', { target: teamName });
      }

      if (node.assignMode === 'team_auto') {
        const teamName = this.routingTeams().find(team => team.routingTeamId === node.assignToTeamId)?.name
          ?? `#${node.assignToTeamId ?? '-'}`;
        return this.t('automation.builder.previews.assignTeamAuto', { target: teamName });
      }

      return this.t('automation.builder.previews.assignAuto');
    }

    if (node.type === 'external_link') {
      return node.url ?? this.t('automation.builder.previews.externalUrl');
    }

    return node.bodyText?.slice(0, 60) ?? node.title;
  }

  toNumber(value: string | number | null | undefined): number {
    return Number(value ?? 0);
  }

  openBuilderWorkspace(): void {
    this.builderDialogVisible.set(true);
    this.queueCanvasClamp();
  }

  onBuilderDialogVisibleChange(visible: boolean): void {
    this.builderDialogVisible.set(visible);
    if (visible) {
      this.queueCanvasClamp();
    }
  }

  handleBuilderDialogShown(): void {
    this.queueCanvasClamp();
  }

  onMetaFlowStudioVisibleChange(visible: boolean): void {
    this.metaFlowStudioOpen.set(visible);
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
          this.updateWebhookField('token', token);
        }
        complete();
      },
      error: () => {
        this.webhookContextError.set(this.t('automation.builder.webhook.feedback.contextLoadError'));
        complete();
      },
    });

    this.api.get<WhatsAppPhoneNumber[]>('/phone-numbers').subscribe({
      next: numbers => {
        const list = numbers ?? [];
        this.webhookPhoneNumbers.set(list);

        const defaultPhone = list.find(item => item.isDefault) ?? list[0];
        if (defaultPhone) {
          this.customWebhookForm.update(form => ({
            ...form,
            whatsAppPhoneNumberId: form.whatsAppPhoneNumberId || String(defaultPhone.whatsAppPhoneNumberId),
            phoneNumberId: form.phoneNumberId || defaultPhone.phoneNumberId,
          }));
        }

        complete();
      },
      error: () => {
        this.webhookContextError.set(this.t('automation.builder.webhook.feedback.contextLoadError'));
        complete();
      },
    });
  }

  updateWebhookField<K extends keyof CustomWebhookComposerForm>(field: K, value: CustomWebhookComposerForm[K]): void {
    this.customWebhookForm.update(form => ({
      ...form,
      [field]: value,
    }));
    this.webhookDispatchError.set('');
    this.webhookDispatchSuccess.set('');
    this.webhookDispatchResult.set(null);
  }

  copyWebhookValue(value: string, successKey: string): void {
    if (!value) {
      return;
    }

    navigator.clipboard.writeText(value).then(
      () => this.webhookDispatchSuccess.set(this.t(successKey)),
      () => this.webhookDispatchError.set(this.t('automation.builder.webhook.feedback.copyFailed')),
    );
  }

  useSelectedNodeAsTemplate(): void {
    const template = this.selectedNodeTemplate();
    if (!template) {
      this.webhookDispatchError.set(this.t('automation.builder.webhook.feedback.noNodeTemplate'));
      return;
    }

    this.customWebhookForm.update(form => ({
      ...form,
      message: template,
      useSelectedNodeTemplate: true,
    }));
    this.webhookDispatchError.set('');
  }

  dispatchCustomWebhook(): void {
    const form = this.customWebhookForm();
    const token = form.token.trim();
    const to = form.to.trim();
    const template = this.effectiveTemplate();

    this.webhookDispatchError.set('');
    this.webhookDispatchSuccess.set('');
    this.webhookDispatchResult.set(null);

    if (!token) {
      this.webhookDispatchError.set(this.t('automation.builder.webhook.feedback.tokenRequired'));
      return;
    }

    if (!to) {
      this.webhookDispatchError.set(this.t('automation.builder.webhook.feedback.toRequired'));
      return;
    }

    if (!template) {
      this.webhookDispatchError.set(this.t('automation.builder.webhook.feedback.templateRequired'));
      return;
    }

    const parsedVariables = this.tryParseObjectJson(form.variablesJson, 'automation.builder.webhook.feedback.variablesJsonInvalid');
    if (!parsedVariables) {
      return;
    }

    const parsedData = this.tryParseOptionalJson(form.dataJson, 'automation.builder.webhook.feedback.dataJsonInvalid');
    if (parsedData === undefined && form.dataJson.trim()) {
      return;
    }

    const payload: CustomWebhookDispatchRequest = {
      to,
      message: template,
      contactName: form.contactName.trim() || undefined,
      strictVariables: form.strictVariables,
      keepUnresolvedPlaceholders: form.keepUnresolvedPlaceholders,
      allowOutside24HourWindow: form.allowOutside24HourWindow,
      variables: parsedVariables,
      data: parsedData,
    };

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
          this.webhookDispatchSuccess.set(this.t('automation.builder.webhook.feedback.dispatchSuccess'));
          return;
        }

        const details = response.error?.details ? ` ${response.error.details}` : '';
        this.webhookDispatchError.set((response.message || this.t('automation.builder.webhook.feedback.dispatchError')) + details);
      },
      error: error => {
        this.webhookDispatching.set(false);
        this.webhookDispatchError.set(error?.error?.message ?? this.t('automation.builder.webhook.feedback.dispatchError'));
      },
    });
  }

  private tryParseObjectJson(raw: string, errorKey: string): Record<string, unknown> | null {
    const input = raw.trim();
    if (!input) {
      return {};
    }

    try {
      const parsed = JSON.parse(input) as unknown;
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        return parsed as Record<string, unknown>;
      }

      this.webhookDispatchError.set(this.t(errorKey));
      return null;
    } catch {
      this.webhookDispatchError.set(this.t(errorKey));
      return null;
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

  private mutateFlow(mutator: (flow: FlowEditorState) => void): void {
    this.workingFlow.update(flow => {
      if (!flow) {
        return flow;
      }

      const next = this.cloneFlow(flow);
      mutator(next);
      return next;
    });
  }

  private cloneFlow(flow: ConversationFlow): FlowEditorState {
    return JSON.parse(JSON.stringify(flow)) as FlowEditorState;
  }

  private initializeCanvasViewport(): void {
    const panel = this.canvasPanelRef?.nativeElement;
    if (!panel) {
      return;
    }

    this.updateCanvasViewport();
    this.resizeObserver = new ResizeObserver(() => this.updateCanvasViewport());
    this.resizeObserver.observe(panel);
    window.addEventListener('resize', this.onWindowResize);
  }

  private updateCanvasViewport(): void {
    const panel = this.canvasPanelRef?.nativeElement;
    if (!panel) {
      return;
    }

    const rect = panel.getBoundingClientRect();
    const minHeight = window.innerWidth < 1280 ? 540 : 680;
    const visibleWidth = Math.max(860, Math.floor(rect.width) - 2);
    const visibleHeight = Math.max(minHeight, Math.floor(window.innerHeight - rect.top - 28));
    const content = this.calculateCanvasContentSize(this.workingFlow()?.definition.nodes ?? []);
    const nextWidth = Math.max(visibleWidth, content.width);
    const nextHeight = Math.max(visibleHeight, content.height);
    this.canvasSize.set({ width: nextWidth, height: nextHeight });
    this.clampAllNodesIntoViewport();
  }

  private clampAllNodesIntoViewport(): void {
    this.mutateFlow(flow => {
      flow.definition.nodes = flow.definition.nodes.map(node => {
        const nextPosition = this.clampPosition(node.x, node.y);
        if (nextPosition.x === node.x && nextPosition.y === node.y) {
          return node;
        }

        return {
          ...node,
          x: nextPosition.x,
          y: nextPosition.y,
        };
      });
    });
  }

  private clampPosition(x: number, y: number): { x: number; y: number } {
    const size = this.canvasSize();
    const maxX = Math.max(this.canvasPadding, size.width - this.nodeWidth - this.canvasPadding);
    const maxY = Math.max(this.canvasPadding, size.height - this.nodeHeight - this.canvasPadding);

    return {
      x: Math.min(Math.max(this.canvasPadding, x), maxX),
      y: Math.min(Math.max(this.canvasPadding, y), maxY),
    };
  }

  private queueCanvasClamp(): void {
    window.requestAnimationFrame(() => this.updateCanvasViewport());
  }

  private calculateCanvasContentSize(nodes: ConversationFlowNode[]): { width: number; height: number } {
    if (nodes.length === 0) {
      return { width: 1280, height: 760 };
    }

    let maxRight = 0;
    let maxBottom = 0;
    for (const node of nodes) {
      maxRight = Math.max(maxRight, node.x + this.nodeWidth);
      maxBottom = Math.max(maxBottom, node.y + this.nodeHeight);
    }

    return {
      width: Math.max(1280, maxRight + this.canvasPadding + 320),
      height: Math.max(760, maxBottom + this.canvasPadding + 200),
    };
  }

  private buildNode(type: string, x: number, y: number): ConversationFlowNode {
    const base: ConversationFlowNode = {
      id: `${type}_${Math.random().toString(36).slice(2, 8)}`,
      type,
      title: this.nodeTypeLabel(type),
      x,
      y,
      bodyText: '',
      footerText: '',
      buttonText: '',
      variableName: '',
      invalidInputMessage: '',
      menuPresentation: 'buttons',
      options: [],
      sections: [],
      assignMode: 'auto',
      assignToUserId: null,
      assignToTeamId: null,
      updateContactOwner: false,
      assignReason: '',
      url: '',
      linkLabel: '',
      metaFlowId: '',
      metaFlowName: '',
      metaFlowCta: '',
      metaFlowMode: 'published',
      metaFlowAction: 'navigate',
      metaFlowScreen: '',
      metaFlowDataJson: '',
    };

    if (type === 'start') {
      base.title = this.t('automation.builder.nodeDefaults.startTitle');
    } else if (type === 'message') {
      base.title = this.t('automation.builder.nodeDefaults.messageTitle');
      base.bodyText = this.t('automation.builder.nodeDefaults.messageBody');
    } else if (type === 'menu') {
      base.title = this.t('automation.builder.nodeDefaults.menuTitle');
      base.bodyText = this.t('automation.builder.nodeDefaults.menuBody');
      base.options = [
        { id: 'option_1', label: this.t('automation.builder.nodeDefaults.optionSales'), description: '' },
        { id: 'option_2', label: this.t('automation.builder.nodeDefaults.optionSupport'), description: '' },
      ];
      base.invalidInputMessage = this.t('automation.builder.nodeDefaults.menuInvalid');
    } else if (type === 'capture_text') {
      base.title = this.t('automation.builder.nodeDefaults.captureTitle');
      base.bodyText = this.t('automation.builder.nodeDefaults.captureBody');
      base.variableName = 'customer_input';
      base.invalidInputMessage = this.t('automation.builder.nodeDefaults.captureInvalid');
    } else if (type === 'meta_flow') {
      base.title = this.t('automation.builder.nodeDefaults.metaFlowTitle');
      base.bodyText = this.t('automation.builder.nodeDefaults.metaFlowBody');
      base.footerText = this.t('automation.builder.nodeDefaults.metaFlowFooter');
      base.variableName = 'meta_flow_response';
      base.metaFlowCta = this.t('automation.builder.nodeDefaults.metaFlowCta');
      base.metaFlowMode = 'published';
      base.metaFlowAction = 'navigate';
      base.metaFlowScreen = '';
      base.metaFlowDataJson = '{\n  "source": "flow_builder"\n}';
      base.invalidInputMessage = this.t('automation.builder.nodeDefaults.metaFlowInvalid');
    } else if (type === 'assign_agent') {
      base.title = this.t('automation.builder.nodeDefaults.assignTitle');
      base.assignMode = 'auto';
      base.assignReason = 'FLOW_HANDOFF';
    } else if (type === 'external_link') {
      base.title = this.t('automation.builder.nodeDefaults.linkTitle');
      base.bodyText = this.t('automation.builder.nodeDefaults.linkBody');
      base.url = 'https://example.com';
    } else if (type === 'end') {
      base.title = this.t('automation.builder.nodeDefaults.endTitle');
      base.bodyText = this.t('automation.builder.nodeDefaults.endBody');
    }

    return base;
  }

  private upsertEdge(edges: ConversationFlowEdge[], sourceNodeId: string, targetNodeId: string, sourceHandle: string, label: string): void {
    const existing = edges.find(edge => edge.sourceNodeId === sourceNodeId && (edge.sourceHandle ?? 'default') === sourceHandle);
    if (existing) {
      existing.targetNodeId = targetNodeId;
      existing.label = label;
      return;
    }

    edges.push({
      id: this.createEdgeId(sourceNodeId, targetNodeId, sourceHandle),
      sourceNodeId,
      targetNodeId,
      sourceHandle,
      label,
    });
  }

  private createEdgeId(sourceNodeId: string, targetNodeId: string, sourceHandle: string): string {
    return `${sourceNodeId}_${sourceHandle}_${targetNodeId}`;
  }

  private resolveNodeConnectionSides(
    flow: ConversationFlow,
    nodes: Map<string, ConversationFlowNode>,
  ): Map<string, NodeConnectionSides> {
    const predecessorsByNodeId = new Map<string, ConversationFlowNode[]>();
    const successorsByNodeId = new Map<string, ConversationFlowNode[]>();

    for (const edge of flow.definition.edges) {
      const sourceNode = nodes.get(edge.sourceNodeId);
      const targetNode = nodes.get(edge.targetNodeId);
      if (!sourceNode || !targetNode) {
        continue;
      }

      const targetPredecessors = predecessorsByNodeId.get(targetNode.id) ?? [];
      targetPredecessors.push(sourceNode);
      predecessorsByNodeId.set(targetNode.id, targetPredecessors);

      const sourceSuccessors = successorsByNodeId.get(sourceNode.id) ?? [];
      sourceSuccessors.push(targetNode);
      successorsByNodeId.set(sourceNode.id, sourceSuccessors);
    }

    const sidesByNodeId = new Map<string, NodeConnectionSides>();
    for (const node of flow.definition.nodes) {
      const predecessors = predecessorsByNodeId.get(node.id) ?? [];
      const successors = successorsByNodeId.get(node.id) ?? [];

      let incoming: HorizontalSide = 'left';
      if (predecessors.length > 0) {
        const averagePredecessorX = predecessors.reduce((sum, predecessor) => sum + predecessor.x, 0) / predecessors.length;
        incoming = averagePredecessorX <= node.x ? 'left' : 'right';
      } else if (successors.length > 0) {
        const averageSuccessorX = successors.reduce((sum, successor) => sum + successor.x, 0) / successors.length;
        const inferredOutgoing: HorizontalSide = averageSuccessorX >= node.x ? 'right' : 'left';
        incoming = this.oppositeSide(inferredOutgoing);
      }

      sidesByNodeId.set(node.id, {
        incoming,
        outgoing: this.oppositeSide(incoming),
      });
    }

    return sidesByNodeId;
  }

  private oppositeSide(side: HorizontalSide): HorizontalSide {
    return side === 'left' ? 'right' : 'left';
  }

  private cubicBezierPointAtHalf(
    x0: number,
    y0: number,
    x1: number,
    y1: number,
    x2: number,
    y2: number,
    x3: number,
    y3: number,
  ): { x: number; y: number } {
    return {
      x: (x0 + (3 * x1) + (3 * x2) + x3) / 8,
      y: (y0 + (3 * y1) + (3 * y2) + y3) / 8,
    };
  }

  private sanitizeId(value: string): string {
    return value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '_')
      .replace(/^_+|_+$/g, '') || `option_${Math.random().toString(36).slice(2, 6)}`;
  }

  triggerTypeKey(type: string): string {
    switch (type) {
      case 'any_message':
        return 'automation.anyMessage';
      case 'keyword':
        return 'automation.keyword';
      case 'contains':
        return 'automation.contains';
      case 'exact':
        return 'automation.exact';
      default:
        return 'automation.builder.triggerTypes.any_message';
    }
  }

  nodeTypeKey(type: string): string {
    switch (type) {
      case 'start':
        return 'automation.builder.nodeTypes.start';
      case 'message':
        return 'automation.builder.nodeTypes.message';
      case 'menu':
        return 'automation.builder.nodeTypes.menu';
      case 'capture_text':
        return 'automation.builder.nodeTypes.capture_text';
      case 'meta_flow':
        return 'automation.builder.nodeTypes.meta_flow';
      case 'assign_agent':
        return 'automation.builder.nodeTypes.assign_agent';
      case 'external_link':
        return 'automation.builder.nodeTypes.external_link';
      case 'end':
        return 'automation.builder.nodeTypes.end';
      default:
        return type;
    }
  }

  private createDefaultWebhookForm(): CustomWebhookComposerForm {
    return {
      token: '',
      to: '',
      contactName: '',
      message: 'Hello {{customer_name}}, your request {{request_id|default:N/A}} is now {{status|upper}}.',
      strictVariables: true,
      keepUnresolvedPlaceholders: false,
      allowOutside24HourWindow: false,
      whatsAppPhoneNumberId: '',
      phoneNumberId: '',
      variablesJson: '{\n  "request_id": "A-102",\n  "status": "ready",\n  "customer_name": "Customer"\n}',
      dataJson: '',
      useSelectedNodeTemplate: true,
    };
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
