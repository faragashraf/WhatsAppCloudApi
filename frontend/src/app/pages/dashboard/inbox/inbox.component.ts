import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild, ElementRef, AfterViewChecked, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TooltipModule } from 'primeng/tooltip';
import { SelectModule } from 'primeng/select';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, interval, switchMap, takeUntil, catchError, of, filter } from 'rxjs';
import { ApiService, NotificationManagerService, TokenService, PermissionService } from '../../../core/services';
import { Conversation, ConversationMessage, PagedResult, SendMessageRequest } from '../../../core/models';
import { DomSanitizer } from '@angular/platform-browser';

@Component({
  selector: 'app-inbox',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ButtonModule, ProgressSpinnerModule, TooltipModule, FormsModule, TranslateModule, SelectModule],
  template: `
    <div class="h-[calc(100vh-128px)] flex rounded-2xl overflow-hidden border border-[var(--app-border)] dark:border-slate-700/60 bg-[var(--app-surface)] dark:bg-slate-900/70 shadow-[0_20px_44px_-24px_rgba(13,37,63,0.48)]">
      <!-- ━━ Left: Conversation List ━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->
      <div class="w-full md:w-[340px] lg:w-[380px] border-e border-[var(--app-border)] dark:border-slate-700/60 flex flex-col bg-[var(--app-surface)] dark:bg-slate-900/85"
        [class.max-md:hidden]="mobileChat() && selectedConversation()">

        <!-- Header -->
        <div class="px-4 pt-4 pb-3 bg-gradient-to-b from-[var(--app-primary)] to-[var(--app-primary-strong)]">
          <div class="flex items-center justify-between mb-3">
            <h2 class="text-lg font-bold text-white">{{ 'inbox.title' | translate }}</h2>
            <span class="text-emerald-100/90 text-xs font-medium">{{ conversations().length }} {{ 'inbox.conversations' | translate }}</span>
          </div>
          <div class="relative">
            <i class="pi pi-search absolute start-3 top-2 !text-[18px] text-emerald-100/80"></i>
            <input [(ngModel)]="searchQuery" (input)="loadConversations()"
              [placeholder]="'inbox.search' | translate"
              class="w-full ps-10 pe-4 py-2 bg-white/20 placeholder-emerald-100/80 text-white rounded-xl text-sm border border-white/20 focus:ring-2 focus:ring-white/35 outline-none backdrop-blur-sm" />
          </div>
          <!-- Filter chips -->
          <div class="flex gap-2 mt-3">
            <button (click)="filterStatus.set('all')"
              class="px-3 py-1 rounded-full text-xs font-medium transition-all"
              [class]="filterStatus() === 'all' ? 'bg-white text-[var(--app-primary-strong)] shadow-sm' : 'bg-white/18 text-emerald-100 hover:bg-white/28'">
              {{ 'inbox.all' | translate }}
            </button>
            <button (click)="filterStatus.set('unread')"
              class="px-3 py-1 rounded-full text-xs font-medium transition-all"
              [class]="filterStatus() === 'unread' ? 'bg-white text-[var(--app-primary-strong)] shadow-sm' : 'bg-white/18 text-emerald-100 hover:bg-white/28'">
              {{ 'inbox.unread' | translate }}
            </button>
          </div>
        </div>

        <!-- Conversation list -->
        <div class="flex-1 overflow-y-auto">
          @if (loading()) {
            <div class="flex justify-center py-16"><p-progressSpinner [style]="{'width':'28px','height':'28px'}" strokeWidth="4" /></div>
          } @else if (filteredConversations().length === 0) {
            <div class="text-center py-16 text-slate-400 dark:text-slate-500">
              <i class="pi pi-comments !text-[48px] mb-2 opacity-40"></i>
              <p class="text-sm">{{ 'inbox.noConversations' | translate }}</p>
            </div>
          } @else {
            @for (group of groupedConversations(); track group.phoneId) {
              <!-- Group header -->
              <div (click)="toggleGroup(group.phoneId)"
                class="flex items-center gap-2 px-4 py-2 bg-[var(--app-surface-muted)] dark:bg-slate-800/70 border-b border-[var(--app-border)] dark:border-slate-700/50 cursor-pointer hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-800 transition-colors select-none">
                <i class="pi pi-phone !text-[14px] text-emerald-600 dark:text-emerald-400"></i>
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300 flex-1 truncate" [pTooltip]="group.label">{{ group.label }}</span>
                <span class="bg-[var(--app-primary-soft)] dark:bg-emerald-900/40 text-[var(--app-primary-strong)] dark:text-emerald-300 rounded-full text-[10px] min-w-5 h-5 px-1.5 flex items-center justify-center font-bold">
                  {{ group.conversations.length }}
                </span>
                <i class="pi !text-[12px] text-slate-400 transition-transform"
                  [ngClass]="isGroupCollapsed(group.phoneId) ? 'pi-chevron-down' : 'pi-chevron-up'"></i>
              </div>
              @if (!isGroupCollapsed(group.phoneId)) {
                @for (conv of group.conversations; track conv.conversationId) {
                  <button (click)="selectConversation(conv)"
                    class="w-full flex items-center gap-3 px-4 py-3 border-b border-slate-100 dark:border-slate-800 hover:bg-[var(--app-primary-soft)] dark:hover:bg-emerald-950/20 transition-all text-start group"
                    [ngClass]="selectedConversation()?.conversationId === conv.conversationId ? 'bg-[var(--app-primary-soft)] dark:bg-emerald-950/30' : ''">
                    <!-- Avatar -->
                    <div class="w-12 h-12 rounded-full flex items-center justify-center shrink-0 font-bold text-sm shadow-inner"
                      [class]="getAvatarClasses(conv)">
                      {{ getInitials(conv.contactName || conv.contactNumber) }}
                    </div>
                    <!-- Info -->
                    <div class="flex-1 min-w-0">
                      <div class="flex items-center justify-between gap-2">
                        <span class="text-[13px] font-semibold text-slate-900 dark:text-white truncate"
                          [pTooltip]="conv.contactName || conv.contactNumber">
                          {{ conv.contactName || conv.contactNumber }}
                        </span>
                        <span class="text-[10px] text-slate-400 dark:text-slate-500 whitespace-nowrap shrink-0"
                          [class.text-emerald-600]="conv.unreadCount > 0"
                          [class.font-semibold]="conv.unreadCount > 0">
                          {{ formatRelativeTime(conv.lastMessageAtUtc) }}
                        </span>
                      </div>
                      <div class="flex items-center justify-between gap-2 mt-0.5">
                        <p class="text-xs text-slate-500 dark:text-slate-400 truncate"
                          [pTooltip]="conv.lastMessageContent || ''">
                          @if (conv.lastMessageType && conv.lastMessageType !== 'text') {
                            <i class="pi !text-[13px] !w-3.5 !h-3.5 align-middle me-0.5 opacity-60" [ngClass]="getMediaIcon(conv.lastMessageType)"></i>
                          }
                          {{ conv.lastMessageContent || '...' }}
                        </p>
                        @if (conv.unreadCount > 0) {
                          <span class="bg-[var(--app-primary)] text-white rounded-full text-[10px] min-w-5 h-5 px-1.5 flex items-center justify-center font-bold shrink-0 shadow-sm">
                            {{ conv.unreadCount }}
                          </span>
                        }
                      </div>
                      <!-- 24h Window Indicator + Agent -->
                      <div class="flex items-center gap-2 mt-0.5">
                        @if (conv.lastInboundMessageAtUtc) {
                          <span class="inline-flex items-center gap-0.5 text-[10px] font-medium px-1.5 py-0.5 rounded-full"
                            [class]="isWindowOpen(conv.lastInboundMessageAtUtc)
                              ? 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400'
                              : 'bg-red-100 dark:bg-red-900/30 text-red-500 dark:text-red-400'">
                            <i class="pi !text-[9px]" [ngClass]="isWindowOpen(conv.lastInboundMessageAtUtc) ? 'pi-clock' : 'pi-exclamation-triangle'"></i>
                            {{ getConvWindowText(conv.lastInboundMessageAtUtc) }}
                          </span>
                        }
                        @if (conv.assignedUserId) {
                          <span class="inline-flex items-center gap-0.5 text-[10px] text-blue-600 dark:text-blue-400 font-medium">
                            <i class="pi pi-user !text-[10px]"></i>
                            {{ getAgentName(conv.assignedUserId) }}
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
      <div class="flex-1 flex flex-col min-w-0"
        [class.max-md:hidden]="!selectedConversation()">

        @if (!selectedConversation()) {
          <!-- Empty State -->
          <div class="flex-1 flex items-center justify-center bg-gradient-to-br from-[var(--app-surface-muted)] to-[var(--app-bg-soft)] dark:from-slate-800 dark:to-slate-900">
            <div class="text-center max-w-sm px-6">
              <div class="w-24 h-24 mx-auto mb-6 rounded-full bg-[var(--app-primary-soft)] dark:bg-emerald-900/30 flex items-center justify-center">
                <i class="pi pi-comments !text-[48px] text-emerald-500/60"></i>
              </div>
              <h3 class="text-xl font-bold text-slate-700 dark:text-slate-300 mb-2">{{ 'inbox.emptyTitle' | translate }}</h3>
              <p class="text-sm text-slate-400">{{ 'inbox.emptySubtitle' | translate }}</p>
            </div>
          </div>
        } @else {
          <!-- Chat Header -->
          <div class="h-[60px] px-4 flex items-center gap-3 bg-gradient-to-r from-[var(--app-primary)] to-[var(--app-primary-strong)] text-white shadow-md">
            <!-- Back (mobile) -->
            <button (click)="deselectConversation()" class="md:hidden w-8 h-8 rounded-full hover:bg-white/10 flex items-center justify-center">
              <i class="pi pi-arrow-left !text-[20px]"></i>
            </button>
            <!-- Avatar -->
            <div class="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center font-bold text-sm backdrop-blur-sm">
              {{ getInitials(selectedConversation()!.contactName || selectedConversation()!.contactNumber) }}
            </div>
            <!-- Info -->
            <div class="flex-1 min-w-0">
              <h3 class="text-sm font-semibold truncate" [pTooltip]="selectedConversation()!.contactName || selectedConversation()!.contactNumber">{{ selectedConversation()!.contactName || selectedConversation()!.contactNumber }}</h3>
              <p class="text-[11px] text-emerald-100/80 truncate" [pTooltip]="selectedConversation()!.contactNumber">{{ selectedConversation()!.contactNumber }}</p>
            </div>
            <!-- 24h Window Countdown -->
            @if (selectedConversation()!.lastInboundMessageAtUtc) {
              <div class="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold"
                [class]="windowAvailable() ? 'bg-emerald-500/35 text-emerald-100' : 'bg-red-500/35 text-red-100'">
                <i class="pi !text-[12px]" [ngClass]="windowAvailable() ? 'pi-clock' : 'pi-exclamation-triangle'"></i>
                <span>{{ windowCountdown() }}</span>
              </div>
            }
            <!-- Actions -->
            <button (click)="markCurrentAsRead()" [pTooltip]="'inbox.markRead' | translate"
              class="w-8 h-8 rounded-full hover:bg-white/10 flex items-center justify-center">
              <i class="pi pi-check !text-[18px]"></i>
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
                styleClass="w-40 !bg-white/20 !border-white/35 text-white [&_.p-select-label]:!text-white [&_.p-select-label]:!text-xs [&_.p-select-trigger-icon]:!text-white/80"
              />
            </div>
            }
            <!-- Pick button (non-admin, unassigned conversations) -->
            @if (!permService.isAdmin && !selectedConversation()!.assignedUserId) {
              <button (click)="pickConversation()"
                class="px-3 py-1.5 rounded-full bg-white/20 hover:bg-white/30 text-white text-xs font-semibold transition-colors flex items-center gap-1.5"
                [pTooltip]="'inbox.pickTooltip' | translate">
                <i class="pi pi-hand !text-[14px]"></i>
                <span>{{ 'inbox.pick' | translate }}</span>
              </button>
            }
            <!-- Picked/Assigned badge for non-admin -->
            @if (!permService.isAdmin && selectedConversation()!.assignedUserId) {
              <div class="px-2.5 py-1 rounded-full text-[11px] font-semibold"
                [class]="selectedConversation()!.assignedUserId === tokenService.userId()
                  ? 'bg-emerald-500/30 text-emerald-100'
                  : 'bg-amber-500/30 text-amber-100'">
                @if (selectedConversation()!.assignedUserId === tokenService.userId()) {
                  <i class="pi pi-check-circle !text-[11px]"></i> {{ 'inbox.pickedByYou' | translate }}
                } @else {
                  <i class="pi pi-user !text-[11px]"></i> {{ getAgentName(selectedConversation()!.assignedUserId!) }}
                }
              </div>
            }
          </div>

          <!-- Messages -->
          <div #messageContainer
            (scroll)="onChatScroll()"
            class="flex-1 overflow-y-auto px-4 py-3 space-y-0.5 relative"
            style="background-color: #eef4fb; background-image: radial-gradient(rgba(16,168,97,0.08) 0.8px, transparent 0.8px), radial-gradient(rgba(13,139,202,0.05) 0.8px, transparent 0.8px); background-size: 22px 22px, 28px 28px; background-position: 0 0, 11px 11px;">

            <!-- Dark mode override bg -->
            <div class="absolute inset-0 bg-slate-800 opacity-0 dark:opacity-100 -z-10"></div>

            @if (messagesLoading()) {
              <div class="flex justify-center py-16"><p-progressSpinner [style]="{'width':'24px','height':'24px'}" strokeWidth="4" /></div>
            } @else if (messages().length === 0) {
              <div class="flex justify-center py-16">
                <div class="bg-white/90 dark:bg-slate-700/80 backdrop-blur-sm rounded-lg px-5 py-3 shadow-sm border border-[var(--app-border)] dark:border-slate-700 text-center">
                  <i class="pi pi-sparkles !text-[28px] text-emerald-500/60 mb-1"></i>
                  <p class="text-xs text-slate-500 dark:text-slate-400">{{ 'inbox.startConversation' | translate }}</p>
                </div>
              </div>
            } @else {
              @for (msg of messages(); track msg.conversationMessageId; let i = $index) {
                <!-- Date separator -->
                @if (isNewDay(i)) {
                  <div class="flex justify-center py-3">
                    <span class="bg-white/95 dark:bg-slate-700/90 text-slate-600 dark:text-slate-300 text-[11px] px-4 py-1.5 rounded-lg shadow-sm backdrop-blur-sm border border-[var(--app-border)] dark:border-slate-700 font-medium">
                      {{ formatDateLabel(msg.timestampUtc) }}
                    </span>
                  </div>
                }
                <!-- Bubble -->
                <div class="flex mb-[2px]"
                  [class.justify-end]="msg.direction === 'outbound'"
                  [class.justify-start]="msg.direction === 'inbound'">
                  <div class="max-w-[65%] rounded-lg px-3 pt-1.5 pb-1 text-[13.5px] leading-[19px] shadow-sm relative border"
                    [class]="msg.direction === 'outbound'
                      ? 'bg-[var(--wa-light-green)] dark:bg-emerald-900/70 text-slate-900 dark:text-slate-100 border-emerald-200/70 dark:border-emerald-800/60'
                      : 'bg-white dark:bg-slate-700 text-slate-900 dark:text-slate-100 border-[var(--app-border)] dark:border-slate-600/70'">
                    <!-- Tail -->
                    @if (isFirstInGroup(i)) {
                      <div class="absolute top-0 w-3 h-3"
                        [class]="msg.direction === 'outbound' ? '-end-1.5' : '-start-1.5'">
                        <svg viewBox="0 0 12 12" class="w-3 h-3">
                          @if (msg.direction === 'outbound') {
                            <path d="M0,0 L12,0 C6,4 3,8 0,12 Z"
                              [attr.fill]="isDark() ? 'rgb(6 78 59 / 0.7)' : '#d8f8e6'" />
                          } @else {
                            <path d="M12,0 L0,0 C6,4 9,8 12,12 Z"
                              [attr.fill]="isDark() ? 'rgb(51 65 85)' : 'white'" />
                          }
                        </svg>
                      </div>
                    }
                    <!-- Media preview -->
                    @if (msg.messageType !== 'text') {
                      <div class="mb-1.5 rounded-md overflow-hidden">
                        @if (msg.mediaUrl) {
                          @switch (msg.messageType) {
                            @case ('image') {
                              <img [src]="msg.mediaUrl" alt="Image" class="max-w-full rounded-md cursor-pointer hover:opacity-90 transition-opacity" loading="lazy"
                                (click)="openMediaUrl(msg.mediaUrl!)" />
                            }
                            @case ('video') {
                              <video [src]="msg.mediaUrl" controls class="max-w-full rounded-md" preload="metadata"></video>
                            }
                            @case ('audio') {
                              <audio [src]="msg.mediaUrl" controls class="w-full min-w-[200px]" preload="metadata"></audio>
                            }
                            @default {
                              <a [href]="msg.mediaUrl" target="_blank" rel="noopener noreferrer" download
                                class="flex items-center gap-3 p-3 rounded-md cursor-pointer hover:opacity-80 transition-opacity no-underline"
                                [class]="msg.direction === 'outbound'
                                  ? 'bg-emerald-500/10 dark:bg-emerald-800/30'
                                  : 'bg-slate-100 dark:bg-slate-600/40'">
                                <div class="w-10 h-10 rounded-lg bg-emerald-100 dark:bg-emerald-900/40 flex items-center justify-center">
                                  <i class="pi pi-file !text-[22px] text-emerald-600 dark:text-emerald-400"></i>
                                </div>
                                <div class="flex-1 min-w-0">
                                  <span class="text-xs font-medium text-slate-700 dark:text-slate-200 block truncate">{{ getMediaLabel(msg.messageType) }}</span>
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
                    <!-- Content with URL detection -->
                    @if (msg.content) {
                      <p class="whitespace-pre-wrap break-words" [dir]="detectDir(msg.content)" [innerHTML]="renderContentWithLinks(msg.content)"></p>
                    }
                    <!-- URL Previews -->
                    @for (url of extractUrls(msg.content); track url) {
                      <a [href]="url" target="_blank" rel="noopener noreferrer"
                        class="mt-1.5 block rounded-md border overflow-hidden no-underline transition-opacity hover:opacity-80"
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
                    <!-- Time + Status -->
                    <div class="flex items-center justify-end gap-1 -mb-0.5 mt-0.5 select-none">
                      <span class="text-[10.5px] leading-none"
                        [class]="msg.direction === 'outbound' ? 'text-emerald-800/40 dark:text-emerald-300/40' : 'text-slate-400 dark:text-slate-500'">
                        {{ formatTime(msg.timestampUtc) }}
                      </span>
                      @if (msg.direction === 'outbound') {
                        @switch (msg.status) {
                          @case ('sending') {
                            <i class="pi pi-clock !text-[14px] !w-3.5 !h-3.5 text-slate-400"></i>
                          }
                          @case ('sent') {
                            <i class="pi pi-check !text-[14px] !w-3.5 !h-3.5 text-slate-400"></i>
                          }
                          @case ('delivered') {
                            <i class="pi pi-check !text-[14px] !w-3.5 !h-3.5 text-slate-400"></i>
                          }
                          @case ('read') {
                            <i class="pi pi-check !text-[14px] !w-3.5 !h-3.5 text-blue-500"></i>
                          }
                          @case ('failed') {
                            <i class="pi pi-times-circle !text-[14px] !w-3.5 !h-3.5 text-red-500" [pTooltip]="msg.failureReason || ''"></i>
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
                class="w-10 h-10 bg-white dark:bg-slate-700 rounded-full shadow-lg flex items-center justify-center hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-600 transition-colors border border-[var(--app-border)] dark:border-slate-600">
                <i class="pi pi-chevron-down text-slate-500 dark:text-slate-300 !text-[20px]"></i>
                @if (newMessageCount() > 0) {
                  <span class="absolute -top-1.5 -end-1.5 bg-[var(--app-primary)] text-white rounded-full text-[9px] min-w-4 h-4 px-1 flex items-center justify-center font-bold">
                    {{ newMessageCount() }}
                  </span>
                }
              </button>
            </div>
          }

          <!-- Window expired banner -->
          @if (selectedConversation()!.lastInboundMessageAtUtc && !windowAvailable()) {
            <div class="px-3 py-2 bg-red-50 dark:bg-red-950/30 border-t border-red-200 dark:border-red-800/40 flex items-center gap-2">
              <i class="pi pi-exclamation-triangle !text-[16px] text-red-500"></i>
              <span class="text-xs text-red-600 dark:text-red-400 font-medium">{{ 'inbox.windowExpired' | translate }}</span>
            </div>
          }

          <!-- ━━ Input Area ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->
          @if (!canInteract()) {
            <!-- Locked: user must pick or be assigned -->
            <div class="px-4 py-4 bg-[var(--app-surface-muted)] dark:bg-slate-900 border-t border-[var(--app-border)] dark:border-slate-800 text-center">
              <div class="flex items-center justify-center gap-2 text-slate-500 dark:text-slate-400">
                <i class="pi pi-lock !text-[20px]"></i>
                <span class="text-sm font-medium">{{ 'inbox.pickFirst' | translate }}</span>
              </div>
              @if (!selectedConversation()!.assignedUserId) {
                <button (click)="pickConversation()"
                  class="mt-2 px-4 py-2 rounded-xl bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white text-sm font-semibold transition-colors">
                  <i class="pi pi-hand me-1 !text-[14px]"></i> {{ 'inbox.pick' | translate }}
                </button>
              }
            </div>
          } @else {
          <div class="px-3 py-2.5 bg-[var(--app-surface-muted)] dark:bg-slate-900 border-t border-[var(--app-border)] dark:border-slate-800">
            <!-- File preview -->
            @if (selectedFile()) {
              <div class="mb-2 p-2 bg-white dark:bg-slate-800 rounded-lg border border-[var(--app-border)] dark:border-slate-700 flex items-center gap-2 animate-slide-up">
                <div class="w-9 h-9 rounded bg-[var(--app-primary-soft)] dark:bg-emerald-900/30 flex items-center justify-center">
                  <i class="pi !text-[18px] text-emerald-600" [ngClass]="getMediaIcon(getFileType(selectedFile()!))"></i>
                </div>
                <div class="flex-1 min-w-0">
                  <p class="text-xs text-slate-700 dark:text-slate-300 truncate font-medium" [pTooltip]="selectedFile()!.name">{{ selectedFile()!.name }}</p>
                  <p class="text-[10px] text-slate-400">{{ formatFileSize(selectedFile()!.size) }}</p>
                </div>
                <button (click)="clearFile()" class="w-6 h-6 rounded-full hover:bg-slate-100 dark:hover:bg-slate-700 flex items-center justify-center text-slate-400 hover:text-red-500 transition-colors">
                  <i class="pi pi-times !text-[16px]"></i>
                </button>
              </div>
            }
            <div class="flex items-end gap-2">
              <!-- Attach (hidden if attachment permission is disabled) -->
              @if (permService.has('conversationsAttach')) {
              <button (click)="fileInput.click()"
                class="w-10 h-10 rounded-full hover:bg-[var(--app-surface-hover)] dark:hover:bg-slate-700 flex items-center justify-center text-slate-500 dark:text-slate-400 transition-colors shrink-0"
                                [pTooltip]="'inbox.attach' | translate">
                <i class="pi pi-paperclip !text-[22px] rotate-45"></i>
              </button>
              <input #fileInput type="file" class="hidden" accept="image/*,video/*,audio/*,.pdf,.doc,.docx,.xls,.xlsx,.txt,.csv,.zip" (change)="onFileSelected($event)" />
              }
              <!-- Text -->
              <div class="flex-1 bg-white dark:bg-slate-800 rounded-2xl px-4 py-2 border border-[var(--app-border)] dark:border-slate-700 focus-within:ring-2 focus-within:ring-emerald-500/30 transition-shadow">
                <textarea #messageInput
                  [(ngModel)]="newMessage"
                  (keydown)="onKeyDown($event)"
                  (input)="autoResize($event); autoDetectDir($event)"
                  [dir]="inputDir()"
                  [placeholder]="'inbox.typeMessage' | translate"
                  rows="1"
                  class="w-full resize-none bg-transparent border-none outline-none text-sm text-slate-900 dark:text-white placeholder-slate-400 leading-5"
                  style="max-height: 120px; overflow-y: auto; min-height: 20px;"></textarea>
              </div>
              <!-- Send -->
              <button (click)="sendMessage()" [disabled]="!canSend()"
                class="w-10 h-10 rounded-full flex items-center justify-center shrink-0 transition-all duration-200"
                [class]="canSend()
                  ? 'bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white shadow-md hover:shadow-lg active:scale-95'
                  : 'bg-slate-200 dark:bg-slate-700 text-slate-400 dark:text-slate-500'">
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
    @keyframes slide-up { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
    .animate-slide-up { animation: slide-up 0.2s ease-out; }
    :host { display: block; position: relative; }
    /* Custom scrollbar */
    ::-webkit-scrollbar { width: 5px; }
    ::-webkit-scrollbar-track { background: transparent; }
    ::-webkit-scrollbar-thumb { background: color-mix(in srgb, var(--app-text-muted) 42%, transparent); border-radius: 4px; }
    ::-webkit-scrollbar-thumb:hover { background: color-mix(in srgb, var(--app-text-soft) 52%, transparent); }
  `],
})
export class InboxComponent implements OnInit, OnDestroy, AfterViewChecked {
  private readonly api = inject(ApiService);
  private readonly notifService = inject(NotificationManagerService);
  protected readonly tokenService = inject(TokenService);
  readonly permService = inject(PermissionService);
  private readonly translate = inject(TranslateService);
  private readonly sanitizer = inject(DomSanitizer);
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
  showScrollDown = signal(false);
  newMessageCount = signal(0);
  mobileChat = signal(false);
  collapsedGroups = signal<Set<number | null>>(new Set());
  private windowTimerInterval: any = null;
  windowCountdownText = signal('');

