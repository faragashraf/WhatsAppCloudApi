import { Component, inject, OnInit, signal } from '@angular/core';
import { SlicePipe, TitleCasePipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService } from '../../../core/services';
import { Message, PagedResult } from '../../../core/models';

@Component({
  selector: 'app-messages',
  standalone: true,
  imports: [
    FormsModule, SlicePipe, TitleCasePipe, NgClass, ProgressSpinnerModule, InputTextModule, SelectModule, ButtonModule, DialogModule, TranslateModule,
  ],
  template: `
    <div class="space-y-6">
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'messages.title' | translate }}</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'messages.subtitle' | translate }}</p>
      </div>

      <!-- Filters -->
      <div class="flex flex-wrap gap-3 items-end">
        <div class="relative w-64">
          <i class="pi pi-search absolute start-3 top-1/2 -translate-y-1/2 text-slate-400"></i>
          <input pInputText [(ngModel)]="searchQuery" (ngModelChange)="loadMessages()" [placeholder]="'messages.search' | translate"
            class="w-full ps-10 pe-4 py-2.5 rounded-xl text-sm bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
        </div>

        <p-select [(ngModel)]="statusFilter" (ngModelChange)="loadMessages()" [options]="statusOptions" optionLabel="label" optionValue="value" [placeholder]="'messages.status' | translate" styleClass="w-40" />

        <p-select [(ngModel)]="typeFilter" (ngModelChange)="loadMessages()" [options]="typeOptions" optionLabel="label" optionValue="value" [placeholder]="'messages.type' | translate" styleClass="w-40" />
      </div>

      @if (loading()) {
        <div class="flex justify-center py-16"><p-progressSpinner [style]="{'width':'36px','height':'36px'}" strokeWidth="4" /></div>
      } @else if (messages().length === 0) {
        <div class="text-center py-20 bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50">
          <i class="pi pi-envelope !text-[48px] text-slate-300 dark:text-slate-600 mb-3"></i>
          <h3 class="text-lg font-semibold text-slate-700 dark:text-slate-300 mb-1">{{ 'messages.noMessages' | translate }}</h3>
          <p class="text-sm text-slate-500">{{ 'messages.subtitle' | translate }}</p>
        </div>
      } @else {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 overflow-hidden">
          <div class="overflow-x-auto">
            <table class="w-full">
              <thead>
                <tr class="border-b border-slate-200 dark:border-slate-700/50">
                  <th class="text-start text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">{{ 'messages.recipient' | translate }}</th>
                  <th class="text-start text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">{{ 'messages.type' | translate }}</th>
                  <th class="text-start text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">{{ 'messages.status' | translate }}</th>
                  <th class="text-start text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">{{ 'messages.timestamp' | translate }}</th>
                  <th class="text-start text-xs font-semibold text-slate-500 uppercase tracking-wider px-6 py-3">{{ 'messages.error' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (msg of messages(); track msg.messageId) {
                  <tr (click)="openDetail(msg)"
                    class="border-b border-slate-100 dark:border-slate-700/20 hover:bg-slate-50 dark:hover:bg-slate-800/30 transition-colors cursor-pointer">
                    <td class="px-6 py-4">
                      <span class="text-sm font-mono font-medium text-slate-900 dark:text-white dir-ltr">{{ msg.toNumber }}</span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="text-xs px-2.5 py-1 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 font-medium">
                        {{ msg.messageType }}
                      </span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="inline-flex items-center gap-1.5 text-xs font-medium px-2.5 py-1 rounded-full"
                        [class]="msg.status === 'SENT' ? 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400'
                          : msg.status === 'FAILED' ? 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-400'
                          : 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-400'">
                        {{ msg.status }}
                      </span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="text-sm text-slate-600 dark:text-slate-400 dir-ltr">{{ msg.createdAtUtc | slice:0:19 }}</span>
                    </td>
                    <td class="px-6 py-4">
                      <span class="text-sm text-red-500 truncate max-w-[200px] block">{{ msg.failureReason || '—' }}</span>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Pagination -->
          <div class="flex items-center justify-between px-6 py-4 border-t border-slate-200 dark:border-slate-700/50">
            <span class="text-sm text-slate-500">
              {{ (page() - 1) * pageSize + 1 }}–{{ Math.min(page() * pageSize, totalCount()) }}
              / {{ totalCount() }}
            </span>
            <div class="flex gap-2">
              <button pButton [outlined]="true" [disabled]="page() === 1" (click)="goToPage(page() - 1)" class="!rounded-lg">
                <i class="pi pi-chevron-left"></i>
              </button>
              <button pButton [outlined]="true" [disabled]="!hasNext()" (click)="goToPage(page() + 1)" class="!rounded-lg">
                <i class="pi pi-chevron-right"></i>
              </button>
            </div>
          </div>
        </div>
      }
    </div>

    <!-- ━━ Message Detail Dialog ━━━━━━━━━━━━━━━━━━━━━━━━━ -->
    <p-dialog
      [header]="'messages.detail.title' | translate"
      [(visible)]="showDetail"
      [modal]="true"
      [dismissableMask]="true"
      [style]="{width: '560px', maxWidth: '95vw'}"
      [contentStyle]="{'padding': '0'}">

      @if (selectedMessage(); as msg) {
        <div class="divide-y divide-slate-200 dark:divide-slate-700">
          <!-- Status banner -->
          <div class="px-5 py-3 flex items-center gap-3"
            [class]="msg.status === 'SENT' ? 'bg-emerald-50 dark:bg-emerald-950/30'
              : msg.status === 'FAILED' ? 'bg-red-50 dark:bg-red-950/30'
              : 'bg-amber-50 dark:bg-amber-950/30'">
            <i class="pi !text-[20px]"
              [ngClass]="msg.status === 'SENT' ? 'pi-check-circle text-emerald-600' : msg.status === 'FAILED' ? 'pi-times-circle text-red-600' : 'pi-clock text-amber-600'"></i>
            <div>
              <span class="text-sm font-semibold"
                [class]="msg.status === 'SENT' ? 'text-emerald-700 dark:text-emerald-400' : msg.status === 'FAILED' ? 'text-red-700 dark:text-red-400' : 'text-amber-700 dark:text-amber-400'">
                {{ msg.status }}
              </span>
              <span class="text-xs text-slate-500 ms-2 dir-ltr">{{ msg.createdAtUtc | slice:0:19 }}</span>
            </div>
          </div>

          <!-- Details grid -->
          <div class="px-5 py-4 space-y-3">
            <!-- Recipient -->
            <div class="flex items-start gap-3">
              <div class="w-28 shrink-0 text-xs font-semibold text-slate-500 uppercase tracking-wider pt-0.5">{{ 'messages.detail.recipient' | translate }}</div>
              <span class="text-sm font-mono font-medium text-slate-900 dark:text-white dir-ltr">{{ msg.toNumber }}</span>
            </div>
            <!-- Message ID -->
            <div class="flex items-start gap-3">
              <div class="w-28 shrink-0 text-xs font-semibold text-slate-500 uppercase tracking-wider pt-0.5">{{ 'messages.detail.messageId' | translate }}</div>
              <span class="text-sm text-slate-700 dark:text-slate-300 font-mono">#{{ msg.messageId }}</span>
            </div>
            <!-- Type -->
            <div class="flex items-start gap-3">
              <div class="w-28 shrink-0 text-xs font-semibold text-slate-500 uppercase tracking-wider pt-0.5">{{ 'messages.detail.type' | translate }}</div>
              <span class="text-xs px-2.5 py-1 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 font-medium">{{ msg.messageType }}</span>
            </div>
            <!-- External ID -->
            @if (msg.externalMessageId) {
              <div class="flex items-start gap-3">
                <div class="w-28 shrink-0 text-xs font-semibold text-slate-500 uppercase tracking-wider pt-0.5">{{ 'messages.detail.externalId' | translate }}</div>
                <span class="text-xs text-slate-500 font-mono break-all">{{ msg.externalMessageId }}</span>
              </div>
            }
            <!-- Updated -->
            @if (msg.updatedAtUtc) {
              <div class="flex items-start gap-3">
                <div class="w-28 shrink-0 text-xs font-semibold text-slate-500 uppercase tracking-wider pt-0.5">{{ 'messages.detail.updated' | translate }}</div>
                <span class="text-sm text-slate-600 dark:text-slate-400 dir-ltr">{{ msg.updatedAtUtc | slice:0:19 }}</span>
              </div>
            }
          </div>

          <!-- Message Body -->
          <div class="px-5 py-4">
            <div class="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">{{ 'messages.detail.body' | translate }}</div>
            @if (isMediaType(msg.messageType)) {
              <div class="mb-3 p-3 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 flex items-center gap-3">
                <div class="w-10 h-10 rounded-lg bg-emerald-100 dark:bg-emerald-900/40 flex items-center justify-center">
                  <i class="pi !text-[22px] text-emerald-600 dark:text-emerald-400" [ngClass]="getMediaIcon(msg.messageType)"></i>
                </div>
                <div class="flex-1 min-w-0">
                  <span class="text-sm font-medium text-slate-700 dark:text-slate-200 block">{{ msg.messageType | titlecase }} {{ 'messages.detail.attachment' | translate }}</span>
                </div>
              </div>
            }
            <div class="p-3 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
              <p class="text-sm text-slate-800 dark:text-slate-200 whitespace-pre-wrap break-words leading-relaxed">{{ msg.messageBody || '—' }}</p>
            </div>
          </div>

          <!-- Error reason -->
          @if (msg.failureReason) {
            <div class="px-5 py-4">
              <div class="text-xs font-semibold text-red-500 uppercase tracking-wider mb-2">{{ 'messages.detail.failureReason' | translate }}</div>
              <div class="p-3 rounded-xl bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800/40">
                <p class="text-sm text-red-700 dark:text-red-400 whitespace-pre-wrap break-words">{{ msg.failureReason }}</p>
              </div>
            </div>
          }
        </div>
      }
    </p-dialog>
  `,
})
export class MessagesComponent implements OnInit {
  private api = inject(ApiService);
  protected Math = Math;

  messages = signal<Message[]>([]);
  totalCount = signal(0);
  hasNext = signal(false);
  loading = signal(true);
  page = signal(1);
  pageSize = 20;
  searchQuery = '';
  statusFilter = '';
  typeFilter = '';

  // Detail dialog
  showDetail = false;
  selectedMessage = signal<Message | null>(null);

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
    { label: 'Document', value: 'DOCUMENT' },
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
        this.loading.set(false);
      },
    });
  }

  goToPage(p: number): void {
    this.page.set(p);
    this.loadMessages();
  }

  openDetail(msg: Message): void {
    this.selectedMessage.set(msg);
    this.showDetail = true;
  }

  isMediaType(type: string): boolean {
    return ['IMAGE', 'VIDEO', 'AUDIO', 'DOCUMENT', 'image', 'video', 'audio', 'document'].includes(type);
  }

  getMediaIcon(type: string): string {
    switch (type?.toUpperCase()) {
      case 'IMAGE': return 'pi-image';
      case 'VIDEO': return 'pi-video';
      case 'AUDIO': return 'pi-volume-up';
      case 'DOCUMENT': return 'pi-file';
      default: return 'pi-paperclip';
    }
  }
}
