import { Component, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, of, catchError } from 'rxjs';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService, TokenService } from '../../../core/services';
import { CompanyService } from '../../../core/services/company.service';
import { ActivityFeedComponent } from '../../../shared/components/activity-feed/activity-feed.component';
import { WebhookStatusComponent } from '../../../shared/components/webhook-status/webhook-status.component';
import {
  CompanySubscription, Message, WhatsAppAccount, WhatsAppPhoneNumber,
  PagedResult, Contact, Conversation, Campaign, AutomationRule,
  WhatsAppConnectionStatus,
} from '../../../core/models';

interface StatCard {
  label: string;
  value: string;
  icon: string;
  change?: string;
  color: string;
}

@Component({
  selector: 'app-dashboard-home',
  standalone: true,
  imports: [
    NgClass, ProgressSpinnerModule, ToastModule, FormsModule,
    RouterLink, TranslateModule, ActivityFeedComponent, WebhookStatusComponent,
  ],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="space-y-6 lg:space-y-8 min-w-0 app-wrap-safe">
      <!-- ═══ Header ═══ -->
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'dashboard.title' | translate }}</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'dashboard.subtitle' | translate }}</p>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-20">
          <p-progressSpinner [style]="{'width':'40px','height':'40px'}" strokeWidth="4" />
        </div>
      } @else {
        <!-- ═══ TOP: Stats Cards ═══ -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 lg:gap-5">
          @for (card of statCards(); track card.label) {
            <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 hover:shadow-lg hover:shadow-emerald-500/5 transition-all duration-200 animate-fadeInUp">
              <div class="flex items-center justify-between mb-4">
                <div class="w-10 h-10 rounded-xl flex items-center justify-center" [class]="card.color">
                  <i class="pi !text-[20px]" [ngClass]="card.icon"></i>
                </div>
                @if (card.change) {
                  <span class="text-xs font-medium px-2 py-1 rounded-full bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400">
                    {{ card.change }}
                  </span>
                }
              </div>
              <div class="text-2xl font-bold text-slate-900 dark:text-white mb-1">{{ card.value }}</div>
              <div class="text-xs text-slate-500 dark:text-slate-400 uppercase tracking-wider">{{ card.label | translate }}</div>
            </div>
          }
        </div>

        <!-- ═══ MIDDLE: WhatsApp Connection + Subscription + Webhook Status ═══ -->
        <div class="grid xl:grid-cols-3 gap-5 lg:gap-6">
          <!-- WhatsApp Connection Status -->
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'dashboard.whatsappConnection' | translate }}</h3>
              <a routerLink="/dashboard/settings" class="text-sm text-emerald-600 dark:text-emerald-400 font-medium hover:underline no-underline">
                {{ 'dashboard.goToSettings' | translate }}
              </a>
            </div>
            @if (waConnection()) {
              @if (waConnection()!.isConnected) {
                <div class="flex items-center gap-4 mb-4">
                  <div class="w-12 h-12 rounded-xl bg-emerald-100 dark:bg-emerald-900/30 flex items-center justify-center">
                    <i class="pi pi-check-circle !text-[24px] text-emerald-600"></i>
                  </div>
                  <div>
                    <div class="text-sm font-semibold text-emerald-700 dark:text-emerald-400">{{ 'dashboard.whatsappConnected' | translate }}</div>
                    <div class="text-xs text-slate-500 dark:text-slate-400">{{ waConnection()!.businessAccountName || waConnection()!.businessAccountId }}</div>
                  </div>
                </div>
                <div class="grid grid-cols-2 gap-3">
                  <div class="bg-slate-50 dark:bg-slate-700/30 rounded-xl p-3 text-center">
                    <div class="text-lg font-bold text-slate-900 dark:text-white">{{ waConnection()!.phoneNumberCount }}</div>
                    <div class="text-xs text-slate-500">{{ 'settings.phoneNumbers' | translate }}</div>
                  </div>
                  <div class="bg-slate-50 dark:bg-slate-700/30 rounded-xl p-3 text-center">
                    <div class="flex items-center justify-center gap-1">
                      <span class="w-2 h-2 rounded-full" [class]="waConnection()!.tokenValid ? 'bg-emerald-500' : 'bg-red-500'"></span>
                      <span class="text-lg font-bold" [class]="waConnection()!.tokenValid ? 'text-emerald-600' : 'text-red-600'">
                        {{ waConnection()!.tokenValid ? ('settings.valid' | translate) : ('settings.invalid' | translate) }}
                      </span>
                    </div>
                    <div class="text-xs text-slate-500">{{ 'settings.tokenStatus' | translate }}</div>
                  </div>
                </div>
                @if (!waConnection()!.tokenValid) {
                  <div class="mt-3 p-2.5 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/30 flex items-center gap-2">
                    <i class="pi pi-exclamation-triangle !text-red-500 !text-[16px]"></i>
                    <span class="text-xs text-red-600 dark:text-red-400">{{ 'dashboard.whatsappTokenExpired' | translate }}</span>
                  </div>
                }
              } @else {
                <div class="text-center py-6">
                  <div class="w-12 h-12 mx-auto rounded-xl bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center mb-3">
                    <i class="pi pi-link !text-[24px] text-amber-600"></i>
                  </div>
                  <p class="text-sm text-slate-500 dark:text-slate-400 mb-3">{{ 'dashboard.whatsappNotConnected' | translate }}</p>
                  <a routerLink="/dashboard/settings" class="inline-flex items-center gap-1 text-sm text-emerald-600 dark:text-emerald-400 font-medium hover:underline no-underline">
                    {{ 'dashboard.whatsappSetup' | translate }}
                    <i class="pi pi-arrow-right !text-[16px]"></i>
                  </a>
                </div>
              }
            } @else {
              <div class="text-center py-6">
                <i class="pi pi-link !text-[40px] text-slate-300 mb-2"></i>
                <p class="text-sm text-slate-400">{{ 'dashboard.whatsappNotConnected' | translate }}</p>
              </div>
            }
          </div>

          <!-- Subscription Info -->
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
            <h3 class="text-lg font-semibold text-slate-900 dark:text-white mb-4">{{ 'dashboard.subscription' | translate }}</h3>
            @if (subscription()) {
              <div class="space-y-3">
                <div class="flex justify-between items-center">
                  <span class="text-sm text-slate-500">{{ 'dashboard.status' | translate }}</span>
                  <span class="px-3 py-1 rounded-full text-xs font-semibold"
                    [class]="subscription()!.status === 'TRIAL'
                      ? 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-400'
                      : 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400'">
                    {{ subscription()!.status }}
                  </span>
                </div>
                @if (subscription()!.trialEndDate) {
                  <div class="flex justify-between items-center">
                    <span class="text-sm text-slate-500">{{ 'dashboard.trialEnds' | translate }}</span>
                    <span class="text-sm font-medium text-slate-900 dark:text-white">{{ subscription()!.trialEndDate?.slice(0, 10) }}</span>
                  </div>
                }
                <a routerLink="/dashboard/billing" class="inline-flex items-center gap-1 text-sm text-emerald-600 dark:text-emerald-400 font-medium hover:underline no-underline mt-2">
                  {{ 'dashboard.manageSubscription' | translate }}
                  <i class="pi pi-arrow-right !text-[16px]"></i>
                </a>
              </div>
            } @else {
              <div class="text-center py-6 text-slate-400">
                <i class="pi pi-credit-card !text-[40px] mb-2"></i>
                <p class="text-sm">{{ 'common.noData' | translate }}</p>
              </div>
            }
          </div>

          <!-- Webhook Status Widget — Phase 14 -->
          <app-webhook-status />
        </div>

        <!-- ═══ BOTTOM: Test Message Widget + Activity Feed (side by side) ═══ -->
        <div class="grid xl:grid-cols-2 gap-5 lg:gap-6">
          <!-- Quick Test Message Widget -->
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'dashboard.quickTestMessage' | translate }}</h3>
              <a routerLink="/dashboard/send-message" class="text-sm text-emerald-600 dark:text-emerald-400 font-medium hover:underline no-underline">
                {{ 'dashboard.fullSender' | translate }}
              </a>
            </div>
            <div class="space-y-4">
              <div>
                <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">{{ 'sendMessage.recipient' | translate }}</label>
                <input type="tel" [(ngModel)]="testRecipient" [placeholder]="'dashboard.recipientPlaceholder' | translate" dir="ltr"
                  class="w-full px-3 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none transition-shadow" />
              </div>
              <div>
                <label class="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">{{ 'sendMessage.messageContent' | translate }}</label>
                <textarea [(ngModel)]="testMessage" rows="3" [placeholder]="'dashboard.messagePlaceholder' | translate"
                  class="w-full px-3 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none resize-none transition-shadow">
                </textarea>
              </div>

              @if (testSuccess()) {
                <div class="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800/30 flex items-center gap-2 animate-fadeInUp">
                  <i class="pi pi-check-circle !text-emerald-500 !text-[18px]"></i>
                  <span class="text-sm text-emerald-700 dark:text-emerald-300">{{ 'dashboard.testMessageSent' | translate }}</span>
                </div>
              }

              @if (testError()) {
                <div class="p-3 rounded-xl bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/30 flex items-center gap-2 animate-fadeInUp">
                  <i class="pi pi-times-circle !text-red-500 !text-[18px]"></i>
                  <span class="text-sm text-red-700 dark:text-red-300">{{ testError() }}</span>
                </div>
              }

              <button (click)="sendTestMessage()" [disabled]="testSending()"
                class="w-full py-3 px-4 rounded-xl bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white text-sm font-semibold transition-all duration-200 flex items-center justify-center gap-2 disabled:opacity-50 border-none cursor-pointer hover:shadow-lg hover:shadow-emerald-500/20">
                @if (testSending()) {
                  <p-progressSpinner [style]="{'width':'18px','height':'18px'}" strokeWidth="4" />
                } @else {
                  <i class="pi pi-send !text-[18px]"></i>
                }
                {{ 'dashboard.sendTestMessage' | translate }}
              </button>
            </div>
          </div>

          <!-- Activity Feed — Phase 10 -->
          <app-activity-feed />
        </div>

        <!-- ═══ Recent Messages ═══ -->
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
          <div class="flex flex-wrap items-center justify-between gap-3 mb-4">
            <h3 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'dashboard.recentMessages' | translate }}</h3>
            <a routerLink="/dashboard/messages" class="text-sm text-emerald-600 dark:text-emerald-400 font-medium hover:underline no-underline">
              {{ 'common.viewAll' | translate }}
            </a>
          </div>
          @if (recentMessages().length === 0) {
            <div class="text-center py-8 text-slate-400">
              <i class="pi pi-comments !text-[40px] mb-2"></i>
              <p class="text-sm">{{ 'dashboard.noMessages' | translate }}</p>
            </div>
          } @else {
            <div class="space-y-2">
              @for (msg of recentMessages().slice(0, 5); track msg.messageId) {
                <div class="flex items-center justify-between gap-3 py-2.5 px-3 rounded-xl hover:bg-slate-50 dark:hover:bg-slate-700/20 transition-colors">
                  <div class="flex items-center gap-3 min-w-0">
                    <div class="w-8 h-8 rounded-lg flex items-center justify-center"
                      [class]="msg.status === 'SENT' ? 'bg-emerald-100 dark:bg-emerald-900/40' : msg.status === 'FAILED' ? 'bg-red-100 dark:bg-red-900/40' : 'bg-amber-100 dark:bg-amber-900/40'">
                      <i class="pi !text-[16px]"
                        [class]="msg.status === 'SENT' ? '!text-emerald-600' : msg.status === 'FAILED' ? '!text-red-600' : '!text-amber-600'"
                        [ngClass]="msg.status === 'SENT' ? 'pi-check-circle' : msg.status === 'FAILED' ? 'pi-times-circle' : 'pi-clock'">
                      </i>
                    </div>
                    <div class="min-w-0">
                      <div class="text-sm font-medium text-slate-900 dark:text-white truncate">{{ msg.toNumber }}</div>
                      <div class="text-xs text-slate-500">{{ msg.messageType }}</div>
                    </div>
                  </div>
                  <span class="text-xs px-2.5 py-1 rounded-full font-medium"
                    [class]="msg.status === 'SENT' ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400'
                      : msg.status === 'FAILED' ? 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-400'
                      : 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400'">
                    {{ msg.status }}
                  </span>
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    @keyframes fadeInUp {
      from { opacity: 0; transform: translateY(10px); }
      to   { opacity: 1; transform: translateY(0); }
    }
    .animate-fadeInUp {
      animation: fadeInUp 0.35s ease-out both;
    }
  `],
})
export class DashboardHomeComponent implements OnInit {
  private api = inject(ApiService);
  private tokenService = inject(TokenService);
  private companyService = inject(CompanyService);
  private messageService = inject(MessageService);

  loading = signal(true);
  statCards = signal<StatCard[]>([]);
  subscription = signal<CompanySubscription | null>(null);
  recentMessages = signal<Message[]>([]);
  waConnection = signal<WhatsAppConnectionStatus | null>(null);

  // Test message widget state
  testRecipient = '';
  testMessage = 'Hello from BotGlobal Services platform';
  testSending = signal(false);
  testSuccess = signal(false);
  testError = signal<string | null>(null);

  ngOnInit(): void {
    // Use CompanyService for shared data (connection status, subscription, phone numbers)
    // Use catchError on each individual observable to prevent forkJoin from failing entirely
    forkJoin({
      accounts: this.api.get<WhatsAppAccount[]>('/whatsapp-accounts').pipe(catchError(() => of([]))),
      phones: this.api.get<WhatsAppPhoneNumber[]>('/phone-numbers').pipe(catchError(() => of([]))),
      subscriptions: this.api.get<CompanySubscription[]>('/subscriptions').pipe(catchError(() => of([]))),
      contacts: this.api.get<PagedResult<Contact>>('/contacts', { pageSize: '1' }).pipe(catchError(() => of({ totalCount: 0 } as PagedResult<Contact>))),
      conversations: this.api.get<PagedResult<Conversation>>('/conversations', { pageSize: '1' }).pipe(catchError(() => of({ totalCount: 0 } as PagedResult<Conversation>))),
      campaigns: this.api.get<PagedResult<Campaign>>('/campaigns', { pageSize: '1' }).pipe(catchError(() => of({ totalCount: 0 } as PagedResult<Campaign>))),
      automationRules: this.api.get<AutomationRule[]>('/automation').pipe(catchError(() => of([]))),
      waConnection: this.companyService.loadConnectionStatus(true),
      messages: this.api.get<PagedResult<Message>>('/messages', { pageSize: '5' }).pipe(catchError(() => of({ items: [] } as unknown as PagedResult<Message>))),
    }).subscribe({
      next: ({ accounts, phones, subscriptions, contacts, conversations, campaigns, automationRules, waConnection, messages }) => {
        const activeSub = (subscriptions as CompanySubscription[])?.find((s) => s.isActive) ?? null;
        this.subscription.set(activeSub);
        this.companyService.subscription.set(activeSub);

        if (waConnection) {
          this.waConnection.set(waConnection as WhatsAppConnectionStatus);
        }

        // Set recent messages
        const msgResult = messages as PagedResult<Message>;
        if (msgResult?.items) {
          this.recentMessages.set(msgResult.items);
        }

        const phonesArr = (phones as WhatsAppPhoneNumber[]) ?? [];
        const accountsArr = (accounts as WhatsAppAccount[]) ?? [];

        this.statCards.set([
          {
            label: 'dashboard.conversations',
            value: String((conversations as PagedResult<Conversation>)?.totalCount ?? 0),
            icon: 'pi-comments',
            color: 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-600 dark:text-emerald-400',
          },
          {
            label: 'dashboard.contacts',
            value: String((contacts as PagedResult<Contact>)?.totalCount ?? 0),
            icon: 'pi-users',
            color: 'bg-blue-100 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400',
          },
          {
            label: 'dashboard.campaigns',
            value: String((campaigns as PagedResult<Campaign>)?.totalCount ?? 0),
            icon: 'pi-megaphone',
            color: 'bg-purple-100 dark:bg-purple-900/40 text-purple-600 dark:text-purple-400',
          },
          {
            label: 'dashboard.automationRules',
            value: String((automationRules as AutomationRule[])?.length ?? 0),
            icon: 'pi-bolt',
            color: 'bg-amber-100 dark:bg-amber-900/40 text-amber-600 dark:text-amber-400',
          },
          {
            label: 'dashboard.whatsappAccounts',
            value: String(accountsArr.length),
            icon: 'pi-server',
            color: 'bg-indigo-100 dark:bg-indigo-900/40 text-indigo-600 dark:text-indigo-400',
          },
          {
            label: 'dashboard.phoneNumbers',
            value: String(phonesArr.length),
            icon: 'pi-phone',
            color: 'bg-teal-100 dark:bg-teal-900/40 text-teal-600 dark:text-teal-400',
          },
          {
            label: 'dashboard.subscription',
            value: activeSub?.status ?? 'None',
            icon: 'pi-credit-card',
            color: 'bg-rose-100 dark:bg-rose-900/40 text-rose-600 dark:text-rose-400',
          },
          {
            label: 'dashboard.activeNumbers',
            value: String(phonesArr.filter((p) => p.isActive).length),
            icon: 'pi-check-circle',
            color: 'bg-lime-100 dark:bg-lime-900/40 text-lime-600 dark:text-lime-400',
          },
        ]);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  sendTestMessage(): void {
    if (!this.testRecipient.trim() || !this.testMessage.trim()) {
      this.testError.set('Please fill in both recipient and message.');
      return;
    }

    this.testSending.set(true);
    this.testSuccess.set(false);
    this.testError.set(null);

    const body = {
      to: this.testRecipient.replace(/\s+/g, ''),
      body: this.testMessage,
      previewUrl: false,
    };

    this.api.postRaw<unknown>('/whatsapp/messages/text', body).subscribe({
      next: (res) => {
        this.testSending.set(false);
        if (res.success) {
          this.testSuccess.set(true);
          this.messageService.add({ severity: 'success', summary: '✅ Test message sent!', life: 3000 });
          setTimeout(() => this.testSuccess.set(false), 5000);
        } else {
          this.testError.set(res.message || 'Failed to send test message.');
        }
      },
      error: (err) => {
        this.testSending.set(false);
        this.testError.set(err.error?.message || 'Failed to send test message.');
      },
    });
  }
}

