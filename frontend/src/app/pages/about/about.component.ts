import { Component, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-about',
  standalone: true,
  imports: [NgClass, TranslateModule],
  template: `
    <section class="min-h-screen bg-gradient-to-b from-slate-50 to-white dark:from-slate-950 dark:to-slate-900 pt-32 pb-24">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <!-- Hero -->
        <div class="text-center mb-20">
          <h1 class="text-4xl sm:text-5xl font-bold text-slate-900 dark:text-white mb-6">
            {{ 'landing.about.title1' | translate }}<br>
            <span class="bg-gradient-to-r from-emerald-600 to-green-500 bg-clip-text text-transparent">{{ 'landing.about.title2' | translate }}</span>
          </h1>
          <p class="text-lg text-slate-600 dark:text-slate-400 max-w-3xl mx-auto leading-relaxed">
            {{ 'landing.about.description' | translate }}
          </p>
        </div>

        <!-- Mission Cards -->
        <div class="grid md:grid-cols-3 gap-8 mb-20">
          @for (card of values; track card.titleKey) {
            <div class="p-8 rounded-2xl bg-white dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700/50 shadow-sm hover:shadow-lg transition-shadow">
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-500 to-green-600 flex items-center justify-center mb-5 shadow-lg shadow-emerald-500/20">
                <i class="pi text-white" [ngClass]="'pi-' + card.icon"></i>
              </div>
              <h3 class="text-lg font-semibold text-slate-900 dark:text-white mb-3">{{ card.titleKey | translate }}</h3>
              <p class="text-sm text-slate-600 dark:text-slate-400 leading-relaxed">{{ card.descKey | translate }}</p>
            </div>
          }
        </div>

        <!-- Story -->
        <div class="max-w-3xl mx-auto">
          <div class="rounded-2xl bg-gradient-to-br from-emerald-600 to-green-700 p-10 text-white">
            <h2 class="text-2xl font-bold mb-4">{{ 'landing.about.storyTitle' | translate }}</h2>
            <p class="text-emerald-100 leading-relaxed mb-4">
              {{ 'landing.about.storyP1' | translate }}
            </p>
            <p class="text-emerald-100 leading-relaxed">
              {{ 'landing.about.storyP2' | translate }}
            </p>
          </div>
        </div>

        <!-- Stats -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-8 mt-20">
          @for (stat of aboutStats; track stat.labelKey) {
            <div class="text-center">
              <div class="text-3xl font-bold text-emerald-600 dark:text-emerald-400 mb-1">{{ stat.value }}</div>
              <div class="text-sm text-slate-500 dark:text-slate-400">{{ stat.labelKey | translate }}</div>
            </div>
          }
        </div>
      </div>
    </section>
  `,
})
export class AboutComponent {
  private t = inject(TranslateService);

  values = [
    { icon: 'code', titleKey: 'landing.about.devFirst', descKey: 'landing.about.devFirstDesc' },
    { icon: 'shield', titleKey: 'landing.about.security', descKey: 'landing.about.securityDesc' },
    { icon: 'gauge', titleKey: 'landing.about.reliability', descKey: 'landing.about.reliabilityDesc' },
  ];

  aboutStats = [
    { value: '500+', labelKey: 'landing.about.statBusinesses' },
    { value: '10M+', labelKey: 'landing.about.statMessagesDelivered' },
    { value: '99.9%', labelKey: 'landing.about.statUptime' },
    { value: '24/7', labelKey: 'landing.about.statSupport' },
  ];
}
