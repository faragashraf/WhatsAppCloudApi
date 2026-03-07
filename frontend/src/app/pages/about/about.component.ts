import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-about',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <section class="min-h-screen bg-gradient-to-b from-slate-50 to-white dark:from-slate-950 dark:to-slate-900 pt-32 pb-24">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <!-- Hero -->
        <div class="text-center mb-20">
          <h1 class="text-4xl sm:text-5xl font-bold text-slate-900 dark:text-white mb-6">
            Powering the future of<br>
            <span class="bg-gradient-to-r from-emerald-600 to-green-500 bg-clip-text text-transparent">business messaging</span>
          </h1>
          <p class="text-lg text-slate-600 dark:text-slate-400 max-w-3xl mx-auto leading-relaxed">
            WaCloud was built by developers, for developers. Our mission is to make WhatsApp Cloud API
            integration effortless — so you can focus on building amazing customer experiences.
          </p>
        </div>

        <!-- Mission Cards -->
        <div class="grid md:grid-cols-3 gap-8 mb-20">
          @for (card of values; track card.title) {
            <div class="p-8 rounded-2xl bg-white dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700/50 shadow-sm hover:shadow-lg transition-shadow">
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-500 to-green-600 flex items-center justify-center mb-5 shadow-lg shadow-emerald-500/20">
                <mat-icon class="text-white">{{ card.icon }}</mat-icon>
              </div>
              <h3 class="text-lg font-semibold text-slate-900 dark:text-white mb-3">{{ card.title }}</h3>
              <p class="text-sm text-slate-600 dark:text-slate-400 leading-relaxed">{{ card.description }}</p>
            </div>
          }
        </div>

        <!-- Story -->
        <div class="max-w-3xl mx-auto">
          <div class="rounded-2xl bg-gradient-to-br from-emerald-600 to-green-700 p-10 text-white">
            <h2 class="text-2xl font-bold mb-4">Our Story</h2>
            <p class="text-emerald-100 leading-relaxed mb-4">
              We started WaCloud because we saw how complex it was for businesses to integrate with the
              WhatsApp Cloud API. Between managing authentication, handling webhooks, building message queues,
              and maintaining multi-tenant isolation — developers were spending months on infrastructure
              instead of building features.
            </p>
            <p class="text-emerald-100 leading-relaxed">
              WaCloud abstracts all that complexity into a clean, developer-friendly API. Register, connect your
              WhatsApp Business account, and start sending messages in minutes — not months. We handle the
              infrastructure so you can build what matters.
            </p>
          </div>
        </div>

        <!-- Stats -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-8 mt-20">
          @for (stat of aboutStats; track stat.label) {
            <div class="text-center">
              <div class="text-3xl font-bold text-emerald-600 dark:text-emerald-400 mb-1">{{ stat.value }}</div>
              <div class="text-sm text-slate-500 dark:text-slate-400">{{ stat.label }}</div>
            </div>
          }
        </div>
      </div>
    </section>
  `,
})
export class AboutComponent {
  values = [
    { icon: 'code', title: 'Developer First', description: 'Built with developers in mind. Clean APIs, great docs, and SDKs for every major language.' },
    { icon: 'shield', title: 'Enterprise Security', description: 'SOC 2 compliant infrastructure with end-to-end encryption, JWT auth, and tenant isolation.' },
    { icon: 'speed', title: 'Reliability at Scale', description: 'Message queue with retry logic, health monitoring, and 99.9% uptime guarantee.' },
  ];

  aboutStats = [
    { value: '500+', label: 'Businesses Served' },
    { value: '10M+', label: 'Messages Delivered' },
    { value: '99.9%', label: 'Platform Uptime' },
    { value: '24/7', label: 'Support Available' },
  ];
}
