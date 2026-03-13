import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild, ElementRef, AfterViewChecked, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TooltipModule } from 'primeng/tooltip';
import { SelectModule } from 'primeng/select';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, interval, switchMap, takeUntil, catchError, of, filter } from 'rxjs';
import { ApiService, NotificationManagerService, TokenService, PermissionService } from '../../../core/services';
import { Conversation, ConversationMessage, PagedResult, SendMessageRequest } from '../../../core/models';
import { environment } from '../../../../environments/environment';

interface StructuredLocationPreview {
  name: string | null;
  address: string | null;
  latitude: number | null;
  longitude: number | null;
  mapsUrl: string | null;
}

interface StructuredContactPreview {
  name: string;
  phones: string[];
  emails: string[];
}

interface StructuredReactionPreview {
  emoji: string;
  messageId: string | null;
}

interface StructuredOrderPreview {
  title: string;
  itemCount: number;
  catalogId: string | null;
}

interface StructuredSystemPreview {
  body: string;
}

interface StructuredDetailRow {
  label: string;
  value: string;
}

@Component({
  selector: 'app-inbox',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ButtonModule, ProgressSpinnerModule, TooltipModule, FormsModule, TranslateModule, SelectModule],
  template: `
    <div class="wa-shell h-[calc(100dvh-128px)] min-h-[32rem] flex overflow-hidden min-w-0">
      <!-- ━━ Left: Conversation List ━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->
      <div class="wa-sidebar w-full xl:w-[380px] border-e flex flex-col min-w-0"
        [class.max-xl:hidden]="mobileChat() && selectedConversation()">

        <!-- Header -->
        <div class="wa-sidebar-header px-4 pt-4 pb-3">
          <div class="flex items-center justify-between mb-3">
            <h2 class="wa-title text-lg font-semibold">{{ 'inbox.title' | translate }}</h2>
            <span class="wa-count text-xs font-medium">{{ conversations().length }} {{ 'inbox.conversations' | translate }}</span>
          </div>
          <div class="relative">
            <i class="pi pi-search absolute start-3 top-2.5 !text-[16px] wa-search-icon"></i>
            <input [(ngModel)]="searchQuery" (input)="loadConversations()"
              [placeholder]="'inbox.search' | translate"
              class="wa-search-input w-full ps-10 pe-4 py-2.5 rounded-lg text-sm outline-none" />
          </div>
          <!-- Filter chips -->
          <div class="flex gap-2 mt-3">
            <button (click)="filterStatus.set('all')"
              class="wa-filter-pill px-3 py-1.5 rounded-full text-xs font-medium transition-all"
              [class.wa-filter-pill--active]="filterStatus() === 'all'">
              {{ 'inbox.all' | translate }}
            </button>
            <button (click)="filterStatus.set('unread')"
              class="wa-filter-pill px-3 py-1.5 rounded-full text-xs font-medium transition-all"
              [class.wa-filter-pill--active]="filterStatus() === 'unread'">
              {{ 'inbox.unread' | translate }}
            </button>
          </div>
        </div>

        <!-- Conversation list -->
        <div class="wa-sidebar-list flex-1 overflow-y-auto">
          @if (loading()) {
            <div class="flex justify-center py-16"><p-progressSpinner [style]="{'width':'28px','height':'28px'}" strokeWidth="4" /></div>
          } @else if (filteredConversations().length === 0) {
            <div class="text-center py-16 wa-empty-copy">
              <i class="pi pi-comments !text-[48px] mb-2 opacity-40"></i>
              <p class="text-sm">{{ 'inbox.noConversations' | translate }}</p>
            </div>
          } @else {
            @for (group of groupedConversations(); track group.phoneId) {
              <!-- Group header -->
              <div (click)="toggleGroup(group.phoneId)"
                class="wa-group-header flex items-center gap-2 px-4 py-2 cursor-pointer transition-colors select-none">
                <i class="pi pi-phone !text-[13px] wa-group-icon"></i>
                <span class="text-[11px] font-semibold uppercase tracking-[0.08em] flex-1 truncate" [pTooltip]="group.label">{{ group.label }}</span>
                <span class="wa-group-count rounded-full text-[10px] min-w-5 h-5 px-1.5 flex items-center justify-center font-bold">
                  {{ group.conversations.length }}
                </span>
                <i class="pi !text-[12px] wa-group-chevron transition-transform"
                  [ngClass]="isGroupCollapsed(group.phoneId) ? 'pi-chevron-down' : 'pi-chevron-up'"></i>
              </div>
              @if (!isGroupCollapsed(group.phoneId)) {
                @for (conv of group.conversations; track conv.conversationId) {
                  <button (click)="selectConversation(conv)"
                    class="wa-thread w-full flex items-center gap-3 px-4 py-3 text-start group transition-colors"
                    [class.wa-thread--active]="selectedConversation()?.conversationId === conv.conversationId">
                    <!-- Avatar -->
                    <div class="wa-avatar w-12 h-12 rounded-full flex items-center justify-center shrink-0 font-bold text-sm"
                      [class]="getAvatarClasses(conv)">
                      {{ getInitials(conv.contactName || conv.contactNumber) }}
                    </div>
                    <!-- Info -->
                    <div class="flex-1 min-w-0">
                      <div class="flex items-center justify-between gap-2">
                        <span class="wa-conv-name text-[13px] font-semibold truncate"
                          [pTooltip]="conv.contactName || conv.contactNumber">
                          {{ conv.contactName || conv.contactNumber }}
                        </span>
                        <span class="wa-conv-time text-[11px] whitespace-nowrap shrink-0"
                          [class.wa-conv-time--unread]="conv.unreadCount > 0"
                          [class.font-semibold]="conv.unreadCount > 0">
                          {{ formatRelativeTime(conv.lastMessageAtUtc) }}
                        </span>
                      </div>
                      <div class="flex items-center justify-between gap-2 mt-0.5">
                        <p class="wa-conv-preview text-xs truncate"
                          [pTooltip]="getConversationPreviewText(conv)">
                          @if (conv.lastMessageType && normalizeMessageType(conv.lastMessageType) !== 'text') {
                            <i class="pi !text-[13px] !w-3.5 !h-3.5 align-middle me-0.5 opacity-60" [ngClass]="getMediaIcon(conv.lastMessageType)"></i>
                          }
                          {{ getConversationPreviewText(conv) }}
                        </p>
                        @if (conv.unreadCount > 0) {
                          <span class="wa-unread-badge rounded-full text-[10px] min-w-5 h-5 px-1.5 flex items-center justify-center font-bold shrink-0">
                            {{ conv.unreadCount }}
                          </span>
                        }
                      </div>
                      <!-- 24h Window Indicator + Agent -->
                      <div class="flex items-center gap-2 mt-0.5">
                        @if (conv.lastInboundMessageAtUtc) {
                          <span class="wa-meta-chip inline-flex items-center gap-0.5 text-[10px] font-medium px-1.5 py-0.5 rounded-full"
                            [class.wa-meta-chip--expired]="!isWindowOpen(conv.lastInboundMessageAtUtc)">
                            <i class="pi !text-[9px]" [ngClass]="isWindowOpen(conv.lastInboundMessageAtUtc) ? 'pi-clock' : 'pi-exclamation-triangle'"></i>
                            {{ getConvWindowText(conv.lastInboundMessageAtUtc) }}
                          </span>
                        }
                        @if (conv.assignedUserId) {
                          <span class="wa-assignee inline-flex items-center gap-0.5 text-[10px] font-medium">
                            <i class="pi pi-user !text-[10px]"></i>
                            {{ getAssignedUserName(conv) }}
                          </span>
                        }
                        @if (getOwnerName(conv); as ownerName) {
                          <span class="wa-assignee inline-flex items-center gap-0.5 text-[10px] font-medium opacity-80">
                            <i class="pi pi-briefcase !text-[10px]"></i>
                            {{ ownerName }}
                          </span>
                        }
                      </div>
                    </div>
                  </button>
                }
              }
            }
          }
        </div>
      </div>

      <!-- ━━ Right: Chat Area ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->
      <div class="wa-chat-pane flex-1 flex flex-col min-w-0"
        [class.max-xl:hidden]="!selectedConversation()">

        @if (!selectedConversation()) {
          <!-- Empty State -->
          <div class="wa-empty-state flex-1 flex items-center justify-center">
            <div class="text-center max-w-sm px-6">
              <div class="wa-empty-hero w-24 h-24 mx-auto mb-6 rounded-full flex items-center justify-center">
                <i class="pi pi-comments !text-[44px]"></i>
              </div>
              <h3 class="wa-empty-title text-xl font-semibold mb-2">{{ 'inbox.emptyTitle' | translate }}</h3>
              <p class="wa-empty-subtitle text-sm">{{ 'inbox.emptySubtitle' | translate }}</p>
            </div>
          </div>
        } @else {
          <!-- Chat Header -->
          <div class="wa-chat-header h-[60px] px-4 flex items-center gap-3">
            <!-- Back (mobile) -->
            <button (click)="deselectConversation()" class="wa-icon-button xl:hidden w-8 h-8 rounded-full flex items-center justify-center">
              <i class="pi pi-arrow-left !text-[18px]"></i>
            </button>
            <!-- Avatar -->
            <div class="wa-chat-avatar w-10 h-10 rounded-full flex items-center justify-center font-bold text-sm">
              {{ getInitials(selectedConversation()!.contactName || selectedConversation()!.contactNumber) }}
            </div>
            <!-- Info -->
            <div class="flex-1 min-w-0">
              <h3 class="wa-chat-name text-sm font-semibold truncate" [pTooltip]="selectedConversation()!.contactName || selectedConversation()!.contactNumber">{{ selectedConversation()!.contactName || selectedConversation()!.contactNumber }}</h3>
              <p class="wa-chat-subtitle text-[11px] truncate" [pTooltip]="selectedConversation()!.contactNumber">{{ selectedConversation()!.contactNumber }}</p>
              <p class="wa-chat-subtitle text-[11px] truncate">
                Owner: {{ getOwnerName(selectedConversation()!) || 'Unowned' }} | Assignee: {{ getAssignedUserName(selectedConversation()!) }}
              </p>
            </div>
            <!-- 24h Window Countdown -->
            @if (selectedConversation()!.lastInboundMessageAtUtc) {
              <div class="wa-header-badge flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold"
                [class.wa-header-badge--expired]="!windowAvailable()">
                <i class="pi !text-[12px]" [ngClass]="windowAvailable() ? 'pi-clock' : 'pi-exclamation-triangle'"></i>
                <span>{{ windowCountdown() }}</span>
              </div>
            }
            <!-- Actions -->
            <button (click)="markCurrentAsRead()" [pTooltip]="'inbox.markRead' | translate"
              class="wa-icon-button w-8 h-8 rounded-full flex items-center justify-center">
              <i class="pi pi-check !text-[16px]"></i>
            </button>
            <!-- Assign dropdown (admin only) -->
            @if (permService.isAdmin) {
            <div class="relative">
              <p-select
                [options]="agentOptions()"
                [ngModel]="selectedConversation()!.assignedUserId"
                (ngModelChange)="onAssignChange($event)"
                optionLabel="fullName"
                optionValue="companyUserId"
                [placeholder]="'inbox.assignTo' | translate"
                [showClear]="!!selectedConversation()!.assignedUserId"
                styleClass="wa-assign-select w-32 sm:w-40"
              />
            </div>
            }
            <!-- Pick button (non-admin, unassigned conversations) -->
            @if (!permService.isAdmin && !selectedConversation()!.assignedUserId) {
              <button (click)="pickConversation()"
                class="wa-header-pill px-3 py-1.5 rounded-full text-xs font-semibold transition-colors flex items-center gap-1.5"
                [pTooltip]="'inbox.pickTooltip' | translate">
                <i class="pi pi-hand !text-[14px]"></i>
                <span>{{ 'inbox.pick' | translate }}</span>
              </button>
            }
            <!-- Picked/Assigned badge for non-admin -->
            @if (!permService.isAdmin && selectedConversation()!.assignedUserId) {
              <div class="wa-header-pill px-2.5 py-1 rounded-full text-[11px] font-semibold"
                [class.wa-header-pill--owned]="selectedConversation()!.assignedUserId === tokenService.userId()"
                [class.wa-header-pill--other]="selectedConversation()!.assignedUserId !== tokenService.userId()">
                @if (selectedConversation()!.assignedUserId === tokenService.userId()) {
                  <i class="pi pi-check-circle !text-[11px]"></i> {{ 'inbox.pickedByYou' | translate }}
                } @else {
                  <i class="pi pi-user !text-[11px]"></i> {{ getAssignedUserName(selectedConversation()!) }}
                }
              </div>
            }
          </div>

          <!-- Messages -->
          <div #messageContainer
            (scroll)="onChatScroll()"
            class="wa-chat-body flex-1 overflow-y-auto px-4 py-3 space-y-0.5 relative">

            @if (messagesLoading()) {
              <div class="flex justify-center py-16"><p-progressSpinner [style]="{'width':'24px','height':'24px'}" strokeWidth="4" /></div>
            } @else if (messages().length === 0) {
              <div class="flex justify-center py-16">
                <div class="wa-chat-hint rounded-lg px-5 py-3 text-center">
                  <i class="pi pi-sparkles !text-[28px] mb-1"></i>
                  <p class="text-xs">{{ 'inbox.startConversation' | translate }}</p>
                </div>
              </div>
            } @else {
              @for (msg of messages(); track msg.conversationMessageId; let i = $index) {
                <!-- Date separator -->
                @if (isNewDay(i)) {
                  <div class="flex justify-center py-3">
                    <span class="wa-date-chip text-[11px] px-4 py-1.5 rounded-lg font-medium">
                      {{ formatDateLabel(msg.timestampUtc) }}
                    </span>
                  </div>
                }
                <!-- Bubble -->
                <div class="wa-bubble-row flex mb-[2px]"
                  [class.justify-end]="msg.direction === 'outbound'"
                  [class.justify-start]="msg.direction === 'inbound'">
                  <div class="wa-bubble relative"
                    [class.wa-bubble--out]="msg.direction === 'outbound'"
                    [class.wa-bubble--in]="msg.direction === 'inbound'">
                    <!-- Tail -->
                    @if (isFirstInGroup(i)) {
                      <div class="absolute top-0 w-3 h-3"
                        [class]="msg.direction === 'outbound' ? '-end-1.5' : '-start-1.5'">
                        <svg viewBox="0 0 12 12" class="w-3 h-3">
                          @if (msg.direction === 'outbound') {
                            <path d="M0,0 L12,0 C6,4 3,8 0,12 Z"
                              [attr.fill]="getBubbleTailFill(msg.direction)" />
                          } @else {
                            <path d="M12,0 L0,0 C6,4 9,8 12,12 Z"
                              [attr.fill]="getBubbleTailFill(msg.direction)" />
                          }
                        </svg>
                      </div>
                    }
                    <!-- Media preview -->
                    @if (isMediaMessage(msg)) {
                      <div class="mb-1.5 rounded-md overflow-hidden">
                        @if (resolveMediaUrl(msg); as mediaHref) {
                          @switch (normalizeMessageType(msg.messageType)) {
                            @case ('image') {
                              <img [src]="mediaHref" alt="Image" class="max-w-full rounded-md cursor-pointer hover:opacity-90 transition-opacity" loading="lazy"
                                (click)="openMediaUrl(mediaHref)" />
                            }
                            @case ('video') {
                              <video [src]="mediaHref" controls class="max-w-full rounded-md" preload="metadata"></video>
                            }
                            @case ('audio') {
                              <audio [src]="mediaHref" controls class="w-full min-w-[200px]" preload="metadata"></audio>
                            }
                            @default {
                              <a [href]="mediaHref" [attr.download]="getDownloadFileName(msg)"
                                class="wa-attachment-card flex items-center gap-3 p-3 rounded-md cursor-pointer hover:opacity-80 transition-opacity no-underline"
                                [class]="msg.direction === 'outbound'
                                  ? 'bg-emerald-500/10 dark:bg-emerald-800/30'
                                  : 'bg-slate-100 dark:bg-slate-600/40'">
                                <div class="w-10 h-10 rounded-lg bg-emerald-100 dark:bg-emerald-900/40 flex items-center justify-center">
                                  <i class="pi pi-file !text-[22px] text-emerald-600 dark:text-emerald-400"></i>
                                </div>
                                <div class="flex-1 min-w-0">
                                  <span class="text-xs font-medium text-slate-700 dark:text-slate-200 block truncate">{{ getAttachmentDisplayName(msg) }}</span>
                                  <span class="text-[10px] text-slate-400">{{ 'inbox.tapToDownload' | translate }}</span>
                                </div>
                                <i class="pi pi-download !text-[16px] text-slate-400"></i>
                              </a>
                            }
                          }
                        } @else {
                          <!-- No media URL yet - show type indicator -->
                          <div class="flex items-center gap-2 p-2.5 rounded-md"
                            [class]="msg.direction === 'outbound'
                              ? 'bg-emerald-500/10 dark:bg-emerald-800/30'
                              : 'bg-slate-100 dark:bg-slate-600/40'">
                            <i class="pi !text-[22px] text-emerald-600 dark:text-emerald-400" [ngClass]="getMediaIcon(msg.messageType)"></i>
                            <span class="text-xs font-medium text-slate-600 dark:text-slate-300">{{ getMediaLabel(msg.messageType) }}</span>
                          </div>
                        }
                      </div>
                    }
                    @if (shouldShowTypeBadge(msg)) {
                      <div class="mb-2 inline-flex items-center gap-1.5 rounded-md px-2.5 py-1 text-[11px] font-medium"
                        [class]="msg.direction === 'outbound'
                          ? 'bg-emerald-500/10 text-emerald-800 dark:bg-emerald-800/30 dark:text-emerald-200'
                          : 'bg-slate-100 text-slate-600 dark:bg-slate-600/40 dark:text-slate-200'">
                        <i class="pi !text-[12px]" [ngClass]="getMediaIcon(msg.messageType)"></i>
                        <span>{{ getMediaLabel(msg.messageType) }}</span>
                      </div>
                    }

                    @if (getLocationPreview(msg); as location) {
                      <a [href]="location.mapsUrl || null" [attr.target]="location.mapsUrl ? '_blank' : null" rel="noopener noreferrer"
                        class="mb-2 block rounded-md border p-2.5 no-underline"
                        [class]="msg.direction === 'outbound'
                          ? 'border-emerald-300/30 bg-emerald-500/5'
                          : 'border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-600/30'">
                        <div class="flex items-start gap-2">
                          <i class="pi pi-map-marker mt-0.5 !text-[14px] text-emerald-600 dark:text-emerald-300"></i>
                          <div class="min-w-0 flex-1">
                            <p class="text-xs font-semibold truncate">{{ location.name || getMediaLabel('location') }}</p>
                            @if (location.address) {
                              <p class="text-[11px] mt-0.5 opacity-85 truncate">{{ location.address }}</p>
                            }
                            @if (location.latitude !== null && location.longitude !== null) {
                              <p class="text-[10px] mt-0.5 opacity-75 dir-ltr">{{ location.latitude }}, {{ location.longitude }}</p>
                            }
                          </div>
                        </div>
                      </a>
                    }

                    @if (getReactionPreview(msg); as reaction) {
                      <div class="mb-2 rounded-md border p-2.5"
                        [class]="msg.direction === 'outbound'
                          ? 'border-emerald-300/30 bg-emerald-500/5'
                          : 'border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-600/30'">
                        <div class="flex items-center gap-2">
                          <span class="text-lg leading-none">{{ reaction.emoji }}</span>
                          <div class="min-w-0 flex-1">
                            <p class="text-xs font-semibold">{{ getMediaLabel('reaction') }}</p>
                            @if (reaction.messageId) {
                              <p class="text-[10px] opacity-75 truncate">{{ reaction.messageId }}</p>
                            }
                          </div>
                        </div>
                      </div>
                    }

                    @if (getContactsPreview(msg); as contacts) {
                      @if (contacts.length > 0) {
                        <div class="mb-2 rounded-md border p-2.5 space-y-2"
                          [class]="msg.direction === 'outbound'
                            ? 'border-emerald-300/30 bg-emerald-500/5'
                            : 'border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-600/30'">
                          @for (contact of contacts; track $index) {
                            <div class="rounded-md bg-white/60 dark:bg-slate-700/30 px-2 py-1.5">
                              <p class="text-xs font-semibold truncate">{{ contact.name }}</p>
                              @if (contact.phones.length > 0) {
                                <p class="text-[10px] opacity-80 truncate dir-ltr">{{ contact.phones.join(' | ') }}</p>
                              }
                              @if (contact.emails.length > 0) {
                                <p class="text-[10px] opacity-80 truncate dir-ltr">{{ contact.emails.join(' | ') }}</p>
                              }
                            </div>
                          }
                        </div>
                      }
                    }

                    @if (getOrderPreview(msg); as order) {
                      <div class="mb-2 rounded-md border p-2.5"
                        [class]="msg.direction === 'outbound'
                          ? 'border-emerald-300/30 bg-emerald-500/5'
                          : 'border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-600/30'">
                        <div class="flex items-center justify-between gap-2">
                          <p class="text-xs font-semibold truncate">{{ order.title }}</p>
                          <span class="text-[10px] opacity-80">{{ order.itemCount }}</span>
                        </div>
                        @if (order.catalogId) {
                          <p class="text-[10px] mt-0.5 opacity-75 truncate">{{ order.catalogId }}</p>
                        }
                      </div>
                    }

                    @if (getSystemPreview(msg); as system) {
                      <div class="mb-2 rounded-md border p-2.5"
                        [class]="msg.direction === 'outbound'
                          ? 'border-emerald-300/30 bg-emerald-500/5'
                          : 'border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-600/30'">
                        <p class="text-xs font-semibold">{{ getMediaLabel('system') }}</p>
                        <p class="text-[11px] mt-0.5 break-words">{{ system.body }}</p>
                      </div>
                    }

                    @if (getStructuredDetailRows(msg); as detailRows) {
                      @if (detailRows.length > 0) {
                        <div class="mb-2 rounded-md border p-2.5 space-y-1.5"
                          [class]="msg.direction === 'outbound'
                            ? 'border-emerald-300/30 bg-emerald-500/5'
                            : 'border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-600/30'">
                          @for (row of detailRows; track row.label) {
                            <div class="flex items-start gap-2 text-[11px]">
                              <span class="font-semibold opacity-80 shrink-0">{{ row.label }}:</span>
                              <span class="opacity-90 break-all">{{ row.value }}</span>
                            </div>
                          }
                        </div>
                      }
                    }

                    @if (getDisplayContent(msg); as displayContent) {
                      <p class="wa-message-text whitespace-pre-wrap break-words" [dir]="detectDir(displayContent)" [innerHTML]="renderContentWithLinks(displayContent)"></p>
                      @for (url of extractUrls(displayContent); track url) {
                        <a [href]="url" target="_blank" rel="noopener noreferrer"
                          class="wa-url-card mt-1.5 block rounded-md overflow-hidden no-underline transition-opacity hover:opacity-80"
                          [class]="msg.direction === 'outbound'
                            ? 'border-emerald-300/30 bg-emerald-500/5'
                            : 'border-slate-200 dark:border-slate-600 bg-slate-50 dark:bg-slate-600/30'">
                          <div class="px-3 py-2">
                            <div class="flex items-center gap-1.5">
                              <i class="pi pi-external-link !text-[11px] text-blue-500"></i>
                              <span class="text-[11px] font-medium text-blue-600 dark:text-blue-400 truncate">{{ extractDomain(url) }}</span>
                            </div>
                            <p class="text-[10px] text-slate-400 truncate mt-0.5">{{ url }}</p>
                          </div>
                        </a>
                      }
                    }
                    <!-- Time + Status -->
                    <div class="wa-message-meta flex items-center justify-end gap-1 -mb-0.5 mt-0.5 select-none">
                      <span class="wa-message-time text-[10.5px] leading-none">
                        {{ formatTime(msg.timestampUtc) }}
                      </span>
                      @if (msg.direction === 'outbound') {
                        @switch (normalizeMessageStatus(msg.status)) {
                          @case ('sending') {
                            <i class="pi pi-clock !text-[14px] !w-3.5 !h-3.5 text-slate-400"></i>
                          }
                          @case ('sent') {
                            <svg viewBox="0 0 20 14" class="wa-status-icon wa-status-icon--single wa-status-icon--sent" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                              <path d="M5.25 7.5 8.6 10.85 15.6 3.85" />
                            </svg>
                          }
                          @case ('delivered') {
                            <svg viewBox="0 0 20 14" class="wa-status-icon wa-status-icon--delivered" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                              <path d="M1.9 7.5 5.25 10.85 12.25 3.85" />
                              <path d="M6.25 7.5 9.6 10.85 16.6 3.85" />
                            </svg>
                          }
                          @case ('read') {
                            <svg viewBox="0 0 20 14" class="wa-status-icon wa-status-icon--read" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                              <path d="M1.9 7.5 5.25 10.85 12.25 3.85" />
                              <path d="M6.25 7.5 9.6 10.85 16.6 3.85" />
                            </svg>
                          }
                          @case ('failed') {
                            <i class="pi pi-times-circle !text-[14px] !w-3.5 !h-3.5 text-red-500" [pTooltip]="msg.failureReason || ''"></i>
                          }
                          @default {
                            <svg viewBox="0 0 20 14" class="wa-status-icon wa-status-icon--single wa-status-icon--sent" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                              <path d="M5.25 7.5 8.6 10.85 15.6 3.85" />
                            </svg>
                          }
                        }
                      }
                    </div>
                  </div>
                </div>
              }
            }
          </div>

          <!-- Scroll-to-bottom FAB -->
          @if (showScrollDown()) {
            <div class="absolute bottom-[80px] end-6 z-10">
              <button (click)="scrollToBottom(true)"
                class="wa-scroll-button w-10 h-10 rounded-full flex items-center justify-center transition-colors">
                <i class="pi pi-chevron-down !text-[18px]"></i>
                @if (newMessageCount() > 0) {
                  <span class="wa-unread-badge absolute -top-1.5 -end-1.5 rounded-full text-[9px] min-w-4 h-4 px-1 flex items-center justify-center font-bold">
                    {{ newMessageCount() }}
                  </span>
                }
              </button>
            </div>
          }

          <!-- Window expired banner -->
          @if (selectedConversation()!.lastInboundMessageAtUtc && !windowAvailable()) {
            <div class="wa-system-banner wa-system-banner--expired px-3 py-2 flex items-center gap-2">
              <i class="pi pi-exclamation-triangle !text-[16px] text-red-500"></i>
              <span class="text-xs font-medium">{{ 'inbox.windowExpired' | translate }}</span>
            </div>
          }

          <!-- ━━ Input Area ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->
          @if (!canInteract()) {
            <!-- Locked: user must pick or be assigned -->
            <div class="wa-compose-lock px-4 py-4 text-center">
              <div class="wa-lock-copy flex items-center justify-center gap-2">
                <i class="pi pi-lock !text-[20px]"></i>
                <span class="text-sm font-medium">{{ 'inbox.pickFirst' | translate }}</span>
              </div>
              @if (!selectedConversation()!.assignedUserId) {
                <button (click)="pickConversation()"
                  class="wa-primary-action mt-2 px-4 py-2 rounded-xl text-sm font-semibold transition-colors">
                  <i class="pi pi-hand me-1 !text-[14px]"></i> {{ 'inbox.pick' | translate }}
                </button>
              }
            </div>
          } @else if (selectedConversation()!.lastInboundMessageAtUtc && !windowAvailable()) {
            <!-- Locked: 24h window expired -->
            <div class="wa-compose-lock px-4 py-4 text-center">
              <div class="flex items-center justify-center gap-2 text-red-500">
                <i class="pi pi-lock !text-[20px]"></i>
                <span class="text-sm font-medium">{{ 'inbox.windowExpired' | translate }}</span>
              </div>
              <p class="wa-lock-copy text-xs mt-1">{{ 'inbox.windowClosed' | translate }}</p>
            </div>
          } @else {
          <div class="wa-compose px-3 py-2.5">
            <!-- File preview -->
            @if (selectedFile()) {
              <div class="wa-file-preview mb-2 p-2 rounded-lg flex items-center gap-2 animate-slide-up">
                <div class="wa-file-preview-icon w-9 h-9 rounded flex items-center justify-center">
                  <i class="pi !text-[18px] text-emerald-600" [ngClass]="getMediaIcon(getFileType(selectedFile()!))"></i>
                </div>
                <div class="flex-1 min-w-0">
                  <p class="wa-file-name text-xs truncate font-medium" [pTooltip]="selectedFile()!.name">{{ selectedFile()!.name }}</p>
                  <p class="wa-file-copy text-[10px]">{{ formatFileSize(selectedFile()!.size) }}</p>
                  <p class="wa-file-copy mt-1 text-[10px]">{{ getMediaLabel(getFileType(selectedFile()!)) }}</p>
                </div>
                <button (click)="clearFile()" class="wa-icon-button w-6 h-6 rounded-full flex items-center justify-center transition-colors">
                  <i class="pi pi-times !text-[16px]"></i>
                </button>
              </div>
            }
            @if (attachmentError()) {
              <div class="wa-system-banner wa-system-banner--expired mb-2 px-3 py-2 rounded-lg text-[11px]">
                {{ attachmentError() }}
              </div>
            }
            <div class="flex items-end gap-2">
              <!-- Attach (hidden if attachment permission is disabled) -->
              @if (permService.has('conversationsAttach')) {
              <button (click)="fileInput.click()"
                class="wa-compose-action w-10 h-10 rounded-full flex items-center justify-center transition-colors shrink-0"
                                [pTooltip]="'inbox.attach' | translate">
                <i class="pi pi-paperclip !text-[22px] rotate-45"></i>
              </button>
              <input #fileInput type="file" class="hidden" accept="image/*,video/*,audio/*,.pdf,.doc,.docx,.xls,.xlsx,.txt,.csv,.zip" (change)="onFileSelected($event)" />
              }
              <!-- Text -->
              <div class="wa-compose-box flex-1 rounded-2xl px-4 py-2 transition-shadow">
                <textarea #messageInput
                  [(ngModel)]="newMessage"
                  (keydown)="onKeyDown($event)"
                  (input)="onComposerInput($event)"
                  [dir]="inputDir()"
                  [placeholder]="'inbox.typeMessage' | translate"
                  rows="1"
                  class="w-full resize-none bg-transparent border-none outline-none text-sm leading-5"
                  style="max-height: 120px; overflow-y: auto; min-height: 20px;"></textarea>
              </div>
              <!-- Send -->
              <button (click)="sendMessage()" [disabled]="!canSend()"
                class="wa-send-button flex items-center justify-center shrink-0 transition-all duration-200"
                [class.wa-send-button--active]="canSend()"
                [class.wa-send-button--inactive]="!canSend()">
                @if (sending()) {
                  <p-progressSpinner [style]="{'width':'18px','height':'18px'}" strokeWidth="4" />
                } @else {
                  <i class="pi pi-send !text-[20px]"></i>
                }
              </button>
            </div>
          </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      position: relative;
      --wa-bg: #efeae2;
      --wa-panel: #ffffff;
      --wa-panel-muted: #f0f2f5;
      --wa-panel-hover: #f5f6f6;
      --wa-divider: #d1d7db;
      --wa-text: #111b21;
      --wa-text-soft: #667781;
      --wa-text-muted: #8696a0;
      --wa-accent: #00a884;
      --wa-accent-strong: #008069;
      --wa-accent-soft: #e7fce3;
      --wa-bubble-out: #d9fdd3;
      --wa-bubble-in: #ffffff;
      --wa-chip: #e9edef;
      --wa-read: #53bdeb;
    }

    :host-context(.dark) {
      --wa-bg: #0b141a;
      --wa-panel: #111b21;
      --wa-panel-muted: #202c33;
      --wa-panel-hover: #182229;
      --wa-divider: #2a3942;
      --wa-text: #e9edef;
      --wa-text-soft: #8696a0;
      --wa-text-muted: #6b7c85;
      --wa-accent: #00a884;
      --wa-accent-strong: #00a884;
      --wa-accent-soft: rgba(0, 168, 132, 0.14);
      --wa-bubble-out: #005c4b;
      --wa-bubble-in: #202c33;
      --wa-chip: #182229;
      --wa-read: #53bdeb;
    }

    @keyframes slide-up {
      from { opacity: 0; transform: translateY(8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .animate-slide-up { animation: slide-up 0.2s ease-out; }

    .wa-shell {
      background: var(--wa-panel);
      border: 1px solid var(--wa-divider);
      border-radius: 18px;
      box-shadow: 0 16px 42px -26px rgba(17, 27, 33, 0.45);
    }

    .wa-sidebar,
    .wa-chat-pane {
      background: var(--wa-panel);
    }

    .wa-sidebar {
      border-color: var(--wa-divider);
    }

    .wa-sidebar-header,
    .wa-chat-header,
    .wa-compose,
    .wa-compose-lock {
      background: var(--wa-panel-muted);
      border-color: var(--wa-divider);
    }

    .wa-sidebar-header,
    .wa-chat-header,
    .wa-compose,
    .wa-compose-lock,
    .wa-system-banner {
      border-bottom-color: var(--wa-divider);
      border-top-color: var(--wa-divider);
    }

    .wa-title,
    .wa-chat-name,
    .wa-empty-title,
    .wa-file-name,
    .wa-conv-name {
      color: var(--wa-text);
    }

    .wa-count,
    .wa-conv-preview,
    .wa-assignee,
    .wa-chat-subtitle,
    .wa-empty-subtitle,
    .wa-empty-copy,
    .wa-file-copy,
    .wa-lock-copy {
      color: var(--wa-text-soft);
    }

    .wa-search-icon,
    .wa-group-icon,
    .wa-group-chevron,
    .wa-scroll-button,
    .wa-compose-action,
    .wa-chat-hint,
    .wa-empty-hero {
      color: var(--wa-text-soft);
    }

    .wa-search-input {
      background: var(--wa-panel);
      border: 1px solid transparent;
      color: var(--wa-text);
      transition: border-color 0.2s ease, box-shadow 0.2s ease;
    }

    .wa-search-input::placeholder {
      color: var(--wa-text-muted);
    }

    .wa-search-input:focus {
      border-color: rgba(0, 168, 132, 0.24);
      box-shadow: 0 0 0 2px rgba(0, 168, 132, 0.12);
    }

    .wa-filter-pill {
      background: transparent;
      border: 1px solid var(--wa-divider);
      color: var(--wa-text-soft);
    }

    .wa-filter-pill--active {
      background: var(--wa-accent-soft);
      border-color: rgba(0, 168, 132, 0.18);
      color: var(--wa-accent-strong);
    }

    .wa-group-header {
      background: var(--wa-panel);
      border-bottom: 1px solid var(--wa-divider);
      color: var(--wa-text-soft);
    }

    .wa-group-header:hover {
      background: var(--wa-panel-hover);
    }

    .wa-group-count,
    .wa-unread-badge {
      background: var(--wa-accent);
      color: #fff;
    }

    .wa-thread {
      background: var(--wa-panel);
      border-bottom: 1px solid color-mix(in srgb, var(--wa-divider) 58%, transparent);
    }

    .wa-thread:hover,
    .wa-thread--active {
      background: var(--wa-panel-muted);
    }

    .wa-avatar,
    .wa-chat-avatar {
      box-shadow: inset 0 0 0 1px rgba(17, 27, 33, 0.04);
    }

    .wa-conv-time {
      color: var(--wa-text-muted);
    }

    .wa-conv-time--unread {
      color: var(--wa-accent-strong);
    }

    .wa-meta-chip {
      background: color-mix(in srgb, var(--wa-accent) 10%, transparent);
      color: var(--wa-accent-strong);
    }

    .wa-meta-chip--expired {
      background: rgba(239, 71, 58, 0.1);
      color: #d64545;
    }

    .wa-empty-state {
      background:
        linear-gradient(180deg, color-mix(in srgb, var(--wa-panel-muted) 85%, transparent), transparent 40%),
        var(--wa-bg);
    }

    .wa-empty-hero {
      background: color-mix(in srgb, var(--wa-panel-muted) 85%, transparent);
    }

    .wa-chat-header {
      border-bottom: 1px solid var(--wa-divider);
    }

    .wa-header-badge,
    .wa-header-pill {
      background: color-mix(in srgb, var(--wa-panel) 88%, transparent);
      color: var(--wa-text-soft);
      border: 1px solid color-mix(in srgb, var(--wa-divider) 75%, transparent);
    }

    .wa-header-badge--expired,
    .wa-header-pill--other {
      color: #d64545;
      border-color: rgba(239, 71, 58, 0.18);
      background: rgba(239, 71, 58, 0.08);
    }

    .wa-header-pill--owned {
      color: var(--wa-accent-strong);
      border-color: rgba(0, 168, 132, 0.18);
      background: color-mix(in srgb, var(--wa-accent) 10%, transparent);
    }

    .wa-icon-button {
      color: var(--wa-text-soft);
    }

    .wa-icon-button:hover,
    .wa-compose-action:hover,
    .wa-scroll-button:hover {
      background: color-mix(in srgb, var(--wa-panel) 74%, var(--wa-panel-muted));
    }

    .wa-chat-body {
      background-color: var(--wa-bg);
      background-image:
        radial-gradient(circle at 24px 24px, rgba(17, 27, 33, 0.02) 1.6px, transparent 1.7px),
        radial-gradient(circle at 60px 54px, rgba(0, 168, 132, 0.04) 1.2px, transparent 1.3px),
        linear-gradient(135deg, rgba(255, 255, 255, 0.08) 25%, transparent 25%),
        linear-gradient(225deg, rgba(255, 255, 255, 0.05) 25%, transparent 25%);
      background-size: 82px 82px, 92px 92px, 34px 34px, 34px 34px;
      background-position: 0 0, 16px 18px, 0 0, 17px 17px;
    }

    .wa-chat-hint,
    .wa-date-chip {
      background: rgba(255, 255, 255, 0.9);
      color: var(--wa-text-soft);
      box-shadow: 0 1px 0.5px rgba(17, 27, 33, 0.13);
    }

    :host-context(.dark) .wa-chat-hint,
    :host-context(.dark) .wa-date-chip {
      background: rgba(32, 44, 51, 0.9);
    }

    .wa-bubble {
      max-width: min(72%, 720px);
      padding: 6px 8px 4px 9px;
      border-radius: 7.5px;
      box-shadow: 0 1px 0.5px rgba(17, 27, 33, 0.13);
      color: var(--wa-text);
    }

    .wa-bubble--out {
      background: var(--wa-bubble-out);
    }

    .wa-bubble--in {
      background: var(--wa-bubble-in);
    }

    .wa-bubble p {
      font-size: 14.2px;
      line-height: 19px;
    }

    .wa-attachment-card,
    .wa-url-card {
      border: 1px solid color-mix(in srgb, var(--wa-divider) 70%, transparent);
    }

    .wa-message-meta {
      gap: 2px;
    }

    .wa-message-time {
      color: var(--wa-text-muted);
    }

    .wa-status-icon {
      width: 18px;
      height: 14px;
      display: block;
      overflow: visible;
    }

    .wa-status-icon--single {
      width: 16px;
    }

    .wa-status-icon--sent,
    .wa-status-icon--delivered {
      color: var(--wa-text-muted);
    }

    .wa-status-icon--read {
      color: var(--wa-read);
    }

    .wa-scroll-button {
      background: var(--wa-panel);
      border: 1px solid color-mix(in srgb, var(--wa-divider) 80%, transparent);
      box-shadow: 0 8px 20px -16px rgba(17, 27, 33, 0.6);
    }

    .wa-system-banner {
      border-top: 1px solid transparent;
      color: var(--wa-text-soft);
    }

    .wa-system-banner--expired {
      background: rgba(239, 71, 58, 0.08);
      border-color: rgba(239, 71, 58, 0.12);
      color: #d64545;
    }

    .wa-compose,
    .wa-compose-lock {
      border-top: 1px solid var(--wa-divider);
    }

    .wa-file-preview {
      background: var(--wa-panel);
      border: 1px solid var(--wa-divider);
      box-shadow: 0 1px 0.5px rgba(17, 27, 33, 0.08);
    }

    .wa-file-preview-icon {
      background: color-mix(in srgb, var(--wa-accent) 10%, transparent);
    }

    .wa-compose-box {
      background: var(--wa-panel);
      border: 1px solid transparent;
    }

    .wa-compose-box:focus-within {
      border-color: rgba(0, 168, 132, 0.24);
      box-shadow: 0 0 0 2px rgba(0, 168, 132, 0.1);
    }

    .wa-compose-box textarea {
      color: var(--wa-text);
    }

    .wa-compose-box textarea::placeholder {
      color: var(--wa-text-muted);
    }

    .wa-send-button {
      width: 40px;
      height: 40px;
      border-radius: 999px;
    }

    .wa-send-button--active {
      background: var(--wa-accent-strong);
      color: #fff;
    }

    .wa-primary-action {
      background: var(--wa-accent-strong);
      color: #fff;
    }

    .wa-send-button--inactive {
      background: color-mix(in srgb, var(--wa-divider) 90%, transparent);
      color: var(--wa-text-muted);
    }

    :host ::ng-deep .wa-assign-select {
      background: var(--wa-panel);
      border: 1px solid var(--wa-divider);
      border-radius: 999px;
      min-height: 34px;
    }

    :host ::ng-deep .wa-assign-select .p-select-label,
    :host ::ng-deep .wa-assign-select .p-select-trigger-icon {
      color: var(--wa-text-soft);
      font-size: 12px;
    }

    :host ::ng-deep .wa-assign-select .p-select-dropdown {
      width: 2rem;
    }

    @media (max-width: 1279px) {
      .wa-shell {
        border-radius: 14px;
      }

      .wa-bubble {
        max-width: min(82%, 620px);
      }
    }

    @media (max-width: 1023px) {
      .wa-shell {
        min-height: 30rem;
        border-radius: 12px;
      }

      .wa-chat-header {
        padding-inline: 0.75rem;
      }

      .wa-chat-body {
        padding-inline: 0.75rem;
      }

      .wa-compose,
      .wa-compose-lock {
        padding-inline: 0.65rem;
      }

      .wa-header-badge {
        display: none;
      }

      .wa-chat-subtitle:last-of-type {
        display: none;
      }

      .wa-bubble {
        max-width: min(88%, 560px);
      }
    }

    @media (max-width: 767px) {
      .wa-shell {
        min-height: 26rem;
        border-radius: 0.95rem;
      }

      .wa-sidebar-header {
        padding-inline: 0.75rem;
      }

      .wa-thread {
        padding-inline: 0.75rem;
      }

      .wa-bubble {
        max-width: 94%;
      }

      .wa-send-button {
        width: 38px;
        height: 38px;
      }
    }

    ::-webkit-scrollbar { width: 5px; }
    ::-webkit-scrollbar-track { background: transparent; }
    ::-webkit-scrollbar-thumb { background: color-mix(in srgb, var(--wa-text-muted) 42%, transparent); border-radius: 4px; }
    ::-webkit-scrollbar-thumb:hover { background: color-mix(in srgb, var(--wa-text-soft) 52%, transparent); }
  `],
})
export class InboxComponent implements OnInit, OnDestroy, AfterViewChecked {
  private static readonly TYPING_INDICATOR_THROTTLE_MS = 18_000;
  private static readonly MEDIA_MESSAGE_TYPES = new Set<string>(['image', 'video', 'audio', 'document', 'sticker']);

  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly notifService = inject(NotificationManagerService);
  protected readonly tokenService = inject(TokenService);
  readonly permService = inject(PermissionService);
  private readonly translate = inject(TranslateService);
  private readonly destroy$ = new Subject<void>();

