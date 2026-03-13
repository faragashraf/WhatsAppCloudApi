import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { TranslateModule } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../../core/services';
import { SubscriptionPlan, CompanySubscription } from '../../../core/models';

@Component({
  selector: 'app-billing',
  standalone: true,
  imports: [DecimalPipe, ButtonModule, ProgressSpinnerModule, ToastModule, TranslateModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="space-y-8 min-w-0 app-wrap-safe">
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'billing.title' | translate }}</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'billing.subtitle' | translate }}</p>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-16"><p-progressSpinner [style]="{'width':'36px','height':'36px'}" strokeWidth="4" /></div>
      } @else {
        <!-- Current Plan -->
        @if (currentSub()) {
          <div class="bg-gradient-to-br from-emerald-600 to-green-700 rounded-2xl p-6 sm:p-7 lg:p-8 text-white">
            <div class="flex flex-wrap items-start justify-between gap-4">
              <div>
                <h3 class="text-sm font-medium text-emerald-200 uppercase tracking-wider mb-2">{{ 'billing.currentPlan' | translate }}</h3>
                <h2 class="text-3xl font-bold mb-1">{{ currentPlanName() }}</h2>
                <div class="flex items-center gap-2 mt-2">
                  <span class="px-3 py-1 rounded-full text-xs font-semibold bg-white/20">
                    {{ currentSub()!.status }}
                  </span>
                  @if (currentSub()!.trialEndDate) {
                    <span class="text-sm text-emerald-200">
                      {{ 'billing.trialEnds' | translate }}: {{ currentSub()!.trialEndDate!.slice(0, 10) }}
                    </span>
                  }
                </div>
              </div>
              <i class="pi pi-credit-card !text-[48px] text-emerald-300/30"></i>
            </div>
          </div>
        }

        <!-- Available Plans -->
        <h3 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'billing.availablePlans' | translate }}</h3>
        <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-5 lg:gap-6">
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
                  <i class="pi pi-check !text-emerald-500 !text-[16px]"></i>
                  {{ plan.maxMessagesPerMonth | number }} {{ 'billing.messagesMonth' | translate }}
                </li>
                <li class="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                  <i class="pi pi-check !text-emerald-500 !text-[16px]"></i>
                  {{ plan.maxWhatsAppAccounts }} {{ 'billing.whatsappAccounts' | translate }}
                </li>
                <li class="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                  <i class="pi pi-check !text-emerald-500 !text-[16px]"></i>
                  {{ plan.trialDays }}-{{ 'billing.dayTrial' | translate }}
                </li>
              </ul>
              @if (isCurrentPlan(plan)) {
                <button pButton [outlined]="true" disabled class="!rounded-xl w-full">{{ 'billing.currentPlan' | translate }}</button>
              } @else {
                <button pButton class="!bg-emerald-600 !text-white !rounded-xl w-full hover:!bg-emerald-700"
                  [disabled]="upgrading()"
                  (click)="upgradePlan(plan)">
                  @if (upgrading() && upgradingPlanId() === plan.subscriptionPlanId) {
                    <p-progressSpinner [style]="{'width':'18px','height':'18px'}" strokeWidth="4" class="!inline-block mr-2" />
                  }
                  {{ 'billing.upgrade' | translate }}
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
  private messageService = inject(MessageService);

  plans = signal<SubscriptionPlan[]>([]);
  currentSub = signal<CompanySubscription | null>(null);
  loading = signal(true);
  upgrading = signal(false);
  upgradingPlanId = signal<number | null>(null);

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
    forkJoin({
      plans: this.api.get<SubscriptionPlan[]>('/subscriptions/plans'),
      currentSub: this.api.get<CompanySubscription>('/subscriptions/current'),
    }).subscribe({
      next: ({ plans, currentSub }) => {
        this.plans.set(plans);
        this.currentSub.set(currentSub);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  upgradePlan(plan: SubscriptionPlan): void {
    this.upgrading.set(true);
    this.upgradingPlanId.set(plan.subscriptionPlanId);
    this.api.post<CompanySubscription>('/subscriptions/change', { subscriptionPlanId: plan.subscriptionPlanId }).subscribe({
      next: (sub) => {
        this.currentSub.set(sub);
        this.upgrading.set(false);
        this.upgradingPlanId.set(null);
        this.messageService.add({severity:'success', summary: 'Plan updated successfully!', life: 3000});
      },
      error: (err) => {
        this.upgrading.set(false);
        this.upgradingPlanId.set(null);
        this.messageService.add({severity:'error', summary: err?.error?.message || 'Failed to update plan', life: 3000});
      },
    });
  }
}
