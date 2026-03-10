import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, ButtonModule, TranslateModule],
  template: `
    <!-- Hero Section -->
    <section class="relative min-h-screen flex items-center overflow-hidden">
      <!-- Background -->
      <div class="absolute inset-0 bg-gradient-to-br from-slate-50 via-emerald-50/40 to-green-50/30 dark:from-slate-950 dark:via-emerald-950/20 dark:to-slate-950"></div>
      <div class="absolute top-20 right-20 w-96 h-96 bg-emerald-400/10 rounded-full blur-3xl"></div>
      <div class="absolute bottom-20 left-20 w-80 h-80 bg-green-400/10 rounded-full blur-3xl"></div>

      <div class="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-32">
        <div class="grid lg:grid-cols-2 gap-12 items-center">
          <!-- Left -->
          <div class="space-y-8">
            <div class="inline-flex items-center gap-2 px-4 py-1.5 bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400 rounded-full text-sm font-medium">
              <span class="w-2 h-2 bg-emerald-500 rounded-full animate-pulse"></span>
              Now Available — WhatsApp Cloud API v18.0
            </div>

            <h1 class="text-5xl sm:text-6xl lg:text-7xl font-bold leading-tight tracking-tight">
              <span class="text-slate-900 dark:text-white">{{ 'landing.home.heroTitle1' | translate }}</span><br>
              <span class="bg-gradient-to-r from-emerald-600 to-green-500 bg-clip-text text-transparent">{{ 'landing.home.heroTitle2' | translate }}</span><br>
              <span class="text-slate-900 dark:text-white">{{ 'landing.home.heroTitle3' | translate }}</span>
            </h1>

            <p class="text-lg text-slate-600 dark:text-slate-400 max-w-lg leading-relaxed">
              {{ 'landing.home.heroDescription' | translate }}
            </p>

            <div class="flex flex-wrap gap-4">
              <a routerLink="/register" pButton class="!bg-emerald-600 !text-white !rounded-xl !px-8 !py-6 !text-base hover:!bg-emerald-700 !shadow-lg !shadow-emerald-600/25 !no-underline">
                {{ 'landing.home.startFreeTrial' | translate }}
                <i class="pi pi-arrow-right ml-2"></i>
              </a>
              <a routerLink="/pricing" pButton [outlined]="true" class="!rounded-xl !px-8 !py-6 !text-base !border-slate-300 dark:!border-slate-600 !text-slate-700 dark:!text-slate-300 !no-underline">
                {{ 'landing.home.viewPricing' | translate }}
              </a>
            </div>

            <div class="flex items-center gap-8 pt-4 text-sm text-slate-500 dark:text-slate-400">
              <div class="flex items-center gap-2">
                <i class="pi pi-check-circle !text-emerald-500 !text-[18px]"></i>
                14-day free trial
              </div>
              <div class="flex items-center gap-2">
                <i class="pi pi-check-circle !text-emerald-500 !text-[18px]"></i>
                No credit card
              </div>
              <div class="flex items-center gap-2">
                <i class="pi pi-check-circle !text-emerald-500 !text-[18px]"></i>
                Cancel anytime
              </div>
            </div>
          </div>

          <!-- Right — Code Preview -->
          <div class="hidden lg:block">
            <div class="bg-slate-900 rounded-2xl shadow-2xl shadow-slate-900/50 overflow-hidden border border-slate-700/50">
              <div class="flex items-center gap-2 px-4 py-3 border-b border-slate-700/50">
                <div class="w-3 h-3 rounded-full bg-red-500/80"></div>
                <div class="w-3 h-3 rounded-full bg-yellow-500/80"></div>
                <div class="w-3 h-3 rounded-full bg-green-500/80"></div>
                <span class="text-xs text-slate-500 ml-2">send-message.sh</span>
              </div>
              <pre class="p-6 text-sm leading-relaxed overflow-x-auto"><code class="text-slate-300"><span class="text-emerald-400">curl</span> <span class="text-yellow-300">-X POST</span> \\
  <span class="text-sky-400">"https://api.botglobalservices.com/whatsapp/messages/text"</span> \
  -H <span class="text-orange-300">"Authorization: Bearer YOUR_API_KEY"</span> \
  -H <span class="text-orange-300">"Content-Type: application/json"</span> \
  -d <span class="text-green-300">'{{'{'}}
    "to": "+201110446331",
    "body": "Hello from BotGlobal Services! 🚀"
  {{'}'}}'</span>

<span class="text-slate-500">// Response</span>
{{'{'}}
  <span class="text-sky-300">"success"</span>: <span class="text-emerald-400">true</span>,
  <span class="text-sky-300">"messageId"</span>: <span class="text-orange-300">"wamid.HBgL..."</span>,
  <span class="text-sky-300">"status"</span>: <span class="text-orange-300">"queued"</span>
{{'}'}}</code></pre>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Features Section -->
    <section class="py-24 bg-white dark:bg-slate-900">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="text-center mb-16">
          <h2 class="text-3xl sm:text-4xl font-bold text-slate-900 dark:text-white mb-4">
            Everything you need to build with WhatsApp
          </h2>
          <p class="text-lg text-slate-600 dark:text-slate-400 max-w-2xl mx-auto">
            A complete platform for WhatsApp Cloud API integration — from message sending to webhook handling.
          </p>
        </div>

        <div class="grid md:grid-cols-2 lg:grid-cols-3 gap-8">
          @for (feature of features; track feature.titleKey) {
            <div class="group p-8 rounded-2xl border border-slate-200 dark:border-slate-700/50 bg-white dark:bg-slate-800/50 hover:shadow-xl hover:shadow-emerald-500/5 hover:border-emerald-200 dark:hover:border-emerald-800/50 transition-all duration-300">
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-500 to-green-600 flex items-center justify-center mb-5 shadow-lg shadow-emerald-500/20 group-hover:scale-110 transition-transform">
                <i [class]="'pi ' + feature.icon + ' text-white !text-[22px]'"></i>
              </div>
              <h3 class="text-lg font-semibold text-slate-900 dark:text-white mb-2">{{ feature.titleKey | translate }}</h3>
              <p class="text-sm text-slate-600 dark:text-slate-400 leading-relaxed">{{ feature.descKey | translate }}</p>
            </div>
          }
        </div>
      </div>
    </section>

    <!-- API Capabilities -->
    <section class="py-24 bg-slate-50 dark:bg-slate-950">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="grid lg:grid-cols-2 gap-16 items-center">
          <div class="space-y-8">
            <h2 class="text-3xl sm:text-4xl font-bold text-slate-900 dark:text-white">
              Built for developers,<br>designed for scale
            </h2>
            <div class="space-y-6">
              @for (cap of capabilities; track cap.titleKey) {
                <div class="flex gap-4">
                  <div class="shrink-0 w-10 h-10 rounded-lg bg-emerald-100 dark:bg-emerald-900/40 flex items-center justify-center">
                    <i [class]="'pi ' + cap.icon + ' !text-emerald-600 dark:!text-emerald-400 !text-[20px]'"></i>
                  </div>
                  <div>
                    <h4 class="font-semibold text-slate-900 dark:text-white mb-1">{{ cap.titleKey | translate }}</h4>
                    <p class="text-sm text-slate-600 dark:text-slate-400">{{ cap.descKey | translate }}</p>
                  </div>
                </div>
              }
            </div>
          </div>

          <div class="bg-white dark:bg-slate-800/50 rounded-2xl p-8 border border-slate-200 dark:border-slate-700/50 shadow-xl">
            <div class="grid grid-cols-2 gap-6">
              @for (stat of stats; track stat.labelKey) {
                <div class="text-center p-4">
                  <div class="text-3xl font-bold text-emerald-600 mb-1">{{ stat.value }}</div>
                  <div class="text-xs text-slate-500 dark:text-slate-400 uppercase tracking-wider">{{ stat.labelKey | translate }}</div>
                </div>
              }
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- CTA Section -->
    <section class="py-24 bg-gradient-to-br from-emerald-600 to-green-700">
      <div class="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
        <h2 class="text-3xl sm:text-4xl font-bold text-white mb-6">
          Ready to build with WhatsApp?
        </h2>
        <p class="text-lg text-emerald-100 mb-10 max-w-2xl mx-auto">
          Join thousands of developers building powerful messaging experiences.
          Start your free 14-day trial today.
        </p>
        <div class="flex flex-wrap justify-center gap-4">
          <a routerLink="/register" pButton class="!bg-white !text-emerald-700 !rounded-xl !px-8 !py-6 !text-base font-semibold hover:!bg-emerald-50 !shadow-lg !no-underline">
            {{ 'landing.home.startFreeTrial' | translate }}
            <i class="pi pi-send ml-2"></i>
          </a>
          <a routerLink="/contact" pButton [outlined]="true" class="!rounded-xl !px-8 !py-6 !text-base !border-emerald-300 !text-white hover:!bg-emerald-500/20 !no-underline">
            {{ 'landing.home.contactSales' | translate }}
          </a>
        </div>
      </div>
    </section>
  `,
})
export class HomeComponent {
  private t = inject(TranslateService);