  @ViewChild('messageContainer') messageContainer?: ElementRef<HTMLDivElement>;
  @ViewChild('messageInput') messageInput?: ElementRef<HTMLTextAreaElement>;

  // ─── State ───
  conversations = signal<Conversation[]>([]);
  selectedConversation = signal<Conversation | null>(null);
  messages = signal<ConversationMessage[]>([]);
  loading = signal(true);
  messagesLoading = signal(false);
  sending = signal(false);
  filterStatus = signal<'all' | 'unread'>('all');
  selectedFile = signal<File | null>(null);
  attachmentError = signal<string | null>(null);
  showScrollDown = signal(false);
  newMessageCount = signal(0);
  mobileChat = signal(false);
  collapsedGroups = signal<Set<number | null>>(new Set());
  mediaUrls = signal<Record<number, string>>({});
  private windowTimerInterval: any = null;
  windowCountdownText = signal('');
  private readonly pendingMediaResolves = new Set<number>();
  private readonly blobObjectUrls = new Map<number, string>();
  private readonly structuredContentCache = new Map<number, { raw: string; parsed: unknown | null }>();

  // Agent assignment
  agentOptions = signal<{ companyUserId: number; fullName: string; email: string; role: string }[]>([]);

  searchQuery = '';
  newMessage = '';
  private shouldScroll = false;
  private lastPollTimestamp: string | null = null;
  private isUserNearBottom = true;
  private typingConversationId: number | null = null;
  private lastTypingIndicatorAt = 0;
  private typingIndicatorInFlight = false;

