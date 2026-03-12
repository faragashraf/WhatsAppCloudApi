import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AssignableUser,
  ConversationFlow,
  ConversationFlowEdge,
  ConversationFlowNode,
  ConversationFlowOption,
  ConversationFlowUpsertRequest,
} from '../../../core/models';
import { ApiService, PermissionService } from '../../../core/services';

type FlowEditorState = ConversationFlow & {
  isNew?: boolean;
};

type EdgeLine = {
  id: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  label: string;
};

type DragState = {
  nodeId: string;
  startClientX: number;
  startClientY: number;
  originalX: number;
  originalY: number;
};

@Component({
  selector: 'app-automation',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './automation.component.html',
  styleUrl: './automation.component.scss',
})
export class AutomationComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  readonly perm = inject(PermissionService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly publishing = signal(false);
  readonly flows = signal<ConversationFlow[]>([]);
  readonly workingFlow = signal<FlowEditorState | null>(null);
  readonly selectedNodeId = signal<string | null>(null);
  readonly agents = signal<AssignableUser[]>([]);
  readonly statusMessage = signal('');
  readonly errorMessage = signal('');

  private dragState: DragState | null = null;
  private removeMoveListener?: () => void;
  private removeUpListener?: () => void;

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
    return flow.definition.edges.flatMap(edge => {
      const source = nodes.get(edge.sourceNodeId);
      const target = nodes.get(edge.targetNodeId);
      if (!source || !target) {
        return [];
      }

      return [{
        id: edge.id,
        x1: source.x + 248,
        y1: source.y + 62,
        x2: target.x,
        y2: target.y + 62,
        label: edge.label ?? edge.sourceHandle ?? '',
      }];
    });
  });

  ngOnInit(): void {
    this.loadFlows();
    this.loadAgents();
  }

  ngOnDestroy(): void {
    this.stopDragging();
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
        this.errorMessage.set(error?.error?.message ?? 'Failed to load flows.');
      },
    });
  }

  loadAgents(): void {
    this.api.get<AssignableUser[]>('/users/agents').subscribe({
      next: users => this.agents.set(users ?? []),
    });
  }

  createFlow(): void {
    const startNode = this.buildNode('start', 80, 140);
    const endNode = this.buildNode('end', 420, 140);

    const flow: FlowEditorState = {
      conversationFlowId: 0,
      companyId: 0,
      name: 'New flow',
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
  }

  selectFlow(flow: ConversationFlow): void {
    const cloned = this.cloneFlow(flow);
    this.workingFlow.set(cloned);
    this.selectedNodeId.set(cloned.definition.nodes[0]?.id ?? null);
    this.statusMessage.set('');
    this.errorMessage.set('');
  }

  saveFlow(): void {
    const flow = this.workingFlow();
    if (!flow) {
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
        this.statusMessage.set('Flow saved successfully.');
        this.loadFlows(saved.conversationFlowId);
      },
      error: error => {
        this.saving.set(false);
        this.errorMessage.set(error?.error?.message ?? 'Failed to save flow.');
      },
    });
  }

  publishFlow(): void {
    const flow = this.workingFlow();
    if (!flow || flow.conversationFlowId <= 0) {
      return;
    }

    this.publishing.set(true);
    this.errorMessage.set('');
    this.statusMessage.set('');

    this.api.post<ConversationFlow>(`/conversation-flows/${flow.conversationFlowId}/publish`).subscribe({
      next: saved => {
        this.publishing.set(false);
        this.statusMessage.set('Published version updated.');
        this.loadFlows(saved.conversationFlowId);
      },
      error: error => {
        this.publishing.set(false);
        this.errorMessage.set(error?.error?.message ?? 'Failed to publish flow.');
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
        this.statusMessage.set(isActive ? 'Flow activated.' : 'Flow paused.');
        this.loadFlows(flow.conversationFlowId);
      },
      error: error => {
        this.errorMessage.set(error?.error?.message ?? 'Failed to update flow status.');
      },
    });
  }

  deleteFlow(flowId: number): void {
    if (!confirm('Delete this flow?')) {
      return;
    }

    this.api.delete<boolean>(`/conversation-flows/${flowId}`).subscribe({
      next: () => {
        this.statusMessage.set('Flow deleted.');
        this.workingFlow.set(null);
        this.selectedNodeId.set(null);
        this.loadFlows();
      },
      error: error => {
        this.errorMessage.set(error?.error?.message ?? 'Failed to delete flow.');
      },
    });
  }

  addNode(type: string): void {
    const flow = this.workingFlow();
    if (!flow) {
      return;
    }

    const index = flow.definition.nodes.length;
    const node = this.buildNode(type, 140 + ((index % 3) * 320), 120 + (Math.floor(index / 3) * 180));

    this.mutateFlow(next => {
      next.definition.nodes.push(node);
      if (this.selectedNodeId()) {
        this.upsertEdge(next.definition.edges, this.selectedNodeId()!, node.id, 'default', '');
      }
    });

    this.selectedNodeId.set(node.id);
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
        label: `Option ${nextNumber}`,
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

        target.x = Math.max(0, this.dragState!.originalX + deltaX);
        target.y = Math.max(0, this.dragState!.originalY + deltaY);
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
    return ['start', 'message', 'capture_text', 'external_link'].includes(type);
  }

  showOptionTargets(type: string): boolean {
    return type === 'menu';
  }

  canDeleteNode(type: string): boolean {
    return type !== 'start';
  }

  nodeTypeLabel(type: string): string {
    switch (type) {
      case 'start': return 'Start';
      case 'message': return 'Message';
      case 'menu': return 'Menu';
      case 'capture_text': return 'Capture';
      case 'assign_agent': return 'Assign';
      case 'external_link': return 'Link';
      case 'end': return 'End';
      default: return type;
    }
  }

  nodePreview(node: ConversationFlowNode): string {
    if (node.type === 'menu') {
      return `${node.options.length} option(s)`;
    }

    if (node.type === 'assign_agent') {
      return node.assignMode === 'specific'
        ? `Assign to #${node.assignToUserId ?? '-'}`
        : 'Auto assign';
    }

    if (node.type === 'external_link') {
      return node.url ?? 'External URL';
    }

    return node.bodyText?.slice(0, 60) ?? node.title;
  }

  toNumber(value: string | number | null | undefined): number {
    return Number(value ?? 0);
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
      updateContactOwner: false,
      assignReason: '',
      url: '',
      linkLabel: '',
    };

    if (type === 'start') {
      base.title = 'Start';
    } else if (type === 'message') {
      base.title = 'Send message';
      base.bodyText = 'Hello {{customer_name}}, how can we help you today?';
    } else if (type === 'menu') {
      base.title = 'Show options';
      base.bodyText = 'Please choose one of the following options:';
      base.options = [
        { id: 'option_1', label: 'Sales', description: '' },
        { id: 'option_2', label: 'Support', description: '' },
      ];
      base.invalidInputMessage = 'Please choose one of the available options.';
    } else if (type === 'capture_text') {
      base.title = 'Capture text';
      base.bodyText = 'Please write your answer.';
      base.variableName = 'customer_input';
      base.invalidInputMessage = 'Please send a text answer.';
    } else if (type === 'assign_agent') {
      base.title = 'Assign to agent';
      base.assignMode = 'auto';
      base.assignReason = 'FLOW_HANDOFF';
    } else if (type === 'external_link') {
      base.title = 'Send external link';
      base.bodyText = 'You can continue from this link:';
      base.url = 'https://example.com';
    } else if (type === 'end') {
      base.title = 'End flow';
      base.bodyText = 'Thanks. Your request has been captured and our team will follow up with you.';
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

  private sanitizeId(value: string): string {
    return value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '_')
      .replace(/^_+|_+$/g, '') || `option_${Math.random().toString(36).slice(2, 6)}`;
  }
}
