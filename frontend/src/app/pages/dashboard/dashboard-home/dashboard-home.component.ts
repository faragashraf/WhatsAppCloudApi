import { Component, inject, OnInit, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService, TokenService } from '../../../core/services';
import {
  CompanySubscription, Message, WhatsAppAccount, WhatsAppPhoneNumber,
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
  imports: [MatIconModule, MatProgressSpinnerModule, RouterLink],
  template: `
    <div class="space-y-8">
      <!-- Header -->
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">Dashboard</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">Welcome back! Here's an overview of your account.</p>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-20">
          <mat-spinner diameter="40"></mat-spinner>
        </div>
      } @else {
        <!-- Stats Cards -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
          @for (card of statCards(); track card.label) {
            <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 hover:shadow-lg hover:shadow-emerald-500/5 transition-all">
              <div class="flex items-center justify-between mb-4">
                <div class="w-10 h-10 rounded-xl flex items-center justify-center" [class]="card.color">
                  <mat-icon class="!text-[20px]">{{ card.icon }}</mat-icon>
                </div>
                @if (card.change) {
                  <span class="text-xs font-medium px-2 py-1 rounded-full bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400">
                    {{ card.change }}
                  </span>
                }
              </div>
              <div class="text-2xl font-bold text-slate-900 dark:text-white mb-1">{{ card.value }}</div>
              <div class="text-xs text-slate-500 dark:text-slate-400 uppercase tracking-wider">{{ card.label }}</div>
            </div>
          }
        </div>

        <!-- Bottom Row -->
        <div class="grid lg:grid-cols-2 gap-6">
          <!-- Subscription Info -->
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
            <h3 class="text-lg font-semibold text-slate-900 dark:text-white mb-4">Subscription</h3>
            @if (subscription()) {
              <div class="space-y-3">
                <div class="flex justify-between items-center">
                  <span class="text-sm text-slate-500">Status</span>
                  <span class="px-3 py-1 rounded-full text-xs font-semibold"
                    [class]="subscription()!.status === 'TRIAL'
                      ? 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-400'
                      : 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400'">
                    {{ subscription()!.status }}
                  </span>
                </div>
                @if (subscription()!.trialEndDate) {
                  <div class="flex justify-between items-center">
                    <span class="text-sm text-slate-500">Trial Ends</span>
                    <span class="text-sm font-medium text-slate-900 dark:text-white">{{ subscription()!.trialEndDate?.slice(0, 10) }}</span>
                  </div>
                }
                <a routerLink="/dashboard/billing" class="inline-flex items-center gap-1 text-sm text-emerald-600 dark:text-emerald-400 font-medium hover:underline no-underline mt-2">
                  Manage Subscription
                  <mat-icon class="!text-[16px]">arrow_forward</mat-icon>
                </a>
              </div>
            }
          </div>

          <!-- Recent Messages -->
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-semibold text-slate-900 dark:text-white">Recent Messages</h3>
              <a routerLink="/dashboard/messages" class="text-sm text-emerald-600 dark:text-emerald-400 font-medium hover:underline no-underline">
                View all
              </a>
            </div>
            @if (recentMessages().length === 0) {
              <div class="text-center py-8 text-slate-400">
                <mat-icon class="!text-[40px] mb-2">chat_bubble_outline</mat-icon>
                <p class="text-sm">No messages sent yet</p>
              </div>
            } @else {
              <div class="space-y-3">
                @for (msg of recentMessages().slice(0, 5); track msg.messageId) {
                  <div class="flex items-center justify-between py-2 border-b border-slate-100 dark:border-slate-700/30 last:border-0">
                    <div class="flex items-center gap-3">
                      <div class="w-8 h-8 rounded-lg flex items-center justify-center"
                        [class]="msg.status === 'SENT' ? 'bg-emerald-100 dark:bg-emerald-900/40' : msg.status === 'FAILED' ? 'bg-red-100 dark:bg-red-900/40' : 'bg-amber-100 dark:bg-amber-900/40'">
                        <mat-icon class="!text-[16px]"
                          [class]="msg.status === 'SENT' ? '!text-emerald-600' : msg.status === 'FAILED' ? '!text-red-600' : '!text-amber-600'">
                          {{ msg.status === 'SENT' ? 'check_circle' : msg.status === 'FAILED' ? 'error' : 'schedule' }}
                        </mat-icon>
                      </div>
                      <div>
                        <div class="text-sm font-medium text-slate-900 dark:text-white">{{ msg.toNumber }}</div>
                        <div class="text-xs text-slate-500">{{ msg.messageType }}</div>
                      </div>
                    </div>
                    <span class="text-xs px-2 py-1 rounded-full font-medium"
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
        </div>
      }
    </div>
  `,
})
export class DashboardHomeComponent implements OnInit {
  private api = inject(ApiService);
  private tokenService = inject(TokenService);

  loading = signal(true);
  statCards = signal<StatCard[]>([]);
  subscription = signal<CompanySubscription | null>(null);
  recentMessages = signal<Message[]>([]);

  ngOnInit(): void {
    forkJoin({
      accounts: this.api.get<WhatsAppAccount[]>('/whatsapp-accounts'),
      phones: this.api.get<WhatsAppPhoneNumber[]>('/phone-numbers'),
      subscriptions: this.api.get<CompanySubscription[]>('/subscriptions'),
    }).subscribe({
      next: ({ accounts, phones, subscriptions }) => {
        const activeSub = subscriptions.find((s) => s.isActive) ?? null;
        this.subscription.set(activeSub);

        this.statCards.set([
          {
            label: 'WhatsApp Accounts',
            value: String(accounts.length),
            icon: 'router',
            color: 'bg-blue-100 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400',
          },
          {
            label: 'Phone Numbers',
            value: String(phones.length),
            icon: 'phone_iphone',
            color: 'bg-purple-100 dark:bg-purple-900/40 text-purple-600 dark:text-purple-400',
          },
          {
            label: 'Subscription',
            value: activeSub?.status ?? 'None',
            icon: 'card_membership',
            color: 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-600 dark:text-emerald-400',
          },
          {
            label: 'Active Numbers',
            value: String(phones.filter((p) => p.isActive).length),
            icon: 'check_circle',
            color: 'bg-amber-100 dark:bg-amber-900/40 text-amber-600 dark:text-amber-400',
          },
        ]);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
