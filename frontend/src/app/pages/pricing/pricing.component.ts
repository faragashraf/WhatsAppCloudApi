import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonToggleModule } from '@angular/material/button-toggle';

interface PricingPlan {
  name: string;
  price: number;
  yearlyPrice: number;
  description: string;
  features: string[];
  cta: string;
  highlighted: boolean;
  badge?: string;
}

@Component({
  selector: 'app-pricing',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule, MatButtonToggleModule],
  template: `
    <section class="min-h-screen bg-gradient-to-b from-slate-50 to-white dark:from-slate-950 dark:to-slate-900 pt-32 pb-24">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <!-- Header -->
        <div class="text-center mb-16">
          <div class="inline-flex items-center gap-2 px-4 py-1.5 bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400 rounded-full text-sm font-medium mb-6">
            Simple, transparent pricing
          </div>
          <h1 class="text-4xl sm:text-5xl font-bold text-slate-900 dark:text-white mb-4">
            Choose your plan
          </h1>
          <p class="text-lg text-slate-600 dark:text-slate-400 max-w-2xl mx-auto">
            Start free and scale as you grow. All plans include a 14-day free trial.
          </p>

          <!-- Toggle -->
          <div class="mt-8 inline-flex items-center gap-3 p-1 bg-slate-100 dark:bg-slate-800 rounded-xl">
            <button
              (click)="yearly.set(false)"
              class="px-5 py-2 rounded-lg text-sm font-medium transition-all cursor-pointer border-none"
              [class]="!yearly() ? 'bg-white dark:bg-slate-700 text-slate-900 dark:text-white shadow-sm' : 'text-slate-500 dark:text-slate-400 bg-transparent'">
              Monthly
            </button>
            <button
              (click)="yearly.set(true)"
              class="px-5 py-2 rounded-lg text-sm font-medium transition-all cursor-pointer border-none"
              [class]="yearly() ? 'bg-white dark:bg-slate-700 text-slate-900 dark:text-white shadow-sm' : 'text-slate-500 dark:text-slate-400 bg-transparent'">
              Yearly
              <span class="ml-1 text-xs text-emerald-600 font-semibold">-20%</span>
            </button>
          </div>
        </div>

        <!-- Plans -->
        <div class="grid md:grid-cols-3 gap-8 max-w-5xl mx-auto">
          @for (plan of plans; track plan.name) {
            <div
              class="relative rounded-2xl border p-8 transition-all duration-300 hover:shadow-xl"
              [class]="plan.highlighted
                ? 'bg-gradient-to-b from-emerald-600 to-green-700 border-emerald-500 text-white shadow-xl shadow-emerald-600/20 scale-105'
                : 'bg-white dark:bg-slate-800/50 border-slate-200 dark:border-slate-700/50 hover:border-emerald-300 dark:hover:border-emerald-700'">

              @if (plan.badge) {
                <div class="absolute -top-3 left-1/2 -translate-x-1/2 px-4 py-1 bg-emerald-500 text-white text-xs font-bold rounded-full uppercase tracking-wider shadow-lg">
                  {{ plan.badge }}
                </div>
              }

              <h3 class="text-xl font-bold mb-2" [class]="plan.highlighted ? 'text-white' : 'text-slate-900 dark:text-white'">
                {{ plan.name }}
              </h3>
              <p class="text-sm mb-6" [class]="plan.highlighted ? 'text-emerald-100' : 'text-slate-500 dark:text-slate-400'">
                {{ plan.description }}
              </p>

              <div class="mb-6">
                <span class="text-4xl font-bold" [class]="plan.highlighted ? 'text-white' : 'text-slate-900 dark:text-white'">
                  \${{ yearly() ? plan.yearlyPrice : plan.price }}
                </span>
                <span class="text-sm ml-1" [class]="plan.highlighted ? 'text-emerald-200' : 'text-slate-500'">/mo</span>
              </div>

              <a routerLink="/register"
                class="block text-center py-3 px-6 rounded-xl font-semibold text-sm transition-all no-underline"
                [class]="plan.highlighted
                  ? 'bg-white text-emerald-700 hover:bg-emerald-50 shadow-lg'
                  : 'bg-emerald-600 text-white hover:bg-emerald-700 shadow-md shadow-emerald-600/20'">
                {{ plan.cta }}
              </a>

              <ul class="mt-8 space-y-3 list-none p-0 m-0">
                @for (feature of plan.features; track feature) {
                  <li class="flex items-start gap-3 text-sm">
                    <mat-icon class="!text-[18px] shrink-0 mt-0.5" [class]="plan.highlighted ? '!text-emerald-200' : '!text-emerald-500'">check_circle</mat-icon>
                    <span [class]="plan.highlighted ? 'text-emerald-50' : 'text-slate-600 dark:text-slate-400'">{{ feature }}</span>
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
  yearly = signal(false);

  plans: PricingPlan[] = [
    {
      name: 'Starter',
      price: 0,
      yearlyPrice: 0,
      description: 'Perfect for exploring the WhatsApp API',
      cta: 'Start Free Trial',
      highlighted: false,
      features: [
        '1,000 messages/month',
        '1 WhatsApp account',
        '1 phone number',
        'Message queue & retry',
        'Webhook handling',
        '14-day free trial',
        'Community support',
      ],
    },
    {
      name: 'Pro',
      price: 49,
      yearlyPrice: 39,
      description: 'For growing businesses and teams',
      cta: 'Start Free Trial',
      highlighted: true,
      badge: 'Most Popular',
      features: [
        '25,000 messages/month',
        '5 WhatsApp accounts',
        'Unlimited phone numbers',
        'Priority message queue',
        'Template management',
        'Analytics & reporting',
        'Email support',
        'API rate: 80 req/sec',
      ],
    },
    {
      name: 'Enterprise',
      price: 199,
      yearlyPrice: 159,
      description: 'For large-scale messaging operations',
      cta: 'Contact Sales',
      highlighted: false,
      features: [
        'Unlimited messages',
        'Unlimited WhatsApp accounts',
        'Unlimited phone numbers',
        'Dedicated infrastructure',
        'Custom webhooks',
        'SLA guarantee 99.99%',
        '24/7 priority support',
        'Custom integrations',
      ],
    },
  ];
}