  // ─── URL pattern ───
  private readonly urlRegex = /https?:\/\/[^\s<>"{}|\\^\[\]`]+/gi;

  // ─── Computed ───
  filteredConversations = computed(() => {
    const convs = this.conversations();
    return this.filterStatus() === 'unread' ? convs.filter(c => c.unreadCount > 0) : convs;
  });

  groupedConversations = computed(() => {
    const convs = this.filteredConversations();
    const map = new Map<number | null, { label: string; conversations: Conversation[] }>();

    for (const c of convs) {
      const phoneId = c.whatsAppPhoneNumberId ?? null;
      if (!map.has(phoneId)) {
        const label = c.whatsAppPhoneNumber?.verifiedName || c.whatsAppPhoneNumber?.displayPhoneNumber || 'Unknown';
        map.set(phoneId, { label, conversations: [] });
      }
      map.get(phoneId)!.conversations.push(c);
    }

    return Array.from(map.entries()).map(([phoneId, data]) => ({
      phoneId,
      label: data.label,
      conversations: data.conversations,
      count: data.conversations.length,
      unreadCount: data.conversations.reduce((sum, c) => sum + c.unreadCount, 0),
    }));
  });

  canSend(): boolean {
    const conv = this.selectedConversation();
    if (!conv) return false;
    if (!this.canInteract()) return false;
    if (conv.lastInboundMessageAtUtc && !this.windowAvailable()) return false;
    return (this.newMessage.trim().length > 0 || this.selectedFile() !== null) && !this.sending();
  }

  /** Whether the current user can interact with (send messages to) the selected conversation.
   *  Admin can always interact. Non-admin must be the assigned user. */
  canInteract = computed(() => {
    const conv = this.selectedConversation();
    if (!conv) return false;
    if (this.permService.isAdmin) return true;
    return conv.assignedUserId === this.tokenService.userId();
  });

  isDark = computed(() => document.documentElement.classList.contains('dark'));

  windowAvailable = computed(() => {
    this.windowCountdownText(); // force reactivity
    const conv = this.selectedConversation();
    if (!conv?.lastInboundMessageAtUtc) return true;
    const lastInbound = new Date(conv.lastInboundMessageAtUtc).getTime();
    return (Date.now() - lastInbound) < 24 * 60 * 60 * 1000;
  });

  windowCountdown = computed(() => {
    this.windowCountdownText(); // force reactivity
    const conv = this.selectedConversation();
    if (!conv?.lastInboundMessageAtUtc) return '';
    const lastInbound = new Date(conv.lastInboundMessageAtUtc).getTime();
    const expiresAt = lastInbound + 24 * 60 * 60 * 1000;
    const remaining = expiresAt - Date.now();
    if (remaining <= 0) return this.translate.instant('inbox.windowClosed');
    const h = Math.floor(remaining / 3600000);
    const m = Math.floor((remaining % 3600000) / 60000);
    return `${h}h ${m}m`;
  });

  isGroupCollapsed(phoneId: number | null): boolean {
    return this.collapsedGroups().has(phoneId);
  }

  toggleGroup(phoneId: number | null): void {
    this.collapsedGroups.update(set => {
      const next = new Set(set);
      if (next.has(phoneId)) next.delete(phoneId);
      else next.add(phoneId);
      return next;
    });
  }

  // ─── Lifecycle ───
  ngOnInit(): void {
    this.loadConversations();
    this.loadAgents();
    this.notifService.requestPermission();
    this.startConversationPolling();
    // Update 24h countdown every 30 seconds
    this.windowTimerInterval = setInterval(() => {
      this.windowCountdownText.set(Date.now().toString());
    }, 30000);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    if (this.windowTimerInterval) clearInterval(this.windowTimerInterval);
    for (const url of this.blobObjectUrls.values()) {
      URL.revokeObjectURL(url);
    }
    this.blobObjectUrls.clear();
  }

  ngAfterViewChecked(): void {
    if (this.shouldScroll) {
      this.shouldScroll = false;
      this.scrollToBottom(false);
    }
  }

  // ─── Conversations ───
  loadConversations(): void {
    const params: Record<string, string> = { pageSize: '100' };
    if (this.searchQuery) params['search'] = this.searchQuery;

    this.api.get<PagedResult<Conversation>>('/conversations', params).subscribe({
      next: (r) => {
        this.conversations.set(r?.items ?? []);
        this.loading.set(false);
        // Refresh selected conversation data
        const sel = this.selectedConversation();
        if (sel) {
          const updated = (r?.items ?? []).find(c => c.conversationId === sel.conversationId);
          if (updated) this.selectedConversation.set(updated);
        }
      },
      error: () => this.loading.set(false),
    });
  }

  selectConversation(conv: Conversation): void {
    this.resetTypingIndicatorState(conv.conversationId);
    this.selectedConversation.set(conv);
    this.mobileChat.set(true);
    this.messages.set([]);
    this.messagesLoading.set(true);
    this.lastPollTimestamp = null;
    this.newMessageCount.set(0);

    this.api.get<PagedResult<ConversationMessage>>(`/conversations/${conv.conversationId}/messages`, { pageSize: '100' }).subscribe({
      next: (r) => {
        const items = (r?.items ?? []).reverse();
        this.messages.set(items);
        this.resolveMediaUrls(items);
        this.messagesLoading.set(false);
        this.shouldScroll = true;
        if (items.length > 0) {
          this.lastPollTimestamp = items[items.length - 1].timestampUtc;
        }
      },
      error: () => this.messagesLoading.set(false),
    });

    // Mark as read
    if (conv.unreadCount > 0) {
      this.api.post(`/conversations/${conv.conversationId}/read`).subscribe(() => {
        conv.unreadCount = 0;
      });
    }

    this.startMessagePolling(conv.conversationId);
  }

  deselectConversation(): void {
    this.resetTypingIndicatorState();
    this.selectedConversation.set(null);
    this.mobileChat.set(false);
    this.messages.set([]);
    this.lastPollTimestamp = null;
  }

  // ─── Send Message (Fixed: file upload flow, attachment optional) ───
  sendMessage(): void {
    if (!this.canSend()) return;
    const conv = this.selectedConversation();
    if (!conv) return;

    this.resetTypingIndicatorState(conv.conversationId);
    this.attachmentError.set(null);
    const content = this.newMessage.trim();
    const file = this.selectedFile();

    this.sending.set(true);
    this.newMessage = '';

    if (file) {
      // Upload file first, then send message with media
      this.uploadAndSend(conv, file, content);
    } else {
      // Text-only message
      const body: SendMessageRequest = { messageType: 'text', content };
      this.clearFile();
      this.api.post<ConversationMessage>(`/conversations/${conv.conversationId}/messages`, body).subscribe({
        next: (msg) => {
          if (msg) {
            this.messages.update(m => [...m, msg]);
            this.lastPollTimestamp = msg.timestampUtc;
            this.shouldScroll = true;
          }
          this.sending.set(false);
        },
        error: () => this.sending.set(false),
      });
    }
  }

  private uploadAndSend(conv: Conversation, file: File, content: string): void {
    const selectedType = this.getFileType(file);
    const reader = new FileReader();
    reader.onload = () => {
      const base64 = (reader.result as string).split(',')[1] || '';
      // Upload media via WhatsApp media endpoint
      this.api.post<any>('/whatsapp/media/upload', {
        fileName: file.name,
        contentType: file.type || 'application/octet-stream',
        base64Data: base64,
      }).subscribe({
        next: (uploadResult) => {
          const mediaId = uploadResult?.id || uploadResult?.data?.id || '';
          const body: SendMessageRequest = {
            messageType: selectedType,
            content: content || file.name,
            mediaUrl: mediaId || undefined,
            mediaMimeType: file.type,
            fileName: file.name,
          };
          if (!mediaId) {
            this.sending.set(false);
            this.attachmentError.set('Upload failed: media id was not returned.');
            return;
          }
          this.clearFile();
          this.sendConversationMessage(conv.conversationId, body);
        },
        error: (err) => {
          this.sending.set(false);
          this.attachmentError.set(err?.error?.message || 'Failed to upload attachment.');
        },
      });
    };
    reader.onerror = () => {
      this.sending.set(false);
      this.attachmentError.set('Failed to read the selected file.');
    };
    reader.readAsDataURL(file);
  }

  private sendConversationMessage(conversationId: number, body: SendMessageRequest): void {
    this.api.post<ConversationMessage>(`/conversations/${conversationId}/messages`, body).subscribe({
      next: (msg) => {
        if (msg) {
          this.messages.update(m => [...m, msg]);
          this.resolveMediaUrls([msg]);
          this.lastPollTimestamp = msg.timestampUtc;
          this.shouldScroll = true;
        }
        this.sending.set(false);
      },
      error: () => this.sending.set(false),
    });
  }

  markCurrentAsRead(): void {
    const conv = this.selectedConversation();
    if (!conv) return;
    this.api.post(`/conversations/${conv.conversationId}/read`).subscribe(() => {
      conv.unreadCount = 0;
      this.loadConversations();
    });
  }

  // ─── Agent Assignment ───
  loadAgents(): void {
    this.api.get<any[]>('/users/agents').subscribe({
      next: (agents) => this.agentOptions.set(agents ?? []),
      error: () => {},
    });
  }

  getAgentName(userId: number): string {
    const agent = this.agentOptions().find(a => a.companyUserId === userId);
    return agent?.fullName || '—';
  }

  getAssignedUserName(conv: Conversation): string {
    if (conv.assignedUser?.fullName) {
      return conv.assignedUser.fullName;
    }

    return conv.assignedUserId ? this.getAgentName(conv.assignedUserId) : 'Unassigned';
  }

  getOwnerName(conv: Conversation): string | null {
    return conv.contact?.ownerUser?.fullName ?? null;
  }

  onAssignChange(userId: number | null): void {
    const conv = this.selectedConversation();
    if (!conv) return;

    if (userId) {
      this.api.put(`/conversations/${conv.conversationId}/assign`, { userId }).subscribe({
        next: () => {
          conv.assignedUserId = userId;
          this.selectedConversation.set({ ...conv });
          this.loadConversations();
        },
      });
    } else {
      this.api.delete(`/conversations/${conv.conversationId}/assign`).subscribe({
        next: () => {
          conv.assignedUserId = null;
          this.selectedConversation.set({ ...conv });
          this.loadConversations();
        },
      });
    }
  }

  /** Non-admin user picks an unassigned conversation (self-assign). */
  pickConversation(): void {
    const conv = this.selectedConversation();
    if (!conv) return;
    this.api.post<Conversation>(`/conversations/${conv.conversationId}/pick`).subscribe({
      next: (updated) => {
        if (updated) {
          this.selectedConversation.set(updated);
        } else {
          conv.assignedUserId = this.tokenService.userId();
          this.selectedConversation.set({ ...conv });
        }
        this.loadConversations();
      },
    });
  }

  // ─── Polling ───
  private startConversationPolling(): void {
    interval(8000).pipe(
      takeUntil(this.destroy$),
      switchMap(() => {
        const params: Record<string, string> = { pageSize: '100' };
        if (this.searchQuery) params['search'] = this.searchQuery;
        return this.api.get<PagedResult<Conversation>>('/conversations', params).pipe(catchError(() => of(null)));
      }),
    ).subscribe(r => {
      if (!r) return;
      const prev = this.conversations();
      this.conversations.set(r.items ?? []);
      // Check for new unread from other conversations
      const sel = this.selectedConversation();
      for (const c of r.items ?? []) {
        const old = prev.find(p => p.conversationId === c.conversationId);
        if (c.unreadCount > (old?.unreadCount ?? 0) && c.conversationId !== sel?.conversationId) {
          this.notifService.showNotification(
            c.contactName || c.contactNumber,
            this.getConversationPreviewText(c),
            () => this.selectConversation(c),
          );
        }
        if (sel && c.conversationId === sel.conversationId) {
          this.selectedConversation.set(c);
        }
      }
    });
  }

  private startMessagePolling(conversationId: number): void {
    interval(4000).pipe(
      takeUntil(this.destroy$),
      filter(() => this.selectedConversation()?.conversationId === conversationId),
      switchMap(() => {
        // Always fetch a recent window so status transitions (sent/delivered/read)
        // are reflected even when message timestamp does not change.
        const params: Record<string, string> = { pageSize: '100' };
        return this.api.get<PagedResult<ConversationMessage>>(`/conversations/${conversationId}/messages`, params).pipe(catchError(() => of(null)));
      }),
    ).subscribe(r => {
      if (!r || !r.items?.length) return;
      const polledMsgs = r.items.reverse();
      this.resolveMediaUrls(polledMsgs);
      const currentMsgs = this.messages();
      const existingById = new Map(currentMsgs.map(m => [m.conversationMessageId, m]));
      const fresh = polledMsgs.filter(m => !existingById.has(m.conversationMessageId));

      let statusUpdated = false;
      for (const polledMsg of polledMsgs) {
        const existingMsg = existingById.get(polledMsg.conversationMessageId);
        if (!existingMsg) continue;

        if (existingMsg.status !== polledMsg.status) {
          existingMsg.status = polledMsg.status;
          statusUpdated = true;
        }

        if (existingMsg.failureReason !== polledMsg.failureReason) {
          existingMsg.failureReason = polledMsg.failureReason;
          statusUpdated = true;
        }
      }

      if (fresh.length > 0) {
        const next = [...currentMsgs, ...fresh];
        this.messages.set(next);
        this.lastPollTimestamp = fresh[fresh.length - 1].timestampUtc;

        if (this.isUserNearBottom) {
          this.shouldScroll = true;
        } else {
          this.newMessageCount.update(c => c + fresh.filter(f => f.direction === 'inbound').length);
        }

        // Notify for inbound
        const inbound = fresh.filter(f => f.direction === 'inbound');
        if (inbound.length > 0 && document.hidden) {
          const conv = this.selectedConversation();
          this.notifService.showNotification(
            conv?.contactName || conv?.contactNumber || 'Message',
            this.getMessagePreviewText(inbound[inbound.length - 1]),
          );
        } else if (inbound.length > 0) {
          this.notifService.playSound();
        }
      }
      else if (statusUpdated) {
        this.messages.set([...currentMsgs]);
      }
    });
  }

  // ─── URL Detection & Preview ───
  extractUrls(content: string | null): string[] {
    if (!content) return [];
    const matches = content.match(this.urlRegex);
    return matches ? [...new Set(matches)].slice(0, 3) : [];
  }

  extractDomain(url: string): string {
    try { return new URL(url).hostname; } catch { return url; }
  }

  renderContentWithLinks(content: string): string {
    const escaped = content
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');
    return escaped.replace(this.urlRegex, (url) =>
      `<a href="${url}" target="_blank" rel="noopener noreferrer" class="text-blue-600 dark:text-blue-400 underline hover:no-underline break-all">${url}</a>`
    );
  }

  // ─── Media helpers ───
  openMediaUrl(url: string): void {
    window.open(url, '_blank');
  }

  resolveMediaUrl(msg: ConversationMessage): string | null {
    if (!msg.mediaUrl) return null;

    const resolved = this.mediaUrls()[msg.conversationMessageId];
    if (resolved) return resolved;

    if (this.isHttpUrl(msg.mediaUrl)) return msg.mediaUrl;
    return null;
  }

  private resolveMediaUrls(list: ConversationMessage[]): void {
    for (const msg of list) {
      if (!this.isMediaType(msg.messageType) || !msg.mediaUrl) continue;
      if (this.mediaUrls()[msg.conversationMessageId]) continue;

      if (this.isHttpUrl(msg.mediaUrl)) {
        this.mediaUrls.update(map => ({ ...map, [msg.conversationMessageId]: msg.mediaUrl! }));
        continue;
      }

      if (this.pendingMediaResolves.has(msg.conversationMessageId)) continue;
      this.pendingMediaResolves.add(msg.conversationMessageId);

      const mediaId = encodeURIComponent(msg.mediaUrl);
      const url = `${environment.apiUrl}/whatsapp/media/file/${mediaId}`;

      this.http.get(url, { responseType: 'blob' }).pipe(
        catchError(() => of(null)),
      ).subscribe(blob => {
        this.pendingMediaResolves.delete(msg.conversationMessageId);
        if (!blob) return;

        const blobUrl = URL.createObjectURL(blob);
        const oldUrl = this.blobObjectUrls.get(msg.conversationMessageId);
        if (oldUrl) URL.revokeObjectURL(oldUrl);
        this.blobObjectUrls.set(msg.conversationMessageId, blobUrl);

        this.mediaUrls.update(map => ({ ...map, [msg.conversationMessageId]: blobUrl }));
      });
    }
  }

  private isHttpUrl(url: string): boolean {
    return /^https?:\/\//i.test(url);
  }

  // ─── File handling ───
  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.selectedFile.set(input.files[0]);
      this.attachmentError.set(null);
      input.value = '';
    }
  }

  clearFile(): void {
    this.selectedFile.set(null);
    this.attachmentError.set(null);
  }

  getFileType(file: File): 'image' | 'video' | 'audio' | 'document' {
    if (file.type.startsWith('image/')) return 'image';
    if (file.type.startsWith('video/')) return 'video';
    if (file.type.startsWith('audio/')) return 'audio';
    return 'document';
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  // ─── UI Helpers ───
  normalizeMessageType(type: string | null | undefined): string {
    return (type ?? '').trim().toLowerCase();
  }

  isMediaMessage(msg: ConversationMessage): boolean {
    return this.isMediaType(msg.messageType);
  }

  shouldShowTypeBadge(msg: ConversationMessage): boolean {
    const normalizedType = this.normalizeMessageType(msg.messageType);
    return !!normalizedType && normalizedType !== 'text' && !this.isMediaType(normalizedType);
  }

  getConversationPreviewText(conv: Conversation): string {
    return this.buildPreviewText(conv.lastMessageType, conv.lastMessageContent, '...');
  }

  getMessagePreviewText(msg: ConversationMessage): string {
    return this.buildPreviewText(msg.messageType, msg.content, '📩');
  }

  getDisplayContent(msg: ConversationMessage): string | null {
    const raw = msg.content?.trim();
    if (!raw) return null;

    const structured = this.parseStructuredContent(msg);
    if (!structured) {
      return raw;
    }

    return this.normalizeMessageType(msg.messageType) === 'text' ? raw : null;
  }

  getLocationPreview(msg: ConversationMessage): StructuredLocationPreview | null {
    if (this.normalizeMessageType(msg.messageType) !== 'location') {
      return null;
    }

    const structured = this.parseStructuredContent(msg);
    const node = this.pickStructuredNode(structured, 'location');

    let name = this.readStringAny(node, ['name']);
    let address = this.readStringAny(node, ['address']);
    let latitude = this.readNumberAny(node, ['latitude']);
    let longitude = this.readNumberAny(node, ['longitude']);

    if ((latitude === null || longitude === null) && msg.content) {
      const coordinates = this.extractCoordinates(msg.content);
      if (coordinates) {
        latitude = coordinates.latitude;
        longitude = coordinates.longitude;
      }
    }

    if (!name && !address && latitude === null && longitude === null) {
      const fallback = msg.content?.trim();
      if (!fallback) return null;
      name = fallback;
    }

    return {
      name,
      address,
      latitude,
      longitude,
      mapsUrl: this.buildMapsUrl(latitude, longitude),
    };
  }

  getReactionPreview(msg: ConversationMessage): StructuredReactionPreview | null {
    if (this.normalizeMessageType(msg.messageType) !== 'reaction') {
      return null;
    }

    const structured = this.parseStructuredContent(msg);
    const node = this.pickStructuredNode(structured, 'reaction');
    const emoji = this.readStringAny(node, ['emoji']) ?? msg.content?.trim() ?? '';
    const messageId = this.readStringAny(node, ['messageId', 'message_id']);

    if (!emoji) return null;
    return { emoji, messageId };
  }

  getContactsPreview(msg: ConversationMessage): StructuredContactPreview[] {
    if (this.normalizeMessageType(msg.messageType) !== 'contacts') {
      return [];
    }

    const structured = this.parseStructuredContent(msg);
    const contactsNode = this.readArrayAny(this.asRecord(structured), ['contacts']) ?? (Array.isArray(structured) ? structured : []);
    const contacts: StructuredContactPreview[] = [];

    for (const item of contactsNode) {
      const contact = this.asRecord(item);
      if (!contact) continue;

      const nameNode = this.asRecord(contact['name']);
      const name = this.readStringAny(contact, ['name'])
        ?? this.readStringAny(nameNode, ['formattedName', 'formatted_name', 'firstName', 'first_name'])
        ?? this.getMediaLabel('contacts');

      const phones = this.extractValueArray(contact['phones'], ['phone', 'waId', 'wa_id']);
      const emails = this.extractValueArray(contact['emails'], ['email']);

      contacts.push({ name, phones, emails });
      if (contacts.length >= 3) break;
    }

    return contacts;
  }

  getOrderPreview(msg: ConversationMessage): StructuredOrderPreview | null {
    if (this.normalizeMessageType(msg.messageType) !== 'order') {
      return null;
    }

    const structured = this.parseStructuredContent(msg);
    const node = this.pickStructuredNode(structured, 'order');
    const title = this.readStringAny(node, ['text']) ?? this.getMediaLabel('order');
    const catalogId = this.readStringAny(node, ['catalogId', 'catalog_id']);
    const productItems = this.readArrayAny(node, ['productItems', 'product_items']) ?? [];
    const itemCount = productItems.reduce<number>((sum, item) => {
      const itemNode = this.asRecord(item);
      const qty = this.readNumberAny(itemNode, ['quantity']);
      return sum + (qty ?? 1);
    }, 0);

    return {
      title,
      itemCount: itemCount > 0 ? itemCount : productItems.length,
      catalogId,
    };
  }

  getSystemPreview(msg: ConversationMessage): StructuredSystemPreview | null {
    if (this.normalizeMessageType(msg.messageType) !== 'system') {
      return null;
    }

    const structured = this.parseStructuredContent(msg);
    const node = this.pickStructuredNode(structured, 'system');
    const body = this.readStringAny(node, ['body']) ?? msg.content?.trim() ?? '';
    if (!body) return null;
    return { body };
  }

  getStructuredDetailRows(msg: ConversationMessage): StructuredDetailRow[] {
    const normalizedType = this.normalizeMessageType(msg.messageType);
    if (['location', 'contacts', 'reaction', 'order', 'system'].includes(normalizedType)) {
      return [];
    }

    const structured = this.parseStructuredContent(msg);
    if (!structured) return [];

    const rows: StructuredDetailRow[] = [];
    this.flattenStructuredRows(structured, '', rows, 0);
    return rows;
  }

  private isMediaType(type: string | null | undefined): boolean {
    return InboxComponent.MEDIA_MESSAGE_TYPES.has(this.normalizeMessageType(type));
  }

  private buildPreviewText(type: string | null | undefined, content: string | null | undefined, fallback: string): string {
    const normalizedType = this.normalizeMessageType(type);
    const defaultText = normalizedType && normalizedType !== 'text'
      ? `[${this.getMediaLabel(normalizedType)}]`
      : fallback;

    const raw = content?.trim();
    if (!raw) return defaultText;

    const parsed = this.tryParseJson(raw);
    if (!parsed) return raw;

    const summary = this.summarizeStructuredPreview(normalizedType, parsed);
    return summary ?? defaultText;
  }

  private summarizeStructuredPreview(normalizedType: string, parsed: unknown): string | null {
    switch (normalizedType) {
      case 'location': {
        const node = this.pickStructuredNode(parsed, 'location');
        const name = this.readStringAny(node, ['name']);
        const address = this.readStringAny(node, ['address']);
        const latitude = this.readNumberAny(node, ['latitude']);
        const longitude = this.readNumberAny(node, ['longitude']);

        if (name && address) return `${name} - ${address}`;
        if (name) return name;
        if (address) return address;
        if (latitude !== null && longitude !== null) return `${latitude}, ${longitude}`;
        return null;
      }
      case 'contacts': {
        const contactsNode = this.readArrayAny(this.asRecord(parsed), ['contacts']) ?? (Array.isArray(parsed) ? parsed : []);
        const names = contactsNode
          .map(item => {
            const contact = this.asRecord(item);
            if (!contact) return null;
            const nameNode = this.asRecord(contact['name']);
            return this.readStringAny(contact, ['name'])
              ?? this.readStringAny(nameNode, ['formattedName', 'formatted_name', 'firstName', 'first_name']);
          })
          .filter((name): name is string => !!name)
          .slice(0, 2);

        if (names.length === 0) return null;
        const suffix = contactsNode.length > names.length ? ` +${contactsNode.length - names.length}` : '';
        return names.join(', ') + suffix;
      }
      case 'reaction': {
        const node = this.pickStructuredNode(parsed, 'reaction');
        const emoji = this.readStringAny(node, ['emoji']);
        const messageId = this.readStringAny(node, ['messageId', 'message_id']);
        if (!emoji) return null;
        return messageId ? `${emoji} (${messageId})` : emoji;
      }
      case 'order': {
        const node = this.pickStructuredNode(parsed, 'order');
        const title = this.readStringAny(node, ['text']) ?? this.getMediaLabel('order');
        const productItems = this.readArrayAny(node, ['productItems', 'product_items']) ?? [];
        const itemCount = productItems.reduce<number>((sum, item) => {
          const itemNode = this.asRecord(item);
          const qty = this.readNumberAny(itemNode, ['quantity']);
          return sum + (qty ?? 1);
        }, 0);
        const safeCount = itemCount > 0 ? itemCount : productItems.length;
        return safeCount > 0 ? `${title} (${safeCount})` : title;
      }
      case 'system': {
        const node = this.pickStructuredNode(parsed, 'system');
        return this.readStringAny(node, ['body']);
      }
      default: {
        const rows: StructuredDetailRow[] = [];
        this.flattenStructuredRows(parsed, '', rows, 0);
        return rows.length > 0 ? rows[0].value : null;
      }
    }
  }

  private parseStructuredContent(msg: ConversationMessage): unknown | null {
    const raw = msg.content?.trim();
    if (!raw) return null;

    const cached = this.structuredContentCache.get(msg.conversationMessageId);
    if (cached && cached.raw === raw) {
      return cached.parsed;
    }

    const parsed = this.tryParseJson(raw);
    this.structuredContentCache.set(msg.conversationMessageId, { raw, parsed });
    return parsed;
  }

  private tryParseJson(raw: string): unknown | null {
    const trimmed = raw.trim();
    if (!(trimmed.startsWith('{') || trimmed.startsWith('['))) {
      return null;
    }

    try {
      return JSON.parse(trimmed) as unknown;
    } catch {
      return null;
    }
  }

  private pickStructuredNode(structured: unknown, nodeKey: string): Record<string, unknown> | null {
    const root = this.asRecord(structured);
    if (!root) return null;
    const nested = this.asRecord(root[nodeKey]);
    return nested ?? root;
  }

  private asRecord(value: unknown): Record<string, unknown> | null {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return null;
    return value as Record<string, unknown>;
  }

  private readStringAny(source: Record<string, unknown> | null, keys: string[]): string | null {
    if (!source) return null;
    for (const key of keys) {
      const value = source[key];
      if (typeof value === 'string' && value.trim()) {
        return value.trim();
      }
    }
    return null;
  }

  private readNumberAny(source: Record<string, unknown> | null, keys: string[]): number | null {
    if (!source) return null;
    for (const key of keys) {
      const value = source[key];
      if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
      }
      if (typeof value === 'string' && value.trim()) {
        const parsed = Number(value);
        if (Number.isFinite(parsed)) {
          return parsed;
        }
      }
    }
    return null;
  }

  private readArrayAny(source: Record<string, unknown> | null, keys: string[]): unknown[] | null {
    if (!source) return null;
    for (const key of keys) {
      const value = source[key];
      if (Array.isArray(value)) {
        return value;
      }
    }
    return null;
  }

  private extractValueArray(value: unknown, keys: string[]): string[] {
    if (!Array.isArray(value)) return [];

    const output: string[] = [];
    for (const item of value) {
      if (typeof item === 'string' && item.trim()) {
        output.push(item.trim());
        continue;
      }

      const node = this.asRecord(item);
      if (!node) continue;
      const extracted = this.readStringAny(node, keys);
      if (extracted) output.push(extracted);
    }

    return [...new Set(output)];
  }

  private extractCoordinates(content: string): { latitude: number; longitude: number } | null {
    const match = content.match(/(-?\d{1,2}(?:\.\d+)?)[,\s]+(-?\d{1,3}(?:\.\d+)?)/);
    if (!match) return null;

    const latitude = Number(match[1]);
    const longitude = Number(match[2]);
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return null;
    if (Math.abs(latitude) > 90 || Math.abs(longitude) > 180) return null;

    return { latitude, longitude };
  }

  private buildMapsUrl(latitude: number | null, longitude: number | null): string | null {
    if (latitude === null || longitude === null) return null;
    return `https://maps.google.com/?q=${encodeURIComponent(`${latitude},${longitude}`)}`;
  }

  private flattenStructuredRows(value: unknown, path: string, rows: StructuredDetailRow[], depth: number): void {
    if (rows.length >= 8 || depth > 3 || value === null || value === undefined) {
      return;
    }

    if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
      const rendered = this.convertStructuredValue(value);
      if (rendered) {
        rows.push({
          label: this.formatStructuredLabel(path || 'value'),
          value: rendered,
        });
      }
      return;
    }

    if (Array.isArray(value)) {
      const preview = value.slice(0, 3);
      for (let i = 0; i < preview.length && rows.length < 8; i++) {
        const nextPath = path ? `${path}[${i}]` : `[${i}]`;
        this.flattenStructuredRows(preview[i], nextPath, rows, depth + 1);
      }
      if (value.length > preview.length && rows.length < 8) {
        rows.push({
          label: this.formatStructuredLabel(path || 'items'),
          value: `+${value.length - preview.length}`,
        });
      }
      return;
    }

    const record = this.asRecord(value);
    if (!record) return;

    for (const [key, nestedValue] of Object.entries(record)) {
      if (rows.length >= 8) break;
      if (key === 'type') continue;
      const nextPath = path ? `${path}.${key}` : key;
      this.flattenStructuredRows(nestedValue, nextPath, rows, depth + 1);
    }
  }

  private convertStructuredValue(value: string | number | boolean): string {
    if (typeof value === 'string') return value.trim();
    if (typeof value === 'number') return String(value);
    return value ? 'true' : 'false';
  }

  private formatStructuredLabel(path: string): string {
    const withoutIndexes = path.replace(/\[\d+\]/g, '');
    const lastSegment = withoutIndexes.split('.').filter(Boolean).pop() ?? withoutIndexes;
    return this.startCase(lastSegment || 'value');
  }

  private startCase(value: string): string {
    return value
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/[_-]+/g, ' ')
      .trim()
      .split(/\s+/)
      .filter(Boolean)
      .map(part => part[0].toUpperCase() + part.slice(1).toLowerCase())
      .join(' ');
  }

  getInitials(name: string): string {
    if (!name) return '?';
    // For phone numbers, show last 2 digits
    if (/^\+?\d/.test(name)) return name.slice(-2);
    return name.split(' ').filter(Boolean).slice(0, 2).map(w => w[0]).join('').toUpperCase();
  }

  getAvatarClasses(conv: Conversation): string {
    const colors = [
      'bg-[#dfe5e7] text-[#54656f] dark:bg-[#2a3942] dark:text-[#d1d7db]',
      'bg-[#d9fdd3] text-[#005c4b] dark:bg-[#005c4b] dark:text-[#d9fdd3]',
      'bg-[#e9defa] text-[#5a4d7a] dark:bg-[#3b344d] dark:text-[#ddd4f0]',
      'bg-[#fff3c4] text-[#7a6313] dark:bg-[#4f4320] dark:text-[#f2e0a1]',
      'bg-[#ffd9e0] text-[#8a4056] dark:bg-[#4d2b35] dark:text-[#f0c4cf]',
      'bg-[#d8efff] text-[#1f5f82] dark:bg-[#183444] dark:text-[#b9def7]',
    ];
    return colors[conv.conversationId % colors.length];
  }

  getBubbleTailFill(direction: string): string {
    if (this.isDark()) {
      return direction === 'outbound' ? '#005c4b' : '#202c33';
    }
    return direction === 'outbound' ? '#d9fdd3' : '#ffffff';
  }

  getMediaIcon(type: string): string {
    switch (this.normalizeMessageType(type)) {
      case 'text': return 'pi-comment';
      case 'template': return 'pi-clone';
      case 'image': return 'pi-image';
      case 'video': return 'pi-video';
      case 'audio': return 'pi-volume-up';
      case 'document': return 'pi-file';
      case 'sticker': return 'pi-face-smile';
      case 'interactive': return 'pi-th-large';
      case 'button': return 'pi-stop-circle';
      case 'location': return 'pi-map-marker';
      case 'contacts': return 'pi-users';
      case 'reaction': return 'pi-heart';
      case 'order': return 'pi-shopping-bag';
      case 'system': return 'pi-cog';
      case 'request_welcome': return 'pi-megaphone';
      default: return 'pi-paperclip';
    }
  }

  getMediaLabel(type: string): string {
    const normalizedType = this.normalizeMessageType(type);
    if (!normalizedType) return '';

    const key = 'inbox.media.' + normalizedType;
    const translated = this.translate.instant(key);
    return translated !== key ? translated : this.startCase(normalizedType);
  }

  getAttachmentDisplayName(msg: ConversationMessage): string {
    return this.extractFileName(msg) ?? this.getMediaLabel(msg.messageType);
  }

  getDownloadFileName(msg: ConversationMessage): string {
    return this.extractFileName(msg) ?? `${msg.messageType || 'attachment'}-${msg.conversationMessageId}${this.getFileExtension(msg)}`;
  }

  private extractFileName(msg: ConversationMessage): string | null {
    const directName = msg.fileName?.trim();
    if (directName) return directName;

    const content = msg.content?.trim();
    if (this.normalizeMessageType(msg.messageType) === 'document' && content && !content.includes('\n') && /\.[a-z0-9]{1,8}$/i.test(content)) {
      return content;
    }

    return null;
  }

  private getFileExtension(msg: ConversationMessage): string {
    const mime = (msg.mediaMimeType ?? '').toLowerCase();
    const map: Record<string, string> = {
      'application/pdf': '.pdf',
      'text/plain': '.txt',
      'text/csv': '.csv',
      'application/msword': '.doc',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document': '.docx',
      'application/vnd.ms-excel': '.xls',
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': '.xlsx',
      'application/vnd.ms-powerpoint': '.ppt',
      'application/vnd.openxmlformats-officedocument.presentationml.presentation': '.pptx',
      'application/zip': '.zip',
      'application/x-zip-compressed': '.zip',
      'image/jpeg': '.jpg',
      'image/png': '.png',
      'image/webp': '.webp',
      'video/mp4': '.mp4',
      'audio/mpeg': '.mp3',
      'audio/ogg': '.ogg',
      'audio/mp4': '.m4a',
    };

    return map[mime] ?? '';
  }

  normalizeMessageStatus(status: string | null | undefined): string {
    return (status ?? '').trim().toLowerCase();
  }

  formatRelativeTime(dateStr: string | null): string {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMin = Math.floor(diffMs / 60000);
    if (diffMin < 1) return this.translate.instant('inbox.time.now');
    if (diffMin < 60) return diffMin + this.translate.instant('inbox.time.m');
    const diffH = Math.floor(diffMin / 60);
    if (diffH < 24) return diffH + this.translate.instant('inbox.time.h');
    if (diffH < 48) return this.translate.instant('inbox.time.yesterday');
    return date.toLocaleDateString();
  }

  formatTime(dateStr: string): string {
    return new Date(dateStr).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  formatDateLabel(dateStr: string): string {
    const date = new Date(dateStr);
    const today = new Date();
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);
    if (date.toDateString() === today.toDateString()) return this.translate.instant('inbox.time.today');
    if (date.toDateString() === yesterday.toDateString()) return this.translate.instant('inbox.time.yesterday');
    return date.toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
  }

  isNewDay(index: number): boolean {
    if (index === 0) return true;
    const curr = new Date(this.messages()[index].timestampUtc).toDateString();
    const prev = new Date(this.messages()[index - 1].timestampUtc).toDateString();
    return curr !== prev;
  }

  isFirstInGroup(index: number): boolean {
    if (index === 0) return true;
    const curr = this.messages()[index];
    const prev = this.messages()[index - 1];
    return curr.direction !== prev.direction || this.isNewDay(index);
  }

  // ─── Scroll ───
  scrollToBottom(smooth: boolean): void {
    const el = this.messageContainer?.nativeElement;
    if (!el) return;
    el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
    this.newMessageCount.set(0);
  }

  onChatScroll(): void {
    const el = this.messageContainer?.nativeElement;
    if (!el) return;
    const distFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    this.isUserNearBottom = distFromBottom < 80;
    this.showScrollDown.set(distFromBottom > 300);
    if (this.isUserNearBottom) this.newMessageCount.set(0);
  }

  // ─── Input ───
  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  onComposerInput(event: Event): void {
    this.autoResize(event);
    this.autoDetectDir(event);
    this.notifyTypingIndicator();
  }

  autoResize(event: Event): void {
    const el = event.target as HTMLTextAreaElement;
    el.style.height = 'auto';
    el.style.height = Math.min(el.scrollHeight, 120) + 'px';
  }

  // ─── RTL Auto-Detect ───
  inputDir = signal<'ltr' | 'rtl'>('ltr');

  private static RTL_REGEX = /[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF\u0590-\u05FF]/;

  detectDir(text: string | null): 'ltr' | 'rtl' {
    if (!text) return 'ltr';
    const firstMeaningful = text.replace(/[\s\d\p{P}\p{S}]/gu, '').charAt(0);
    return InboxComponent.RTL_REGEX.test(firstMeaningful) ? 'rtl' : 'ltr';
  }

  autoDetectDir(event: Event): void {
    const val = (event.target as HTMLTextAreaElement).value;
    this.inputDir.set(this.detectDir(val));
  }

  // ─── 24h Window Helpers (per-conversation) ───
  private notifyTypingIndicator(): void {
    const conv = this.selectedConversation();
    if (!conv || !this.canInteract() || !this.windowAvailable() || this.typingIndicatorInFlight) {
      return;
    }

    if (!this.newMessage.trim()) {
      return;
    }

    if (this.typingConversationId !== conv.conversationId) {
      this.resetTypingIndicatorState(conv.conversationId);
    }

    if ((Date.now() - this.lastTypingIndicatorAt) < InboxComponent.TYPING_INDICATOR_THROTTLE_MS) {
      return;
    }

    this.typingIndicatorInFlight = true;
    this.api.post<boolean>(`/conversations/${conv.conversationId}/typing-indicator`).subscribe({
      next: () => {
        this.lastTypingIndicatorAt = Date.now();
        this.typingIndicatorInFlight = false;
      },
      error: () => {
        this.typingIndicatorInFlight = false;
      },
    });
  }

  private resetTypingIndicatorState(conversationId: number | null = null): void {
    this.typingConversationId = conversationId;
    this.lastTypingIndicatorAt = 0;
    this.typingIndicatorInFlight = false;
  }

  isWindowOpen(lastInbound: string | null): boolean {
    if (!lastInbound) return false;
    const elapsed = Date.now() - new Date(lastInbound).getTime();
    return elapsed < 24 * 60 * 60 * 1000;
  }

  getConvWindowText(lastInbound: string | null): string {
    if (!lastInbound) return '';
    const elapsed = Date.now() - new Date(lastInbound).getTime();
    const remaining = 24 * 60 * 60 * 1000 - elapsed;
    if (remaining <= 0) return this.translate.instant('inbox.windowBadgeExpired') || 'Expired';
    const h = Math.floor(remaining / 3_600_000);
    const m = Math.floor((remaining % 3_600_000) / 60_000);
    return h > 0 ? `${h}h ${m}m` : `${m}m`;
  }
}