  features = [
    { icon: 'pi-send', titleKey: 'landing.home.messageSending', descKey: 'landing.home.messageSendingDesc' },
    { icon: 'pi-link', titleKey: 'landing.home.webhookHandling', descKey: 'landing.home.webhookHandlingDesc' },
    { icon: 'pi-file', titleKey: 'landing.home.templateManagement', descKey: 'landing.home.templateManagementDesc' },
    { icon: 'pi-images', titleKey: 'landing.home.mediaSupport', descKey: 'landing.home.mediaSupportDesc' },
    { icon: 'pi-gauge', titleKey: 'landing.home.queueRetry', descKey: 'landing.home.queueRetryDesc' },
    { icon: 'pi-shield', titleKey: 'landing.home.security', descKey: 'landing.home.securityDesc' },
  ];

  capabilities = [
    { icon: 'pi-code', titleKey: 'landing.home.restApi', descKey: 'landing.home.restApiDesc' },
    { icon: 'pi-users', titleKey: 'landing.home.multiTenant', descKey: 'landing.home.multiTenantDesc' },
    { icon: 'pi-chart-line', titleKey: 'landing.home.autoScaling', descKey: 'landing.home.autoScalingDesc' },
    { icon: 'pi-sitemap', titleKey: 'landing.home.graphProxy', descKey: 'landing.home.graphProxyDesc' },
  ];

  stats = [
    { value: '50+', labelKey: 'landing.home.statEndpoints' },
    { value: '99.9%', labelKey: 'landing.home.statUptime' },
    { value: '<100ms', labelKey: 'landing.home.statLatency' },
    { value: '10M+', labelKey: 'landing.home.statMessages' },
  ];
}
