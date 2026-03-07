import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../../core/services';
import { SubscriptionPlan, CompanySubscription } from '../../../core/models';

@Component({
  selector: 'app-billing',
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="space-y-8">
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">Billing & Subscription</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">Manage your plan and view usage</p>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-16"><mat-spinner diameter="36"></mat-spinner></div>
      } @else {
        <!-- Current Plan -->
        @if (currentSub()) {
          <div class="bg-gradient-to-br from-emerald-600 to-green-700 rounded-2xl p-8 text-white">
            <div class="flex items-start justify-between">
              <div>
                <h3 class="text-sm font-medium text-emerald-200 uppercase tracking-wider mb-2">Current Plan</h3>
                <h2 class="text-3xl font-bold mb-1">{{ currentPlanName() }}</h2>
                <div class="flex items-center gap-2 mt-2">
                  <span class="px-3 py-1 rounded-full text-xs font-semibold bg-white/20">
                    {{ currentSub()!.status }}
                  </span>
                  @if (currentSub()!.trialEndDate) {
                    <span class="text-sm text-emerald-200">
                      Trial ends: {{ currentSub()!.trialEndDate!.slice(0, 10) }}
                    </span>
                  }
                </div>
              </div>
              <mat-icon class="!text-[48px] text-emerald-300/30">card_membership</mat-icon>
            </div>
          </div>
        }

        <!-- Available Plans -->
        <h3 class="text-lg font-semibold text-slate-900 dark:text-white">Available Plans</h3>
        <div class="grid md:grid-cols-3 gap-6">
          @for (plan of plans(); track plan.subscriptionPlanId) {
            <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 hover:shadow-lg transition-shadow"
              [class.!border-emerald-500]="isCurrentPlan(plan)">
              <h3 class="text-lg font-bold text-slate-900 dark:text-white mb-1">{{ plan.name }}</h3>
              <div class="mb-4">
                <span class="text-3xl font-bold text-slate-900 dark:text-white">\${{ plan.monthlyPrice }}</span>
                <span class="text-sm text-slate-500">/mo</span>
              </div>
              <ul class="space-y-2 mb-6 list-none p-0 m-0">
                <li class="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                  <mat-icon class="!text-emerald-500 !text-[16px]">check</mat-icon>
                  {{ plan.maxMessagesPerMonth | number }} messages/month
                </li>
                <li class="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                  <mat-icon class="!text-emerald-500 !text-[16px]">check</mat-icon>
                  {{ plan.maxWhatsAppAccounts }} WhatsApp accounts
                </li>
                <li class="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                  <mat-icon class="!text-emerald-500 !text-[16px]">check</mat-icon>
                  {{ plan.trialDays }}-day free trial
                </li>
              </ul>
              @if (isCurrentPlan(plan)) {
                <button mat-stroked-button disabled class="!rounded-xl w-full">Current Plan</button>
              } @else {
                <button mat-flat-button class="!bg-emerald-600 !text-white !rounded-xl w-full hover:!bg-emerald-700">
                  Upgrade
                </button>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class BillingComponent implements OnInit {
  private api = inject(ApiService);

  plans = signal<SubscriptionPlan[]>([]);
  currentSub = signal<CompanySubscription | null>(null);
  loading = signal(true);

  currentPlanName = () => {
    const sub = this.currentSub();
    if (!sub) return 'None';
    const plan = this.plans().find((p) => p.subscriptionPlanId === sub.subscriptionPlanId);
    return plan?.name ?? 'Unknown';
  };

  isCurrentPlan = (plan: SubscriptionPlan) => {
    return this.currentSub()?.subscriptionPlanId === plan.subscriptionPlanId;
  };

  ngOnInit(): void {
    this.api.get<SubscriptionPlan[]>('/subscriptions/plans').subscribe({
      next: (plans) => {
        this.plans.set(plans);
        this.api.get<CompanySubscription>('/subscriptions/current').subscribe({
          next: (sub) => { this.currentSub.set(sub); this.loading.set(false); },
          error: () => this.loading.set(false),
        });
      },
      error: () => this.loading.set(false),
    });
  }
}