  // Agent assignment
  agentOptions = signal<{ companyUserId: number; fullName: string; email: string; role: string }[]>([]);

  searchQuery = '';
  newMessage = '';
  private shouldScroll = false;
  private lastPollTimestamp: string | null = null;
  private isUserNearBottom = true;

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
    const reader = new FileReader();
    reader.onload = () => {
      const base64 = (reader.result as string).split(',')[1] || '';
      // Upload media via WhatsApp media endpoint
      this.api.post<any>('/whatsapp/media/upload', {
        fileName: file.name,
        contentType: file.type,
        base64Data: base64,
      }).subscribe({
        next: (uploadResult) => {
          const mediaId = uploadResult?.id || uploadResult?.data?.id || '';
          const body: SendMessageRequest = {
            messageType: this.getFileType(file),
            content: content || file.name,
            mediaUrl: mediaId || undefined,
            mediaMimeType: file.type,
            fileName: file.name,
          };
          this.clearFile();
          this.sendConversationMessage(conv.conversationId, body);
        },
        error: () => {
          // Fallback: send message with metadata only
          const body: SendMessageRequest = {
            messageType: this.getFileType(file),
            content: content || file.name,
            mediaMimeType: file.type,
            fileName: file.name,
          };
          this.clearFile();
          this.sendConversationMessage(conv.conversationId, body);
        },
      });
    };
    reader.onerror = () => this.sending.set(false);
    reader.readAsDataURL(file);
  }

  private sendConversationMessage(conversationId: number, body: SendMessageRequest): void {
    this.api.post<ConversationMessage>(`/conversations/${conversationId}/messages`, body).subscribe({
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
            c.lastMessageContent || '📩',
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
        const params: Record<string, string> = { pageSize: '50' };
        if (this.lastPollTimestamp) params['after'] = this.lastPollTimestamp;
        return this.api.get<PagedResult<ConversationMessage>>(`/conversations/${conversationId}/messages`, params).pipe(catchError(() => of(null)));
      }),
    ).subscribe(r => {
      if (!r || !r.items?.length) return;
      const newMsgs = r.items.reverse();
      const existing = new Set(this.messages().map(m => m.conversationMessageId));
      const fresh = newMsgs.filter(m => !existing.has(m.conversationMessageId));

      if (fresh.length > 0) {
        this.messages.update(m => [...m, ...fresh]);
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
            inbound[inbound.length - 1].content || '📩',
          );
        } else if (inbound.length > 0) {
          this.notifService.playSound();
        }
      }

      // Also update status of existing messages (for delivered/read updates)
      const currentMsgs = this.messages();
      let statusUpdated = false;
      for (const newMsg of newMsgs) {
        const existingMsg = currentMsgs.find(m => m.conversationMessageId === newMsg.conversationMessageId);
        if (existingMsg && existingMsg.status !== newMsg.status) {
          existingMsg.status = newMsg.status;
          statusUpdated = true;
        }
      }
      if (statusUpdated) this.messages.set([...currentMsgs]);
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

  // ─── File handling ───
  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.selectedFile.set(input.files[0]);
      input.value = '';
    }
  }

  clearFile(): void { this.selectedFile.set(null); }

  getFileType(file: File): string {
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
  getInitials(name: string): string {
    if (!name) return '?';
    // For phone numbers, show last 2 digits
    if (/^\+?\d/.test(name)) return name.slice(-2);
    return name.split(' ').filter(Boolean).slice(0, 2).map(w => w[0]).join('').toUpperCase();
  }

  getAvatarClasses(conv: Conversation): string {
    const colors = [
      'bg-gradient-to-br from-emerald-400 to-teal-500 text-white',
      'bg-gradient-to-br from-blue-400 to-indigo-500 text-white',
      'bg-gradient-to-br from-violet-400 to-purple-500 text-white',
      'bg-gradient-to-br from-amber-400 to-orange-500 text-white',
      'bg-gradient-to-br from-rose-400 to-pink-500 text-white',
      'bg-gradient-to-br from-cyan-400 to-sky-500 text-white',
    ];
    return colors[conv.conversationId % colors.length];
  }

  getMediaIcon(type: string): string {
    switch (type) {
      case 'image': return 'pi-image';
      case 'video': return 'pi-video';
      case 'audio': return 'pi-volume-up';
      case 'document': return 'pi-file';
      case 'sticker': return 'pi-face-smile';
      default: return 'pi-paperclip';
    }
  }

  getMediaLabel(type: string): string {
    const key = 'inbox.media.' + type;
    const translated = this.translate.instant(key);
    return translated !== key ? translated : type.charAt(0).toUpperCase() + type.slice(1);
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
    el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'instant' });
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

