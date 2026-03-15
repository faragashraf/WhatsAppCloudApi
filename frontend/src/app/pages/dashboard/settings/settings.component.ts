import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { FormsModule } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { InputOtpModule } from 'primeng/inputotp';
import { TokenService } from '../../../core/services/token.service';
import { ThemeService } from '../../../core/services/theme.service';
import { ApiService } from '../../../core/services/api.service';
import { CompanyService } from '../../../core/services/company.service';
import { NotificationManagerService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { LanguageService } from '../../../core/services/language.service';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import * as QRCode from 'qrcode';
import {
  ConnectMetaRequest,
  ConnectMetaResponse,
  CompanyRoutingSettings,
  RotateVerifyTokenResponse,
  SubscriptionUsageSnapshot,
  TwoFactorSetup,
  TwoFactorStatus,
  UpdateCompanyRoutingSettingsRequest,
  WhatsAppConnectionStatus,
} from '../../../core/models';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [ButtonModule, ProgressSpinnerModule, ToastModule, FormsModule, DatePipe, DecimalPipe, TranslateModule, ToggleSwitchModule, InputOtpModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="space-y-8 max-w-6xl mx-auto min-w-0 app-wrap-safe">
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'settings.title' | translate }}</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'settings.subtitle' | translate }}</p>
      </div>
      <div class="grid grid-cols-1 xl:grid-cols-2 gap-6 items-start">

      <!-- Subscription Usage -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <div class="flex items-start justify-between gap-3 mb-5">
          <div>
            <h2 class="text-lg font-semibold text-slate-900 dark:text-white flex items-center gap-2">
              <i class="pi pi-chart-bar text-emerald-500"></i> {{ 'settings.subscription.title' | translate }}
            </h2>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'settings.subscription.subtitle' | translate }}</p>
          </div>
          <button
            pButton
            [outlined]="true"
            size="small"
            (click)="refreshSubscriptionUsage()"
            [disabled]="subscriptionUsageLoading()"
            class="!rounded-lg !text-xs">
            @if (subscriptionUsageLoading()) {
              <p-progressSpinner [style]="{'width':'13px','height':'13px'}" strokeWidth="4" class="!inline-block me-1" />
            }
            {{ 'settings.refresh' | translate }}
          </button>
        </div>

        @if (subscriptionUsageLoading()) {
          <div class="flex items-center justify-center py-6">
            <p-progressSpinner [style]="{'width':'28px','height':'28px'}" strokeWidth="4" />
          </div>
        } @else if (subscriptionUsage(); as usage) {
          <div class="space-y-4">
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 text-sm">
              <div class="rounded-xl border border-slate-200 dark:border-slate-700/60 bg-slate-50 dark:bg-slate-900/30 px-3 py-2">
                <div class="text-xs text-slate-500 dark:text-slate-400 mb-1">{{ 'settings.subscription.plan' | translate }}</div>
                <div class="font-semibold text-slate-900 dark:text-white">{{ usage.planName || usage.planCode || ('settings.subscription.planUnknown' | translate) }}</div>
              </div>
              <div class="rounded-xl border border-slate-200 dark:border-slate-700/60 bg-slate-50 dark:bg-slate-900/30 px-3 py-2">
                <div class="text-xs text-slate-500 dark:text-slate-400 mb-1">{{ 'settings.subscription.status' | translate }}</div>
                <span
                  class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold"
                  [class]="usage.subscriptionStatus === 'ACTIVE'
                    ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300'
                    : usage.subscriptionStatus === 'TRIAL'
                    ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300'
                    : 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300'">
                  {{
                    usage.subscriptionStatus === 'ACTIVE'
                      ? ('settings.subscription.statusActive' | translate)
                      : usage.subscriptionStatus === 'TRIAL'
                      ? ('settings.subscription.statusTrial' | translate)
                      : ('settings.subscription.statusExpired' | translate)
                  }}
                </span>
              </div>
              <div class="rounded-xl border border-slate-200 dark:border-slate-700/60 bg-slate-50 dark:bg-slate-900/30 px-3 py-2">
                <div class="text-xs text-slate-500 dark:text-slate-400 mb-1">{{ 'settings.subscription.expiresAt' | translate }}</div>
                <div class="font-semibold text-slate-900 dark:text-white">
                  @if (usage.expiresAtUtc) {
                    <bdi dir="ltr">{{ usage.expiresAtUtc | date:'mediumDate' }}</bdi>
                  } @else {
                    {{ 'settings.subscription.notSet' | translate }}
                  }
                </div>
              </div>
            </div>

            @if (!usage.isSubscriptionActive) {
              <div class="rounded-xl border border-red-200 dark:border-red-800/50 bg-red-50 dark:bg-red-900/20 px-3 py-2 text-sm text-red-700 dark:text-red-300">
                {{ 'settings.subscription.inactiveHint' | translate }}
              </div>
            }

            <div class="space-y-3">
              <div class="rounded-xl border border-slate-200 dark:border-slate-700/60 p-4">
                <div class="flex items-center justify-between gap-3 text-sm mb-2">
                  <div class="font-medium text-slate-900 dark:text-white">{{ 'settings.subscription.messagesLimit' | translate }}</div>
                  <div class="text-xs text-slate-600 dark:text-slate-400">
                    <bdi dir="ltr">{{ usage.messagesUsedThisMonth | number }}</bdi>
                    /
                    @if (usage.maxMessagesPerMonth > 0) {
                      <bdi dir="ltr">{{ usage.maxMessagesPerMonth | number }}</bdi>
                    } @else {
                      {{ 'settings.subscription.unlimited' | translate }}
                    }
                  </div>
                </div>
                @if (usage.maxMessagesPerMonth > 0) {
                  <div class="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
                    <div class="h-full bg-emerald-500 transition-all duration-300" [style.width.%]="usagePercent(usage.messagesUsedThisMonth, usage.maxMessagesPerMonth)"></div>
                  </div>
                }
                <div class="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400 mt-2">
                  <span>{{ 'settings.subscription.used' | translate }}: <bdi dir="ltr">{{ usage.messagesUsedThisMonth | number }}</bdi></span>
                  <span>
                    {{ 'settings.subscription.remaining' | translate }}:
                    @if (usage.maxMessagesPerMonth > 0) {
                      <bdi dir="ltr">{{ usage.remainingMessagesThisMonth | number }}</bdi>
                    } @else {
                      {{ 'settings.subscription.unlimited' | translate }}
                    }
                  </span>
                </div>
              </div>

              <div class="rounded-xl border border-slate-200 dark:border-slate-700/60 p-4">
                <div class="flex items-center justify-between gap-3 text-sm mb-2">
                  <div class="font-medium text-slate-900 dark:text-white">{{ 'settings.subscription.whatsappAccountsLimit' | translate }}</div>
                  <div class="text-xs text-slate-600 dark:text-slate-400">
                    <bdi dir="ltr">{{ usage.activeWhatsAppAccounts | number }}</bdi>
                    /
                    @if (usage.maxWhatsAppAccounts > 0) {
                      <bdi dir="ltr">{{ usage.maxWhatsAppAccounts | number }}</bdi>
                    } @else {
                      {{ 'settings.subscription.unlimited' | translate }}
                    }
                  </div>
                </div>
                @if (usage.maxWhatsAppAccounts > 0) {
                  <div class="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
                    <div class="h-full bg-emerald-500 transition-all duration-300" [style.width.%]="usagePercent(usage.activeWhatsAppAccounts, usage.maxWhatsAppAccounts)"></div>
                  </div>
                }
                <div class="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400 mt-2">
                  <span>{{ 'settings.subscription.used' | translate }}: <bdi dir="ltr">{{ usage.activeWhatsAppAccounts | number }}</bdi></span>
                  <span>
                    {{ 'settings.subscription.remaining' | translate }}:
                    @if (usage.maxWhatsAppAccounts > 0) {
                      <bdi dir="ltr">{{ usage.remainingWhatsAppAccounts | number }}</bdi>
                    } @else {
                      {{ 'settings.subscription.unlimited' | translate }}
                    }
                  </span>
                </div>
              </div>

              <div class="rounded-xl border border-slate-200 dark:border-slate-700/60 p-4">
                <div class="flex items-center justify-between gap-3 text-sm mb-2">
                  <div class="font-medium text-slate-900 dark:text-white">{{ 'settings.subscription.phoneNumbersLimit' | translate }}</div>
                  <div class="text-xs text-slate-600 dark:text-slate-400">
                    <bdi dir="ltr">{{ usage.activePhoneNumbers | number }}</bdi>
                    /
                    @if (usage.maxPhoneNumbers > 0) {
                      <bdi dir="ltr">{{ usage.maxPhoneNumbers | number }}</bdi>
                    } @else {
                      {{ 'settings.subscription.unlimited' | translate }}
                    }
                  </div>
                </div>
                @if (usage.maxPhoneNumbers > 0) {
                  <div class="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
                    <div class="h-full bg-emerald-500 transition-all duration-300" [style.width.%]="usagePercent(usage.activePhoneNumbers, usage.maxPhoneNumbers)"></div>
                  </div>
                }
                <div class="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400 mt-2">
                  <span>{{ 'settings.subscription.used' | translate }}: <bdi dir="ltr">{{ usage.activePhoneNumbers | number }}</bdi></span>
                  <span>
                    {{ 'settings.subscription.remaining' | translate }}:
                    @if (usage.maxPhoneNumbers > 0) {
                      <bdi dir="ltr">{{ usage.remainingPhoneNumbers | number }}</bdi>
                    } @else {
                      {{ 'settings.subscription.unlimited' | translate }}
                    }
                  </span>
                </div>
              </div>
            </div>
          </div>
        } @else {
          <div class="rounded-xl border border-slate-200 dark:border-slate-700/60 bg-slate-50 dark:bg-slate-900/30 px-3 py-2 text-sm text-slate-600 dark:text-slate-300">
            {{ 'settings.subscription.unavailable' | translate }}
          </div>
        }
      </div>

      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <h2 class="text-lg font-semibold text-slate-900 dark:text-white mb-2 flex items-center gap-2">
          <i class="pi pi-comments text-[var(--app-primary)]"></i>
          {{ 'settings.connectWhatsApp' | translate }}
        </h2>
        <p class="text-sm text-slate-500 dark:text-slate-400 mb-6">{{ 'settings.connectWhatsAppDesc' | translate }}</p>

        <!-- Connection Status Banner -->
        @if (connectionLoading()) {
          <div class="flex items-center justify-center py-6 gap-3">
            <p-progressSpinner [style]="{'width':'24px','height':'24px'}" strokeWidth="4" />
            <span class="text-sm text-slate-500">{{ 'settings.checkingConnection' | translate }}</span>
          </div>
        } @else if (connectionStatus() && connectionStatus()!.isConnected) {
          <div class="mb-6 p-4 rounded-xl bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800/40 overflow-hidden">
            <div class="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
              <div class="flex items-center gap-3 min-w-0">
                <div class="w-10 h-10 rounded-full bg-emerald-500 flex items-center justify-center">
                  <i class="pi pi-check-circle !text-white !text-[20px]"></i>
                </div>
                <div class="min-w-0">
                  <h3 class="font-semibold text-emerald-800 dark:text-emerald-300">{{ 'settings.connected' | translate }}</h3>
                  <p class="text-sm text-emerald-600 dark:text-emerald-400 break-all">
                    <bdi dir="auto">{{ connectionStatus()!.businessAccountName || connectionStatus()!.businessAccountId }}</bdi>
                  </p>
                </div>
              </div>
              <button pButton [outlined]="true" (click)="refreshConnection()" [disabled]="connectionLoading()"
                class="!rounded-xl !border-emerald-300 !text-emerald-700 dark:!text-emerald-300 !text-sm self-start sm:self-auto shrink-0">
                <i class="pi pi-refresh !text-[16px]"></i>
                {{ 'settings.refresh' | translate }}
              </button>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mt-4 pt-4 border-t border-emerald-200 dark:border-emerald-700/30">
              <div class="min-w-0">
                <div class="text-xs text-emerald-600 dark:text-emerald-400 mb-1">{{ 'settings.businessAccount' | translate }}</div>
                <div class="text-sm font-semibold text-emerald-800 dark:text-emerald-300 font-mono break-all leading-tight">
                  <bdi dir="ltr">{{ connectionStatus()!.businessAccountId }}</bdi>
                </div>
              </div>
              <div class="min-w-0">
                <div class="text-xs text-emerald-600 dark:text-emerald-400 mb-1">{{ 'settings.phoneNumbers' | translate }}</div>
                <div class="text-sm font-semibold text-emerald-800 dark:text-emerald-300">
                  <bdi dir="ltr">{{ connectionStatus()!.phoneNumberCount }}</bdi>
                </div>
              </div>
              <div class="min-w-0">
                <div class="text-xs text-emerald-600 dark:text-emerald-400 mb-1">{{ 'settings.lastSync' | translate }}</div>
                <div class="text-sm font-semibold text-emerald-800 dark:text-emerald-300 break-words">
                  <bdi dir="ltr">{{ connectionStatus()!.lastSyncUtc | date:'short' }}</bdi>
                </div>
              </div>
              <div class="min-w-0">
                <div class="text-xs text-emerald-600 dark:text-emerald-400 mb-1">{{ 'settings.tokenStatus' | translate }}</div>
                <div class="flex items-center gap-1">
                  <span class="w-2 h-2 rounded-full" [class]="connectionStatus()!.tokenValid ? 'bg-emerald-500' : 'bg-red-500'"></span>
                  <span class="text-sm font-semibold" [class]="connectionStatus()!.tokenValid ? 'text-emerald-800 dark:text-emerald-300' : 'text-red-600 dark:text-red-400'">
                    {{ connectionStatus()!.tokenValid ? ('settings.valid' | translate) : ('settings.invalid' | translate) }}
                  </span>
                </div>
              </div>
            </div>

            @if (!connectionStatus()!.tokenValid) {
              <div class="mt-4 p-3 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 flex items-center gap-2">
                <i class="pi pi-exclamation-triangle !text-red-500 !text-[18px]"></i>
                <span class="text-sm text-red-600 dark:text-red-400">{{ 'settings.tokenExpiredWarning' | translate }}</span>
              </div>
            }

            @if (connectionStatus()!.webhookUrl) {
              <div class="mt-4 pt-4 border-t border-emerald-200 dark:border-emerald-700/30">
                <div class="text-xs text-emerald-600 dark:text-emerald-400 mb-1">{{ 'settings.webhookUrl' | translate }}</div>
                <code dir="ltr" class="text-sm text-emerald-800 dark:text-emerald-300 bg-emerald-100 dark:bg-emerald-900/30 px-3 py-1.5 rounded-lg block w-full font-mono break-all whitespace-pre-wrap text-left">
                  {{ webhookDisplayUrl() }}
                </code>
              </div>
            }

            @if (connectionStatus()!.verifyToken) {
              <div class="mt-4 pt-4 border-t border-emerald-200 dark:border-emerald-700/30">
                <div class="text-xs text-emerald-600 dark:text-emerald-400 mb-1">{{ 'settings.verifyToken' | translate }}</div>
                <div class="flex flex-col md:flex-row md:items-center gap-2">
                  <code dir="ltr" class="text-sm text-emerald-800 dark:text-emerald-300 bg-emerald-100 dark:bg-emerald-900/30 px-3 py-1.5 rounded-lg inline-block w-full md:w-auto font-mono break-all text-left">
                    {{ displayedVerifyToken() }}
                  </code>
                  <div class="flex items-center gap-2">
                    <button pButton [outlined]="true" size="small" (click)="showVerifyToken.set(!showVerifyToken())" class="!px-2">
                      <i class="pi" [class.pi-eye]="!showVerifyToken()" [class.pi-eye-slash]="showVerifyToken()"></i>
                    </button>
                    <button pButton [outlined]="true" size="small" (click)="copyVerifyToken()" class="!rounded-lg !text-xs">
                      {{ 'settings.copyVerifyToken' | translate }}
                    </button>
                    <button pButton [outlined]="true" size="small" (click)="rotateVerifyToken()" [disabled]="rotatingVerifyToken()" class="!rounded-lg !text-xs">
                      @if (rotatingVerifyToken()) {
                        <p-progressSpinner [style]="{'width':'14px','height':'14px'}" strokeWidth="4" class="!inline-block mr-1" />
                      }
                      {{ 'settings.rotateVerifyToken' | translate }}
                    </button>
                  </div>
                </div>
                <p class="text-xs text-emerald-700 dark:text-emerald-400 mt-2">
                  {{ 'settings.verifyTokenHint' | translate }}
                </p>
              </div>
            }

            @if (connectionStatus()!.phoneNumbers.length > 0) {
              <div class="mt-4 pt-4 border-t border-emerald-200 dark:border-emerald-700/30">
                <div class="text-xs text-emerald-600 dark:text-emerald-400 mb-2">{{ 'settings.connectedNumbers' | translate }}</div>
                <div class="space-y-2">
                  @for (phone of connectionStatus()!.phoneNumbers; track phone.phoneNumberId) {
                    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 bg-white dark:bg-slate-800/50 rounded-lg px-3 py-2 border border-emerald-100 dark:border-emerald-800/30">
                      <div class="flex items-center gap-3 min-w-0">
                        <i class="pi pi-phone !text-[16px] text-emerald-500"></i>
                        <div class="min-w-0">
                          <div class="flex items-center flex-wrap gap-x-2 gap-y-1 min-w-0">
                            <bdi dir="ltr" class="text-sm font-medium text-slate-900 dark:text-white">{{ phone.displayPhoneNumber }}</bdi>
                            @if (phone.verifiedName) {
                              <span class="text-xs text-slate-500 dark:text-slate-400 truncate max-w-full">
                                <bdi dir="auto">{{ phone.verifiedName }}</bdi>
                              </span>
                            }
                          </div>
                        </div>
                      </div>
                      @if (phone.qualityRating) {
                        <span class="text-xs px-2 py-0.5 rounded-full self-start sm:self-auto"
                          [class]="phone.qualityRating === 'GREEN' ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400'
                                  : phone.qualityRating === 'YELLOW' ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400'
                                  : 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-400'">
                          {{ phone.qualityRating }}
                        </span>
                      }
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        }

        <!-- Connection Form -->
        @if (!connectionStatus()?.isConnected || showReconnectForm()) {
          <div class="space-y-4">
            @if (connectError()) {
              <div class="p-3 rounded-xl bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 flex items-start gap-2">
                <i class="pi pi-times-circle !text-red-500 !text-[18px] mt-0.5"></i>
                <div>
                  <div class="text-sm font-medium text-red-700 dark:text-red-400">{{ connectErrorTitle() }}</div>
                  <div class="text-xs text-red-600 dark:text-red-400/80 mt-1">{{ connectError() }}</div>
                </div>
              </div>
            }

            @if (connectSuccess()) {
              <div class="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800/40 flex items-start gap-2">
                <i class="pi pi-check-circle !text-emerald-500 !text-[18px] mt-0.5"></i>
                <div>
                  <div class="text-sm font-medium text-emerald-700 dark:text-emerald-300">{{ 'settings.connectionSuccess' | translate }}</div>
                  <div class="text-xs text-emerald-600 dark:text-emerald-400/80 mt-1">{{ connectSuccessMessage() }}</div>
                </div>
              </div>
            }

            @if (connectionStatus()?.isConnected) {
              <div>
                <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                  {{ 'settings.businessAccountId' | translate }}
                </label>
                <input type="text" [value]="connectionStatus()?.businessAccountId || ''" disabled
                  dir="ltr"
                  class="w-full px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 cursor-not-allowed font-mono text-sm text-left" />
              </div>
            } @else {
              <div>
                <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                  {{ 'settings.businessAccountId' | translate }} <span class="text-red-500">*</span>
                </label>
                <input type="text" [(ngModel)]="connectForm.businessAccountId" placeholder="e.g. 123456789012345"
                  class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none transition-all"
                  [class.!border-red-400]="submitted() && !connectForm.businessAccountId" />
                @if (submitted() && !connectForm.businessAccountId) {
                  <p class="text-xs text-red-500 mt-1">{{ 'settings.fieldRequired' | translate }}</p>
                }
              </div>
            }

            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                {{ 'settings.accessToken' | translate }} <span class="text-red-500">*</span>
              </label>
              <input [type]="showToken() ? 'text' : 'password'" [(ngModel)]="connectForm.accessToken"
                placeholder="EAAxxxxxxxxxxxxxxx..."
                class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none transition-all"
                [class.!border-red-400]="submitted() && !connectForm.accessToken" />
              <div class="flex items-center justify-between mt-1">
                @if (submitted() && !connectForm.accessToken) {
                  <p class="text-xs text-red-500">{{ 'settings.fieldRequired' | translate }}</p>
                } @else {
                  <span></span>
                }
                <button type="button" (click)="showToken.set(!showToken())"
                  class="text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 flex items-center gap-1 cursor-pointer bg-transparent border-none">
                  <i class="!text-[14px]" [class]="showToken() ? 'pi pi-eye-slash' : 'pi pi-eye'"></i>
                  {{ showToken() ? ('settings.hideToken' | translate) : ('settings.showToken' | translate) }}
                </button>
              </div>
            </div>

            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                {{ 'settings.webhookUrl' | translate }} <span class="text-xs text-slate-400">({{ 'settings.autoGenerated' | translate }})</span>
              </label>
              <input type="text" [value]="webhookDisplayUrl()" disabled
                dir="ltr"
                class="w-full px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 cursor-not-allowed font-mono text-sm text-left" />
            </div>

            <div class="flex items-center gap-3">
              <button pButton (click)="onConnect()" [disabled]="connecting()"
                class="!bg-[var(--app-primary)] !text-white !rounded-xl hover:!bg-[var(--app-primary-strong)] !px-6">
                @if (connecting()) { <p-progressSpinner [style]="{'width':'18px','height':'18px'}" strokeWidth="4" class="!inline-block mr-2" /> }
                <i class="pi pi-link !text-[18px]"></i>
                {{ connectionStatus()?.isConnected ? ('settings.updateAccessToken' | translate) : ('settings.connectButton' | translate) }}
              </button>
              @if (connectionStatus()?.isConnected) {
                <button pButton [outlined]="true" (click)="showReconnectForm.set(false)" class="!rounded-xl">
                  {{ 'common.cancel' | translate }}
                </button>
              }
            </div>
          </div>
        } @else {
          <button pButton [outlined]="true" (click)="openReconnectForm()"
            class="!rounded-xl !border-slate-300 dark:!border-slate-600 !text-slate-600 dark:!text-slate-300 !text-sm">
            <i class="pi pi-pencil !text-[16px]"></i>
            {{ 'settings.updateCredentials' | translate }}
          </button>
        }
      </div>

      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <div class="flex items-center justify-between gap-4 mb-6">
          <div>
            <h2 class="text-lg font-semibold text-slate-900 dark:text-white flex items-center gap-2">
              <i class="pi pi-share-alt text-emerald-500"></i> {{ 'settings.routing.title' | translate }}
            </h2>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">
              {{ 'settings.routing.subtitle' | translate }}
            </p>
          </div>
          @if (routingLoading()) {
            <p-progressSpinner [style]="{'width':'22px','height':'22px'}" strokeWidth="4" />
          }
        </div>

        <div class="space-y-5">
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'settings.routing.assignmentMode' | translate }}</label>
            <select [(ngModel)]="routingForm.assignmentMode"
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none">
              <option value="MANUAL">{{ 'settings.routing.mode.manual' | translate }}</option>
              <option value="AUTO">{{ 'settings.routing.mode.auto' | translate }}</option>
            </select>
          </div>

          <div class="space-y-4">
            <label class="flex items-center justify-between gap-4">
              <div>
                <div class="font-medium text-slate-900 dark:text-white">{{ 'settings.routing.reuseOwnerTitle' | translate }}</div>
                <div class="text-sm text-slate-500 dark:text-slate-400">{{ 'settings.routing.reuseOwnerDesc' | translate }}</div>
              </div>
              <p-toggleSwitch [(ngModel)]="routingForm.respectExistingContactOwner" />
            </label>

            <label class="flex items-center justify-between gap-4">
              <div>
                <div class="font-medium text-slate-900 dark:text-white">{{ 'settings.routing.reassignInactiveTitle' | translate }}</div>
                <div class="text-sm text-slate-500 dark:text-slate-400">{{ 'settings.routing.reassignInactiveDesc' | translate }}</div>
              </div>
              <p-toggleSwitch [(ngModel)]="routingForm.reassignWhenOwnerInactive" />
            </label>

            <label class="flex items-center justify-between gap-4">
              <div>
                <div class="font-medium text-slate-900 dark:text-white">{{ 'settings.routing.manualReassignTitle' | translate }}</div>
                <div class="text-sm text-slate-500 dark:text-slate-400">{{ 'settings.routing.manualReassignDesc' | translate }}</div>
              </div>
              <p-toggleSwitch [(ngModel)]="routingForm.manualReassignmentUpdatesContactOwner" />
            </label>
          </div>

          <div class="flex items-center justify-between gap-4 pt-2 border-t border-slate-200 dark:border-slate-700/50">
            <div class="text-xs text-slate-500 dark:text-slate-400">
              {{ 'settings.routing.strategyLabel' | translate }}: {{ routingForm.autoAssignmentStrategy }}
            </div>
            <button pButton (click)="saveRoutingSettings()" [disabled]="routingSaving() || routingLoading()"
              class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700">
              @if (routingSaving()) {
                <p-progressSpinner [style]="{'width':'16px','height':'16px'}" strokeWidth="4" class="!inline-block me-2" />
              }
              {{ 'settings.routing.saveButton' | translate }}
            </button>
          </div>
        </div>
      </div>

      <!-- Profile Section -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <h2 class="text-lg font-semibold text-slate-900 dark:text-white mb-6 flex items-center gap-2">
          <i class="pi pi-user text-emerald-500"></i> {{ 'settings.profile' | translate }}
        </h2>
        <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'settings.fullName' | translate }}</label>
            <input type="text" [value]="userName()" disabled
              class="w-full px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 cursor-not-allowed" />
          </div>
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'settings.role' | translate }}</label>
            <input type="text" [value]="userRole()" disabled
              class="w-full px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 cursor-not-allowed" />
          </div>
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'settings.companyId' | translate }}</label>
            <input type="text" [value]="companyId()" disabled
              class="w-full px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-500 dark:text-slate-400 cursor-not-allowed" />
          </div>
        </div>
      </div>

      <!-- Two-Factor Authentication -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <div class="flex items-center justify-between gap-4 mb-4">
          <div>
            <h2 class="text-lg font-semibold text-slate-900 dark:text-white flex items-center gap-2">
              <i class="pi pi-shield text-emerald-500"></i> {{ 'settings.twoFactor.title' | translate }}
            </h2>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'settings.twoFactor.subtitle' | translate }}</p>
          </div>
          @if (twoFactorLoading()) {
            <p-progressSpinner [style]="{'width':'22px','height':'22px'}" strokeWidth="4" />
          }
        </div>

        @if (twoFactorStatus()?.isEnabled) {
          <div class="rounded-xl border border-emerald-200 dark:border-emerald-800/40 bg-emerald-50 dark:bg-emerald-900/20 p-4 space-y-3">
            <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
              <div>
                <div class="text-sm font-semibold text-emerald-700 dark:text-emerald-300">
                  {{ 'settings.twoFactor.enabled' | translate }}
                </div>
                @if (twoFactorStatus()?.enabledAtUtc) {
                  <div class="text-xs text-emerald-700/90 dark:text-emerald-300/90 mt-1">
                    {{ 'settings.twoFactor.enabledAt' | translate }}: {{ twoFactorStatus()!.enabledAtUtc | date:'medium' }}
                  </div>
                }
              </div>

              <button
                pButton
                [outlined]="true"
                (click)="deactivateTwoFactor()"
                [disabled]="twoFactorWorking()"
                class="!rounded-xl !border-red-300 !text-red-600 dark:!text-red-300 !text-sm">
                @if (twoFactorWorking()) {
                  <p-progressSpinner [style]="{'width':'14px','height':'14px'}" strokeWidth="4" class="!inline-block me-1" />
                }
                {{ 'settings.twoFactor.disable' | translate }}
              </button>
            </div>
          </div>
        } @else {
          <div class="space-y-4">
            <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 bg-slate-50 dark:bg-slate-900/30 p-4">
              <p class="text-sm text-slate-600 dark:text-slate-300">
                {{ 'settings.twoFactor.disabledHint' | translate }}
              </p>
            </div>

            @if (!twoFactorSetup()) {
              <button
                pButton
                (click)="beginTwoFactorSetup()"
                [disabled]="twoFactorWorking()"
                class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700">
                @if (twoFactorWorking()) {
                  <p-progressSpinner [style]="{'width':'16px','height':'16px'}" strokeWidth="4" class="!inline-block me-2" />
                }
                {{ 'settings.twoFactor.startSetup' | translate }}
              </button>
            } @else {
              <div class="space-y-4">
                <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 p-4 space-y-3">
                  <div class="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">{{ 'settings.twoFactor.scanQr' | translate }}</div>
                  <div class="flex flex-col sm:flex-row sm:items-start gap-3">
                    <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 bg-white p-2 w-fit">
                      @if (twoFactorQrCodeDataUrl()) {
                        <img
                          [src]="twoFactorQrCodeDataUrl()!"
                          [alt]="'settings.twoFactor.scanQr' | translate"
                          width="200"
                          height="200"
                          class="block rounded-md" />
                      } @else {
                        <div class="w-[200px] h-[200px] rounded-md bg-slate-100 dark:bg-slate-800 flex items-center justify-center">
                          <i class="pi pi-qrcode !text-[34px] text-slate-400 dark:text-slate-500"></i>
                        </div>
                      }
                    </div>
                    <div class="space-y-2 text-xs text-slate-500 dark:text-slate-400">
                      <p>{{ 'settings.twoFactor.scanQrHint' | translate }}</p>
                      @if (twoFactorQrCodeError()) {
                        <p class="text-amber-600 dark:text-amber-400">
                          {{ 'settings.twoFactor.qrUnavailable' | translate }}
                        </p>
                      }
                    </div>
                  </div>
                </div>

                <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 p-4 space-y-2">
                  <div class="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">{{ 'settings.twoFactor.manualKey' | translate }}</div>
                  <div class="flex flex-col sm:flex-row sm:items-center gap-2">
                    <code class="px-3 py-2 rounded-lg bg-slate-100 dark:bg-slate-700/60 text-slate-900 dark:text-slate-100 font-mono break-all">
                      {{ twoFactorSetup()!.manualEntryKey }}
                    </code>
                    <button
                      pButton
                      [outlined]="true"
                      size="small"
                      class="!rounded-lg !text-xs"
                      (click)="copyValue(twoFactorSetup()!.manualEntryKey, 'settings.twoFactor.feedback.keyCopied')">
                      {{ 'settings.twoFactor.copy' | translate }}
                    </button>
                  </div>
                  <p class="text-xs text-slate-500 dark:text-slate-400">
                    {{ 'settings.twoFactor.manualKeyHint' | translate }}
                  </p>
                </div>

                <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 p-4 space-y-2">
                  <div class="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">{{ 'settings.twoFactor.openAuthenticator' | translate }}</div>
                  <a
                    [href]="twoFactorSetup()!.otpAuthUri"
                    target="_blank"
                    rel="noopener"
                    class="inline-flex items-center gap-2 text-sm text-emerald-600 dark:text-emerald-400 hover:underline">
                    <i class="pi pi-external-link !text-[12px]"></i>
                    {{ 'settings.twoFactor.openAuthenticatorLink' | translate }}
                  </a>
                  <div>
                    <button
                      pButton
                      [outlined]="true"
                      size="small"
                      class="!rounded-lg !text-xs"
                      (click)="copyValue(twoFactorSetup()!.otpAuthUri, 'settings.twoFactor.feedback.uriCopied')">
                      {{ 'settings.twoFactor.copyUri' | translate }}
                    </button>
                  </div>
                </div>

                <div class="space-y-2">
                  <label class="block text-sm font-medium text-slate-700 dark:text-slate-300">
                    {{ 'settings.twoFactor.verificationCode' | translate }}
                  </label>
                  <div class="flex justify-center sm:justify-start px-1">
                    <p-inputOtp [(ngModel)]="twoFactorActivationCode" name="twoFactorActivationCode" [length]="6" [integerOnly]="true"></p-inputOtp>
                  </div>
                  @if (twoFactorActivationError()) {
                    <p class="text-xs text-red-500">{{ twoFactorActivationError() }}</p>
                  }
                </div>

                <div class="flex flex-wrap items-center gap-2">
                  <button
                    pButton
                    (click)="activateTwoFactor()"
                    [disabled]="twoFactorWorking() || twoFactorActivationCode.length < 6"
                    class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700">
                    @if (twoFactorWorking()) {
                      <p-progressSpinner [style]="{'width':'16px','height':'16px'}" strokeWidth="4" class="!inline-block me-2" />
                    }
                    {{ 'settings.twoFactor.activate' | translate }}
                  </button>

                  <button
                    pButton
                    [outlined]="true"
                    (click)="cancelTwoFactorSetup()"
                    [disabled]="twoFactorWorking()"
                    class="!rounded-xl">
                    {{ 'common.cancel' | translate }}
                  </button>
                </div>
              </div>
            }
          </div>
        }
      </div>

      <!-- Appearance -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <h2 class="text-lg font-semibold text-slate-900 dark:text-white mb-6 flex items-center gap-2">
          <i class="pi pi-palette text-emerald-500"></i> {{ 'settings.appearance' | translate }}
        </h2>
        <div class="flex items-center justify-between">
          <div>
            <h4 class="font-medium text-slate-900 dark:text-white">{{ 'settings.darkMode' | translate }}</h4>
            <p class="text-sm text-slate-500 dark:text-slate-400">{{ 'settings.darkModeDesc' | translate }}</p>
          </div>
          <button pButton (click)="theme.toggle()"
            class="!rounded-xl"
            [class]="theme.mode() === 'dark' ? '!bg-yellow-500 !text-white' : '!bg-slate-800 !text-white'">
            <i [class]="theme.mode() === 'dark' ? 'pi pi-sun' : 'pi pi-moon'"></i>
            {{ theme.mode() === 'dark' ? ('settings.lightMode' | translate) : ('settings.darkMode' | translate) }}
          </button>
        </div>
      </div>

      <!-- Change Password -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <h2 class="text-lg font-semibold text-slate-900 dark:text-white mb-6 flex items-center gap-2">
          <i class="pi pi-bell text-emerald-500"></i> {{ 'settings.notifications' | translate }}
        </h2>
        <div class="space-y-5">
          <!-- Desktop Notifications -->
          <div class="flex items-center justify-between">
            <div>
              <h4 class="font-medium text-slate-900 dark:text-white">{{ 'settings.desktopNotifications' | translate }}</h4>
              <p class="text-sm text-slate-500 dark:text-slate-400">{{ 'settings.desktopNotificationsDesc' | translate }}</p>
            </div>
            <button (click)="toggleDesktopNotifications()"
              [attr.aria-pressed]="notifService.preferences().desktopEnabled"
              class="relative w-12 h-7 rounded-full transition-colors duration-200"
              [class]="notifService.preferences().desktopEnabled ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-600'">
              <span class="absolute top-0.5 start-0.5 w-6 h-6 bg-white rounded-full shadow transition-transform duration-200"
                [class.translate-x-5]="notifService.preferences().desktopEnabled && !langService.isRtl()"
                [class.-translate-x-5]="notifService.preferences().desktopEnabled && langService.isRtl()"></span>
            </button>
          </div>
          <!-- Sound -->
          <div class="flex items-center justify-between">
            <div>
              <h4 class="font-medium text-slate-900 dark:text-white">{{ 'settings.soundNotifications' | translate }}</h4>
              <p class="text-sm text-slate-500 dark:text-slate-400">{{ 'settings.soundNotificationsDesc' | translate }}</p>
            </div>
            <button (click)="toggleSoundNotifications()"
              [attr.aria-pressed]="notifService.preferences().soundEnabled"
              class="relative w-12 h-7 rounded-full transition-colors duration-200"
              [class]="notifService.preferences().soundEnabled ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-600'">
              <span class="absolute top-0.5 start-0.5 w-6 h-6 bg-white rounded-full shadow transition-transform duration-200"
                [class.translate-x-5]="notifService.preferences().soundEnabled && !langService.isRtl()"
                [class.-translate-x-5]="notifService.preferences().soundEnabled && langService.isRtl()"></span>
            </button>
          </div>
          <!-- Volume slider -->
          @if (notifService.preferences().soundEnabled) {
            <div class="pt-1">
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                {{ 'settings.volume' | translate }}:
                <bdi dir="ltr">{{ (notifService.preferences().soundVolume * 100) | number:'1.0-0' }}%</bdi>
              </label>
              <input type="range" min="0" max="100" [value]="notifService.preferences().soundVolume * 100" (input)="onVolumeChange($event)"
                class="w-full h-2 bg-slate-200 dark:bg-slate-600 rounded-full appearance-none cursor-pointer accent-emerald-500" />
            </div>
          }
          <!-- Test Sound -->
          <button pButton [outlined]="true" class="!rounded-xl !border-emerald-300 !text-emerald-600 dark:!text-emerald-400 !text-sm" (click)="notifService.playSound()">
            <i class="pi pi-volume-up !text-[18px] me-1"></i>
            {{ 'settings.testSound' | translate }}
          </button>
        </div>
      </div>

      <!-- Change Password -->
      <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
        <h2 class="text-lg font-semibold text-slate-900 dark:text-white mb-6 flex items-center gap-2">
          <i class="pi pi-lock text-emerald-500"></i> {{ 'settings.changePassword' | translate }}
        </h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'settings.newPassword' | translate }}</label>
            <input type="password" [(ngModel)]="newPassword"
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
          </div>
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">{{ 'settings.confirmPassword' | translate }}</label>
            <input type="password" [(ngModel)]="confirmPassword"
              class="w-full px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 outline-none" />
          </div>
          <button pButton class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700" (click)="changePassword()">
            {{ 'settings.updatePassword' | translate }}
          </button>
        </div>
      </div>

      <!-- Danger Zone -->
      <div class="bg-red-50 dark:bg-red-900/10 rounded-2xl border border-red-200 dark:border-red-800/50 p-6 xl:col-span-2">
        <h2 class="text-lg font-semibold text-red-700 dark:text-red-400 mb-4 flex items-center gap-2">
          <i class="pi pi-exclamation-triangle !text-red-500"></i> {{ 'settings.dangerZone' | translate }}
        </h2>
        <p class="text-sm text-red-600 dark:text-red-400 mb-4">{{ 'settings.dangerZoneDesc' | translate }}</p>
        <button pButton [outlined]="true" class="!border-red-500 !text-red-600 !rounded-xl hover:!bg-red-50 dark:hover:!bg-red-900/20">
          {{ 'settings.deleteAccount' | translate }}
        </button>
      </div>
      </div>
    </div>
  `,
  styles: [`
    :host ::ng-deep .p-inputotp {
      display: flex;
      gap: 0.35rem;
    }

    :host ::ng-deep .p-inputotp .p-inputotp-input {
      width: 2.35rem;
      height: 2.65rem;
      text-align: center;
    }
  `],
})
export class SettingsComponent implements OnInit {
  private token = inject(TokenService);
  private messageService = inject(MessageService);
  private api = inject(ApiService);
  private companyService = inject(CompanyService);
  private authService = inject(AuthService);
  private translate = inject(TranslateService);
  langService = inject(LanguageService);
  theme = inject(ThemeService);
  notifService = inject(NotificationManagerService);

  userName = signal('');
  userRole = signal('');
  companyId = signal('');
  newPassword = '';
  confirmPassword = '';

  // WhatsApp Connection State
  connectionStatus = signal<WhatsAppConnectionStatus | null>(null);
  connectionLoading = signal(false);
  routingLoading = signal(false);
  routingSaving = signal(false);
  subscriptionUsageLoading = signal(false);
  connecting = signal(false);
  connectError = signal<string | null>(null);
  connectErrorTitle = signal('');
  connectSuccess = signal(false);
  connectSuccessMessage = signal('');
  showReconnectForm = signal(false);
  showToken = signal(false);
  showVerifyToken = signal(false);
  rotatingVerifyToken = signal(false);
  submitted = signal(false);
  subscriptionUsage = signal<SubscriptionUsageSnapshot | null>(null);
  twoFactorLoading = signal(false);
  twoFactorWorking = signal(false);
  twoFactorStatus = signal<TwoFactorStatus | null>(null);
  twoFactorSetup = signal<TwoFactorSetup | null>(null);
  twoFactorQrCodeDataUrl = signal<string | null>(null);
  twoFactorQrCodeError = signal(false);
  twoFactorActivationError = signal('');
  twoFactorActivationCode = '';

  connectForm: ConnectMetaRequest = {
    businessAccountId: '',
    accessToken: '',
  };
  routingSettings = signal<CompanyRoutingSettings | null>(null);
  routingForm: UpdateCompanyRoutingSettingsRequest = {
    assignmentMode: 'MANUAL',
    autoAssignmentStrategy: 'ROUND_ROBIN',
    respectExistingContactOwner: true,
    reassignWhenOwnerInactive: true,
    manualReassignmentUpdatesContactOwner: false,
  };

  webhookDisplayUrl = computed(() => {
    const backendWebhookUrl = this.connectionStatus()?.webhookUrl?.trim();
    if (backendWebhookUrl) {
      return backendWebhookUrl;
    }

    return this.buildWebhookUrlFromApiBase();
  });

  ngOnInit(): void {
    this.userName.set(this.translate.instant('settings.userFallback', { id: this.token.userId() || '' }));
    this.userRole.set(this.token.role() ?? '');
    this.companyId.set(String(this.token.companyId() ?? ''));
    this.refreshSubscriptionUsage();
    this.refreshConnection();
    this.refreshRoutingSettings();
    this.loadTwoFactorStatus();
  }

  refreshSubscriptionUsage(): void {
    this.subscriptionUsageLoading.set(true);
    this.api.get<SubscriptionUsageSnapshot>('/subscriptions/usage').subscribe({
      next: (usage) => {
        this.subscriptionUsage.set(usage);
        this.subscriptionUsageLoading.set(false);
      },
      error: () => {
        this.subscriptionUsage.set(null);
        this.subscriptionUsageLoading.set(false);
      },
    });
  }

  usagePercent(used: number, limit: number): number {
    if (limit <= 0) return 0;
    return Math.max(0, Math.min(100, Math.round((used / limit) * 100)));
  }

  refreshConnection(): void {
    this.connectionLoading.set(true);
    this.companyService.loadConnectionStatus(true).subscribe({
      next: (status) => {
        this.connectionStatus.set(status);
        if (status?.businessAccountId) {
          this.connectForm.businessAccountId = status.businessAccountId;
        }
        this.connectionLoading.set(false);
      },
      error: () => {
        this.connectionLoading.set(false);
      },
    });
  }

  refreshRoutingSettings(): void {
    this.routingLoading.set(true);
    this.api.get<CompanyRoutingSettings>('/routing/settings').subscribe({
      next: (settings) => {
        this.routingSettings.set(settings);
        this.routingForm = {
          assignmentMode: settings.assignmentMode,
          autoAssignmentStrategy: settings.autoAssignmentStrategy,
          respectExistingContactOwner: settings.respectExistingContactOwner,
          reassignWhenOwnerInactive: settings.reassignWhenOwnerInactive,
          manualReassignmentUpdatesContactOwner: settings.manualReassignmentUpdatesContactOwner,
        };
        this.routingLoading.set(false);
      },
      error: () => this.routingLoading.set(false),
    });
  }

  saveRoutingSettings(): void {
    this.routingSaving.set(true);
    this.api.put<CompanyRoutingSettings>('/routing/settings', this.routingForm).subscribe({
      next: (settings) => {
        this.routingSettings.set(settings);
        this.routingSaving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.translate.instant('settings.routing.feedback.updated'),
          life: 3000,
        });
      },
      error: (err) => {
        this.routingSaving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: err?.error?.message || this.translate.instant('settings.routing.feedback.updateFailed'),
          life: 4000,
        });
      },
    });
  }

  loadTwoFactorStatus(): void {
    this.twoFactorLoading.set(true);
    this.authService.getTwoFactorStatus().subscribe({
      next: (status) => {
        this.twoFactorStatus.set(status);
        if (status.isEnabled) {
          this.twoFactorSetup.set(null);
          this.twoFactorQrCodeDataUrl.set(null);
          this.twoFactorQrCodeError.set(false);
          this.twoFactorActivationCode = '';
          this.twoFactorActivationError.set('');
        }
        this.twoFactorLoading.set(false);
      },
      error: () => {
        this.twoFactorLoading.set(false);
      },
    });
  }

  beginTwoFactorSetup(): void {
    this.twoFactorWorking.set(true);
    this.twoFactorActivationError.set('');

    this.authService.beginTwoFactorSetup().subscribe({
      next: (setup) => {
        this.twoFactorSetup.set(setup);
        this.generateTwoFactorQrCode(setup.otpAuthUri);
        this.twoFactorActivationCode = '';
        this.twoFactorWorking.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.translate.instant('settings.twoFactor.feedback.setupReady'),
          life: 3200,
        });
      },
      error: (err) => {
        this.twoFactorWorking.set(false);
        this.messageService.add({
          severity: 'error',
          summary: err?.error?.message || this.translate.instant('settings.twoFactor.feedback.setupFailed'),
          life: 4200,
        });
      },
    });
  }

  activateTwoFactor(): void {
    const normalizedCode = this.twoFactorActivationCode.trim();
    if (!/^\d{6}$/.test(normalizedCode)) {
      this.twoFactorActivationError.set(this.translate.instant('settings.twoFactor.codeInvalid'));
      return;
    }

    this.twoFactorWorking.set(true);
    this.twoFactorActivationError.set('');

    this.authService.activateTwoFactor(normalizedCode).subscribe({
      next: (status) => {
        this.twoFactorStatus.set(status);
        this.twoFactorSetup.set(null);
        this.twoFactorQrCodeDataUrl.set(null);
        this.twoFactorQrCodeError.set(false);
        this.twoFactorActivationCode = '';
        this.twoFactorWorking.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.translate.instant('settings.twoFactor.feedback.activated'),
          life: 3200,
        });
      },
      error: (err) => {
        this.twoFactorWorking.set(false);
        const message = err?.error?.message || this.translate.instant('settings.twoFactor.feedback.activateFailed');
        this.twoFactorActivationError.set(message);
      },
    });
  }

  deactivateTwoFactor(): void {
    const confirmed = confirm(this.translate.instant('settings.twoFactor.confirmDisable'));
    if (!confirmed) {
      return;
    }

    this.twoFactorWorking.set(true);
    this.authService.deactivateTwoFactor().subscribe({
      next: (status) => {
        this.twoFactorStatus.set(status);
        this.twoFactorSetup.set(null);
        this.twoFactorQrCodeDataUrl.set(null);
        this.twoFactorQrCodeError.set(false);
        this.twoFactorActivationCode = '';
        this.twoFactorActivationError.set('');
        this.twoFactorWorking.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.translate.instant('settings.twoFactor.feedback.deactivated'),
          life: 3200,
        });
      },
      error: (err) => {
        this.twoFactorWorking.set(false);
        this.messageService.add({
          severity: 'error',
          summary: err?.error?.message || this.translate.instant('settings.twoFactor.feedback.deactivateFailed'),
          life: 4200,
        });
      },
    });
  }

  cancelTwoFactorSetup(): void {
    this.twoFactorSetup.set(null);
    this.twoFactorQrCodeDataUrl.set(null);
    this.twoFactorQrCodeError.set(false);
    this.twoFactorActivationCode = '';
    this.twoFactorActivationError.set('');
  }

  private generateTwoFactorQrCode(otpAuthUri: string): void {
    const normalizedUri = (otpAuthUri ?? '').trim();
    if (!normalizedUri) {
      this.twoFactorQrCodeDataUrl.set(null);
      this.twoFactorQrCodeError.set(true);
      return;
    }

    void QRCode.toDataURL(normalizedUri, {
      width: 220,
      margin: 1,
      errorCorrectionLevel: 'M',
      color: {
        dark: '#0f172a',
        light: '#ffffffff',
      },
    })
      .then((dataUrl: string) => {
        this.twoFactorQrCodeDataUrl.set(dataUrl);
        this.twoFactorQrCodeError.set(false);
      })
      .catch(() => {
        this.twoFactorQrCodeDataUrl.set(null);
        this.twoFactorQrCodeError.set(true);
        this.messageService.add({
          severity: 'warn',
          summary: this.translate.instant('settings.twoFactor.feedback.qrUnavailable'),
          life: 3500,
        });
      });
  }

  async copyValue(value: string, successMessageKey: string): Promise<void> {
    const normalized = (value ?? '').trim();
    if (!normalized) return;

    try {
      await navigator.clipboard.writeText(normalized);
      this.messageService.add({ severity: 'success', summary: this.translate.instant(successMessageKey), life: 2500 });
    } catch {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('settings.twoFactor.feedback.copyFailed'), life: 3000 });
    }
  }

  onConnect(): void {
    this.submitted.set(true);
    this.connectError.set(null);
    this.connectSuccess.set(false);

    const isUpdateMode = !!this.connectionStatus()?.isConnected;
    const businessAccountId = isUpdateMode
      ? (this.connectionStatus()?.businessAccountId ?? '').trim()
      : this.connectForm.businessAccountId.trim();
    const accessToken = this.connectForm.accessToken.trim();

    if (!businessAccountId || !accessToken) {
      return;
    }

    this.connecting.set(true);

    const endpoint = isUpdateMode ? '/company/access-token' : '/company/connect-meta';
    const payload = isUpdateMode
      ? { accessToken }
      : { businessAccountId, accessToken };

    this.api.postRaw<ConnectMetaResponse>(endpoint, payload).subscribe({
      next: (response) => {
        this.connecting.set(false);
        if (response.success && response.data) {
          this.connectSuccess.set(true);
          this.connectSuccessMessage.set(this.translate.instant(
            isUpdateMode ? 'settings.feedback.accessTokenUpdatedFor' : 'settings.feedback.connectedTo',
            {
              accountName: response.data.businessAccountName || this.translate.instant('settings.feedback.defaultWhatsAppBusiness'),
              count: response.data.phoneNumbersImported,
            },
          ));
          this.submitted.set(false);
          this.showReconnectForm.set(false);
          this.showToken.set(false);
          this.connectForm = { businessAccountId, accessToken: '' };
          this.refreshConnection();
        } else {
          this.connectError.set(response.message || this.translate.instant('settings.feedback.connectionFailed'));
          this.connectErrorTitle.set(this.mapStatusToTitle(response.data?.status));
        }
      },
      error: (err) => {
        this.connecting.set(false);
        const errorData = err.error;
        const status = errorData?.data?.status || '';
        this.connectErrorTitle.set(this.mapStatusToTitle(status));
        this.connectError.set(errorData?.message || err.message || this.translate.instant('settings.feedback.connectionFailed'));
      },
    });
  }

  openReconnectForm(): void {
    const businessAccountId = this.connectionStatus()?.businessAccountId ?? '';
    this.connectForm = { businessAccountId, accessToken: '' };
    this.connectError.set(null);
    this.connectSuccess.set(false);
    this.submitted.set(false);
    this.showToken.set(false);
    this.showReconnectForm.set(true);
  }

  displayedVerifyToken(): string {
    const token = this.connectionStatus()?.verifyToken?.trim() ?? '';
    if (!token) return '';
    if (this.showVerifyToken()) return token;
    if (token.length <= 8) return '********';
    return `${token.slice(0, 4)}********${token.slice(-4)}`;
  }

  async copyVerifyToken(): Promise<void> {
    const token = this.connectionStatus()?.verifyToken?.trim() ?? '';
    if (!token) return;

    try {
      await navigator.clipboard.writeText(token);
      this.messageService.add({ severity: 'success', summary: this.translate.instant('settings.feedback.verifyTokenCopied'), life: 2500 });
    } catch {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('settings.feedback.verifyTokenCopyFailed'), life: 3000 });
    }
  }

  rotateVerifyToken(): void {
    this.rotatingVerifyToken.set(true);
    this.api.post<RotateVerifyTokenResponse>('/company/verify-token/rotate', {}).subscribe({
      next: (result) => {
        const current = this.connectionStatus();
        if (current) {
          this.connectionStatus.set({
            ...current,
            verifyToken: result.verifyToken,
          });
        }
        this.rotatingVerifyToken.set(false);
        this.showVerifyToken.set(true);
        this.messageService.add({ severity: 'success', summary: this.translate.instant('settings.feedback.verifyTokenRotated'), life: 3000 });
      },
      error: (err) => {
        this.rotatingVerifyToken.set(false);
        this.messageService.add({
          severity: 'error',
          summary: err?.error?.message || this.translate.instant('settings.feedback.verifyTokenRotateFailed'),
          life: 4000,
        });
      },
    });
  }

  changePassword(): void {
    if (!this.newPassword) {
      this.messageService.add({ severity: 'warn', summary: this.translate.instant('settings.feedback.enterNewPassword'), life: 3000 });
      return;
    }
    if (this.newPassword !== this.confirmPassword) {
      this.messageService.add({ severity: 'error', summary: this.translate.instant('settings.feedback.passwordsDoNotMatch'), life: 3000 });
      return;
    }
    this.messageService.add({ severity: 'info', summary: this.translate.instant('settings.feedback.passwordChangeNotImplemented'), life: 3000 });
  }

  // ─── Notification methods ───
  async toggleDesktopNotifications(): Promise<void> {
    const current = this.notifService.preferences().desktopEnabled;
    if (!current) {
      const granted = await this.notifService.requestPermission();
      if (!granted) {
        this.messageService.add({ severity: 'warn', summary: this.translate.instant('settings.feedback.browserNotificationDenied'), life: 3000 });
        return;
      }
    }
    this.notifService.savePreferences({ desktopEnabled: !current });
  }

  toggleSoundNotifications(): void {
    this.notifService.savePreferences({ soundEnabled: !this.notifService.preferences().soundEnabled });
  }

  onVolumeChange(event: Event): void {
    const val = +(event.target as HTMLInputElement).value / 100;
    this.notifService.savePreferences({ soundVolume: val });
  }

  private mapStatusToTitle(status: string | undefined | null): string {
    switch (status) {
      case 'InvalidToken': return this.translate.instant('settings.connectErrorTitles.invalidToken');
      case 'AccountNotFound': return this.translate.instant('settings.connectErrorTitles.accountNotFound');
      case 'PermissionDenied': return this.translate.instant('settings.connectErrorTitles.permissionDenied');
      default: return this.translate.instant('settings.connectErrorTitles.connectionError');
    }
  }

  private buildWebhookUrlFromApiBase(): string {
    const apiUrl = environment.apiUrl.trim();
    const normalizedApiPath = apiUrl.replace(/\/+$/, '').replace(/\/api$/, '/api');

    if (/^https?:\/\//i.test(normalizedApiPath)) {
      return normalizedApiPath.replace(/\/api$/, '') + '/api/webhook';
    }

    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    const relativePath = normalizedApiPath.startsWith('/') ? normalizedApiPath : `/${normalizedApiPath}`;
    return `${origin}${relativePath.replace(/\/api$/, '')}/api/webhook`;
  }
}
