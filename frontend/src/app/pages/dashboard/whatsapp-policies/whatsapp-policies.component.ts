import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-whatsapp-policies',
  standalone: true,
  imports: [TranslateModule, RouterLink],
  template: `
    <div class="space-y-6 lg:space-y-7 max-w-6xl min-w-0">
      <section class="rounded-2xl border border-slate-200 dark:border-slate-700/60 bg-white dark:bg-slate-900/60 p-6 lg:p-7 overflow-hidden relative">
        <div class="absolute inset-0 pointer-events-none opacity-70"
          style="background: radial-gradient(circle at top right, rgba(16,168,97,0.14), transparent 45%), radial-gradient(circle at bottom left, rgba(14,116,144,0.12), transparent 40%);">
        </div>
        <div class="relative space-y-4">
          <span class="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-100/80 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-300 text-xs font-semibold">
            <i class="pi pi-verified !text-[13px]"></i>
            {{ 'whatsappPolicies.metaBadge' | translate }}
          </span>
          <div>
            <h1 class="text-2xl lg:text-3xl font-bold text-slate-900 dark:text-white">
              {{ 'whatsappPolicies.title' | translate }}
            </h1>
            <p class="mt-2 text-sm text-slate-600 dark:text-slate-300 leading-7">
              {{ 'whatsappPolicies.subtitle' | translate }}
            </p>
          </div>
          <div class="rounded-xl border border-amber-200/80 dark:border-amber-700/50 bg-amber-50/80 dark:bg-amber-900/15 p-4">
            <p class="text-sm text-amber-800 dark:text-amber-200 leading-7">
              {{ 'whatsappPolicies.officialNote' | translate }}
            </p>
            <p class="text-xs text-amber-700/90 dark:text-amber-300/80 mt-2">
              {{ 'whatsappPolicies.lastUpdated' | translate }}
            </p>
          </div>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 dark:border-slate-700/60 bg-white dark:bg-slate-900/60 p-6">
        <div class="mb-4">
          <h2 class="text-lg font-semibold text-slate-900 dark:text-white">
            {{ 'whatsappPolicies.section.core.title' | translate }}
          </h2>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">
            {{ 'whatsappPolicies.section.core.subtitle' | translate }}
          </p>
        </div>

        <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <article class="rounded-xl border border-emerald-200 dark:border-emerald-800/50 bg-emerald-50/60 dark:bg-emerald-900/20 p-4">
            <h3 class="text-sm font-semibold text-emerald-800 dark:text-emerald-300">{{ 'whatsappPolicies.cards.service.title' | translate }}</h3>
            <p class="text-xs text-emerald-700/90 dark:text-emerald-300/90 mt-1 leading-6">{{ 'whatsappPolicies.cards.service.desc' | translate }}</p>
            <ul class="mt-3 space-y-2 text-xs text-emerald-800 dark:text-emerald-200 leading-6">
              <li class="flex items-start gap-2"><i class="pi pi-check-circle mt-1 !text-[11px]"></i><span>{{ 'whatsappPolicies.cards.service.item1' | translate }}</span></li>
              <li class="flex items-start gap-2"><i class="pi pi-check-circle mt-1 !text-[11px]"></i><span>{{ 'whatsappPolicies.cards.service.item2' | translate }}</span></li>
              <li class="flex items-start gap-2"><i class="pi pi-check-circle mt-1 !text-[11px]"></i><span>{{ 'whatsappPolicies.cards.service.item3' | translate }}</span></li>
            </ul>
          </article>

          <article class="rounded-xl border border-cyan-200 dark:border-cyan-800/50 bg-cyan-50/60 dark:bg-cyan-900/20 p-4">
            <h3 class="text-sm font-semibold text-cyan-800 dark:text-cyan-300">{{ 'whatsappPolicies.cards.template.title' | translate }}</h3>
            <p class="text-xs text-cyan-700/90 dark:text-cyan-300/90 mt-1 leading-6">{{ 'whatsappPolicies.cards.template.desc' | translate }}</p>
            <ul class="mt-3 space-y-2 text-xs text-cyan-800 dark:text-cyan-200 leading-6">
              <li class="flex items-start gap-2"><i class="pi pi-check-circle mt-1 !text-[11px]"></i><span>{{ 'whatsappPolicies.cards.template.item1' | translate }}</span></li>
              <li class="flex items-start gap-2"><i class="pi pi-check-circle mt-1 !text-[11px]"></i><span>{{ 'whatsappPolicies.cards.template.item2' | translate }}</span></li>
              <li class="flex items-start gap-2"><i class="pi pi-check-circle mt-1 !text-[11px]"></i><span>{{ 'whatsappPolicies.cards.template.item3' | translate }}</span></li>
            </ul>
          </article>

          <article class="rounded-xl border border-rose-200 dark:border-rose-800/50 bg-rose-50/60 dark:bg-rose-900/20 p-4">
            <h3 class="text-sm font-semibold text-rose-800 dark:text-rose-300">{{ 'whatsappPolicies.cards.prohibited.title' | translate }}</h3>
            <p class="text-xs text-rose-700/90 dark:text-rose-300/90 mt-1 leading-6">{{ 'whatsappPolicies.cards.prohibited.desc' | translate }}</p>
            <ul class="mt-3 space-y-2 text-xs text-rose-800 dark:text-rose-200 leading-6">
              <li class="flex items-start gap-2"><i class="pi pi-times-circle mt-1 !text-[11px]"></i><span>{{ 'whatsappPolicies.cards.prohibited.item1' | translate }}</span></li>
              <li class="flex items-start gap-2"><i class="pi pi-times-circle mt-1 !text-[11px]"></i><span>{{ 'whatsappPolicies.cards.prohibited.item2' | translate }}</span></li>
              <li class="flex items-start gap-2"><i class="pi pi-times-circle mt-1 !text-[11px]"></i><span>{{ 'whatsappPolicies.cards.prohibited.item3' | translate }}</span></li>
            </ul>
          </article>
        </div>
      </section>

      <section class="grid gap-4 lg:grid-cols-2">
        <article class="rounded-2xl border border-slate-200 dark:border-slate-700/60 bg-white dark:bg-slate-900/60 p-5">
          <h2 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'whatsappPolicies.section.window.title' | translate }}</h2>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'whatsappPolicies.section.window.subtitle' | translate }}</p>

          <div class="mt-4 space-y-3">
            <div class="rounded-xl border border-slate-200 dark:border-slate-700/60 bg-slate-50/70 dark:bg-slate-800/40 p-3">
              <h3 class="text-sm font-semibold text-slate-900 dark:text-slate-100">{{ 'whatsappPolicies.window.customerInitiated.title' | translate }}</h3>
              <p class="text-xs text-slate-600 dark:text-slate-300 mt-1 leading-6">{{ 'whatsappPolicies.window.customerInitiated.desc' | translate }}</p>
            </div>
            <div class="rounded-xl border border-slate-200 dark:border-slate-700/60 bg-slate-50/70 dark:bg-slate-800/40 p-3">
              <h3 class="text-sm font-semibold text-slate-900 dark:text-slate-100">{{ 'whatsappPolicies.window.businessInitiated.title' | translate }}</h3>
              <p class="text-xs text-slate-600 dark:text-slate-300 mt-1 leading-6">{{ 'whatsappPolicies.window.businessInitiated.desc' | translate }}</p>
            </div>
          </div>

          <div class="mt-4">
            <div class="text-xs font-semibold text-slate-500 dark:text-slate-400 mb-2">{{ 'whatsappPolicies.window.templateCategories' | translate }}</div>
            <div class="flex flex-wrap gap-2">
              <span class="px-2.5 py-1 rounded-full text-xs bg-indigo-100 dark:bg-indigo-900/30 text-indigo-700 dark:text-indigo-300">{{ 'whatsappPolicies.window.categoryMarketing' | translate }}</span>
              <span class="px-2.5 py-1 rounded-full text-xs bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-300">{{ 'whatsappPolicies.window.categoryUtility' | translate }}</span>
              <span class="px-2.5 py-1 rounded-full text-xs bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300">{{ 'whatsappPolicies.window.categoryAuthentication' | translate }}</span>
            </div>
          </div>
        </article>

        <article class="rounded-2xl border border-slate-200 dark:border-slate-700/60 bg-white dark:bg-slate-900/60 p-5">
          <h2 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'whatsappPolicies.section.optIn.title' | translate }}</h2>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'whatsappPolicies.section.optIn.subtitle' | translate }}</p>
          <ul class="mt-4 space-y-2 text-sm text-slate-700 dark:text-slate-200 leading-7">
            <li class="flex items-start gap-2"><i class="pi pi-check mt-1 !text-[12px] text-emerald-600"></i><span>{{ 'whatsappPolicies.optIn.item1' | translate }}</span></li>
            <li class="flex items-start gap-2"><i class="pi pi-check mt-1 !text-[12px] text-emerald-600"></i><span>{{ 'whatsappPolicies.optIn.item2' | translate }}</span></li>
            <li class="flex items-start gap-2"><i class="pi pi-check mt-1 !text-[12px] text-emerald-600"></i><span>{{ 'whatsappPolicies.optIn.item3' | translate }}</span></li>
            <li class="flex items-start gap-2"><i class="pi pi-check mt-1 !text-[12px] text-emerald-600"></i><span>{{ 'whatsappPolicies.optIn.item4' | translate }}</span></li>
          </ul>
        </article>
      </section>

      <section class="grid gap-4 lg:grid-cols-2">
        <article class="rounded-2xl border border-slate-200 dark:border-slate-700/60 bg-white dark:bg-slate-900/60 p-5">
          <h2 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'whatsappPolicies.section.quality.title' | translate }}</h2>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'whatsappPolicies.section.quality.subtitle' | translate }}</p>
          <ul class="mt-4 space-y-2 text-sm text-slate-700 dark:text-slate-200 leading-7">
            <li class="flex items-start gap-2"><i class="pi pi-chart-line mt-1 !text-[12px] text-cyan-600"></i><span>{{ 'whatsappPolicies.quality.item1' | translate }}</span></li>
            <li class="flex items-start gap-2"><i class="pi pi-pause-circle mt-1 !text-[12px] text-cyan-600"></i><span>{{ 'whatsappPolicies.quality.item2' | translate }}</span></li>
            <li class="flex items-start gap-2"><i class="pi pi-sync mt-1 !text-[12px] text-cyan-600"></i><span>{{ 'whatsappPolicies.quality.item3' | translate }}</span></li>
          </ul>
        </article>

        <article class="rounded-2xl border border-slate-200 dark:border-slate-700/60 bg-white dark:bg-slate-900/60 p-5">
          <h2 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'whatsappPolicies.section.examples.title' | translate }}</h2>

          <div class="mt-4 grid gap-3 sm:grid-cols-2">
            <div class="rounded-xl border border-emerald-200 dark:border-emerald-800/50 bg-emerald-50/60 dark:bg-emerald-900/20 p-3">
              <h3 class="text-xs font-semibold text-emerald-800 dark:text-emerald-300 mb-2">{{ 'whatsappPolicies.examples.safeTitle' | translate }}</h3>
              <p class="text-xs text-emerald-800 dark:text-emerald-200 leading-6">{{ 'whatsappPolicies.examples.safe1' | translate }}</p>
              <p class="text-xs text-emerald-800 dark:text-emerald-200 leading-6 mt-2">{{ 'whatsappPolicies.examples.safe2' | translate }}</p>
            </div>
            <div class="rounded-xl border border-rose-200 dark:border-rose-800/50 bg-rose-50/60 dark:bg-rose-900/20 p-3">
              <h3 class="text-xs font-semibold text-rose-800 dark:text-rose-300 mb-2">{{ 'whatsappPolicies.examples.unsafeTitle' | translate }}</h3>
              <p class="text-xs text-rose-800 dark:text-rose-200 leading-6">{{ 'whatsappPolicies.examples.unsafe1' | translate }}</p>
              <p class="text-xs text-rose-800 dark:text-rose-200 leading-6 mt-2">{{ 'whatsappPolicies.examples.unsafe2' | translate }}</p>
            </div>
          </div>
        </article>
      </section>

      <section class="rounded-2xl border border-slate-200 dark:border-slate-700/60 bg-white dark:bg-slate-900/60 p-5">
        <div class="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div>
            <h2 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'whatsappPolicies.resources.title' | translate }}</h2>
            <div class="mt-2 flex flex-wrap gap-2">
              <a [href]="metaDocsUrl" target="_blank" rel="noopener" class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-slate-200 dark:hover:bg-slate-700 no-underline">
                <i class="pi pi-external-link !text-[11px]"></i> {{ 'whatsappPolicies.resources.docs' | translate }}
              </a>
              <a [href]="metaBestPracticesUrl" target="_blank" rel="noopener" class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-slate-200 dark:hover:bg-slate-700 no-underline">
                <i class="pi pi-external-link !text-[11px]"></i> {{ 'whatsappPolicies.resources.bestPractices' | translate }}
              </a>
              <a [href]="metaTemplatesUrl" target="_blank" rel="noopener" class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-slate-200 dark:hover:bg-slate-700 no-underline">
                <i class="pi pi-external-link !text-[11px]"></i> {{ 'whatsappPolicies.resources.templates' | translate }}
              </a>
            </div>
          </div>

          <div class="flex flex-wrap gap-2">
            <a routerLink="/dashboard/templates" class="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-sm no-underline">
              <i class="pi pi-file-edit !text-[13px]"></i> {{ 'whatsappPolicies.action.viewTemplates' | translate }}
            </a>
            <a routerLink="/dashboard/settings" class="inline-flex items-center gap-2 px-4 py-2 rounded-xl border border-slate-300 dark:border-slate-600 text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 text-sm no-underline">
              <i class="pi pi-cog !text-[13px]"></i> {{ 'whatsappPolicies.action.openSettings' | translate }}
            </a>
          </div>
        </div>
      </section>
    </div>
  `,
})
export class WhatsappPoliciesComponent {
  readonly metaDocsUrl = 'https://developers.facebook.com/docs/whatsapp';
  readonly metaBestPracticesUrl = 'https://developers.facebook.com/docs/whatsapp/cloud-api/guides/send-messages';
  readonly metaTemplatesUrl = 'https://developers.facebook.com/docs/whatsapp/message-templates/guidelines';
}
