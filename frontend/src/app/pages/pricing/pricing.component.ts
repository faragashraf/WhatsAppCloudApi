import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

interface PricingPlan {
  nameKey: string;
  price: number;
  yearlyPrice: number;
  descKey: string;
  featureKeys: string[];
  ctaKey: string;
  highlighted: boolean;
  badgeKey?: string;
}

@Component({
  selector: 'app-pricing',
  standalone: true,
  imports: [RouterLink, TranslateModule],
  template: `
    <section class="min-h-screen bg-gradient-to-b from-slate-50 to-white dark:from-slate-950 dark:to-slate-900 pt-24 sm:pt-28 lg:pt-32 pb-16 sm:pb-20 lg:pb-24">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <!-- Header -->
        <div class="text-center mb-16">
          <div class="inline-flex items-center gap-2 px-4 py-1.5 bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400 rounded-full text-sm font-medium mb-6">
            Simple, transparent pricing
          </div>
          <h1 class="text-4xl sm:text-5xl font-bold text-slate-900 dark:text-white mb-4">
            {{ 'landing.pricing.headline' | translate }}
          </h1>
          <p class="text-lg text-slate-600 dark:text-slate-400 max-w-2xl mx-auto">
            {{ 'landing.pricing.subtitle' | translate }}
          </p>

          <!-- Toggle -->
          <div class="mt-8 inline-flex flex-wrap items-center justify-center gap-2 p-1 bg-slate-100 dark:bg-slate-800 rounded-xl">
            <button
              (click)="yearly.set(false)"
              class="px-5 py-2 rounded-lg text-sm font-medium transition-all cursor-pointer border-none"
              [class]="!yearly() ? 'bg-white dark:bg-slate-700 text-slate-900 dark:text-white shadow-sm' : 'text-slate-500 dark:text-slate-400 bg-transparent'">
              {{ 'landing.pricing.monthly' | translate }}
            </button>
            <button
              (click)="yearly.set(true)"
              class="px-5 py-2 rounded-lg text-sm font-medium transition-all cursor-pointer border-none"
              [class]="yearly() ? 'bg-white dark:bg-slate-700 text-slate-900 dark:text-white shadow-sm' : 'text-slate-500 dark:text-slate-400 bg-transparent'">
              {{ 'landing.pricing.yearly' | translate }}
              <span class="ml-1 text-xs text-emerald-600 font-semibold">{{ 'landing.pricing.yearlyDiscount' | translate }}</span>
            </button>
          </div>
        </div>

        <!-- Plans -->
        <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-6 lg:gap-8 max-w-6xl mx-auto">
          @for (plan of plans; track plan.nameKey) {
            <div
              class="relative rounded-2xl border p-6 sm:p-7 lg:p-8 transition-all duration-300 hover:shadow-xl"
              [class]="plan.highlighted
                ? 'bg-gradient-to-b from-emerald-600 to-green-700 border-emerald-500 text-white shadow-xl shadow-emerald-600/20 lg:scale-105'
                : 'bg-white dark:bg-slate-800/50 border-slate-200 dark:border-slate-700/50 hover:border-emerald-300 dark:hover:border-emerald-700'">

              @if (plan.badgeKey) {
                <div class="absolute -top-3 left-1/2 -translate-x-1/2 px-4 py-1 bg-emerald-500 text-white text-xs font-bold rounded-full uppercase tracking-wider shadow-lg">
                  {{ plan.badgeKey | translate }}
                </div>
              }

              <h3 class="text-xl font-bold mb-2" [class]="plan.highlighted ? 'text-white' : 'text-slate-900 dark:text-white'">
                {{ plan.nameKey | translate }}
              </h3>
              <p class="text-sm mb-6" [class]="plan.highlighted ? 'text-emerald-100' : 'text-slate-500 dark:text-slate-400'">
                {{ plan.descKey | translate }}
              </p>

              <div class="mb-6">
                <span class="text-4xl font-bold" [class]="plan.highlighted ? 'text-white' : 'text-slate-900 dark:text-white'">
                  \${{ yearly() ? plan.yearlyPrice : plan.price }}
                </span>
                <span class="text-sm ml-1" [class]="plan.highlighted ? 'text-emerald-200' : 'text-slate-500'">{{ 'landing.pricing.perMonth' | translate }}</span>
              </div>

              <a routerLink="/register"
                class="block text-center py-3 px-6 rounded-xl font-semibold text-sm transition-all no-underline"
                [class]="plan.highlighted
                  ? 'bg-white text-emerald-700 hover:bg-emerald-50 shadow-lg'
                  : 'bg-emerald-600 text-white hover:bg-emerald-700 shadow-md shadow-emerald-600/20'">
                {{ plan.ctaKey | translate }}
              </a>

              <ul class="mt-8 space-y-3 list-none p-0 m-0">
                @for (fk of plan.featureKeys; track fk) {
                  <li class="flex items-start gap-3 text-sm">
                    <i [class]="'pi pi-check-circle !text-[18px] shrink-0 mt-0.5 ' + (plan.highlighted ? '!text-emerald-200' : '!text-emerald-500')"></i>
                    <span [class]="plan.highlighted ? 'text-emerald-50' : 'text-slate-600 dark:text-slate-400'">{{ fk | translate }}</span>
                  </li>
                }
              </ul>
            </div>
          }
        </div>
      </div>
    </section>
  `,
})
export class PricingComponent {
  private t = inject(TranslateService);
  yearly = signal(false);

  plans: PricingPlan[] = [
    {
      nameKey: 'landing.pricing.starter',
      price: 0,
      yearlyPrice: 0,
      descKey: 'landing.pricing.starterDesc',
      ctaKey: 'landing.pricing.startFreeTrial',
      highlighted: false,
      featureKeys: [
        'landing.pricing.messages1k', 'landing.pricing.wa1', 'landing.pricing.phone1',
        'landing.pricing.queue', 'landing.pricing.webhook', 'landing.pricing.trial14', 'landing.pricing.community',
      ],
    },
    {
      nameKey: 'landing.pricing.pro',
      price: 49,
      yearlyPrice: 39,
      descKey: 'landing.pricing.proDesc',
      ctaKey: 'landing.pricing.startFreeTrial',
      highlighted: true,
      badgeKey: 'landing.pricing.proBadge',
      featureKeys: [
        'landing.pricing.messages25k', 'landing.pricing.wa5', 'landing.pricing.unlimitedPhones',
        'landing.pricing.priorityQueue', 'landing.pricing.templateMgmt', 'landing.pricing.analytics',
        'landing.pricing.emailSupport', 'landing.pricing.apiRate80',
      ],
    },
    {
      nameKey: 'landing.pricing.enterprise',
      price: 199,
      yearlyPrice: 159,
      descKey: 'landing.pricing.enterpriseDesc',
      ctaKey: 'landing.pricing.contactSales',
      highlighted: false,
      featureKeys: [
        'landing.pricing.unlimitedMsg', 'landing.pricing.unlimitedWa', 'landing.pricing.unlimitedPhones',
        'landing.pricing.dedicated', 'landing.pricing.customWebhooks', 'landing.pricing.sla',
        'landing.pricing.prioritySupport', 'landing.pricing.customIntegrations',
      ],
    },
  ];
}
