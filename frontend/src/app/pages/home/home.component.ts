import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
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
              <span class="text-slate-900 dark:text-white">The Developer</span><br>
              <span class="bg-gradient-to-r from-emerald-600 to-green-500 bg-clip-text text-transparent">WhatsApp API</span><br>
              <span class="text-slate-900 dark:text-white">Platform</span>
            </h1>

            <p class="text-lg text-slate-600 dark:text-slate-400 max-w-lg leading-relaxed">
              Build powerful WhatsApp messaging experiences. Send messages, manage templates,
              handle webhooks, and scale your business — all through a single API.
            </p>

            <div class="flex flex-wrap gap-4">
              <a routerLink="/register" mat-flat-button class="!bg-emerald-600 !text-white !rounded-xl !px-8 !py-6 !text-base hover:!bg-emerald-700 !shadow-lg !shadow-emerald-600/25 !no-underline">
                Start Free Trial
                <mat-icon class="ml-2">arrow_forward</mat-icon>
              </a>
              <a routerLink="/pricing" mat-stroked-button class="!rounded-xl !px-8 !py-6 !text-base !border-slate-300 dark:!border-slate-600 !text-slate-700 dark:!text-slate-300 !no-underline">
                View Pricing
              </a>
            </div>

            <div class="flex items-center gap-8 pt-4 text-sm text-slate-500 dark:text-slate-400">
              <div class="flex items-center gap-2">
                <mat-icon class="!text-emerald-500 !text-[18px]">check_circle</mat-icon>
                14-day free trial
              </div>
              <div class="flex items-center gap-2">
                <mat-icon class="!text-emerald-500 !text-[18px]">check_circle</mat-icon>
                No credit card
              </div>
              <div class="flex items-center gap-2">
                <mat-icon class="!text-emerald-500 !text-[18px]">check_circle</mat-icon>
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
  <span class="text-sky-400">"https://api.wacloud.dev/whatsapp/messages/text"</span> \\
  -H <span class="text-orange-300">"Authorization: Bearer YOUR_API_KEY"</span> \\
  -H <span class="text-orange-300">"Content-Type: application/json"</span> \\
  -d <span class="text-green-300">'{{'{'}}
    "to": "+1234567890",
    "body": "Hello from WaCloud! 🚀"
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
          @for (feature of features; track feature.title) {
            <div class="group p-8 rounded-2xl border border-slate-200 dark:border-slate-700/50 bg-white dark:bg-slate-800/50 hover:shadow-xl hover:shadow-emerald-500/5 hover:border-emerald-200 dark:hover:border-emerald-800/50 transition-all duration-300">
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-500 to-green-600 flex items-center justify-center mb-5 shadow-lg shadow-emerald-500/20 group-hover:scale-110 transition-transform">
                <mat-icon class="text-white !text-[22px]">{{ feature.icon }}</mat-icon>
              </div>
              <h3 class="text-lg font-semibold text-slate-900 dark:text-white mb-2">{{ feature.title }}</h3>
              <p class="text-sm text-slate-600 dark:text-slate-400 leading-relaxed">{{ feature.description }}</p>
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
              @for (cap of capabilities; track cap.title) {
                <div class="flex gap-4">
                  <div class="shrink-0 w-10 h-10 rounded-lg bg-emerald-100 dark:bg-emerald-900/40 flex items-center justify-center">
                    <mat-icon class="!text-emerald-600 dark:!text-emerald-400 !text-[20px]">{{ cap.icon }}</mat-icon>
                  </div>
                  <div>
                    <h4 class="font-semibold text-slate-900 dark:text-white mb-1">{{ cap.title }}</h4>
                    <p class="text-sm text-slate-600 dark:text-slate-400">{{ cap.description }}</p>
                  </div>
                </div>
              }
            </div>
          </div>

          <div class="bg-white dark:bg-slate-800/50 rounded-2xl p-8 border border-slate-200 dark:border-slate-700/50 shadow-xl">
            <div class="grid grid-cols-2 gap-6">
              @for (stat of stats; track stat.label) {
                <div class="text-center p-4">
                  <div class="text-3xl font-bold text-emerald-600 mb-1">{{ stat.value }}</div>
                  <div class="text-xs text-slate-500 dark:text-slate-400 uppercase tracking-wider">{{ stat.label }}</div>
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
          <a routerLink="/register" mat-flat-button class="!bg-white !text-emerald-700 !rounded-xl !px-8 !py-6 !text-base font-semibold hover:!bg-emerald-50 !shadow-lg !no-underline">
            Start Free Trial
            <mat-icon class="ml-2">rocket_launch</mat-icon>
          </a>
          <a routerLink="/contact" mat-stroked-button class="!rounded-xl !px-8 !py-6 !text-base !border-emerald-300 !text-white hover:!bg-emerald-500/20 !no-underline">
            Contact Sales
          </a>
        </div>
      </div>
    </section>
  `,
})
export class HomeComponent {
  features = [
    { icon: 'send', title: 'Message Sending', description: 'Send text, media, templates, and interactive messages through a simple REST API with automatic queuing and retry.' },
    { icon: 'webhook', title: 'Webhook Handling', description: 'Receive real-time delivery receipts, read confirmations, and incoming messages via secure webhooks.' },
    { icon: 'description', title: 'Template Management', description: 'Create, edit, search, and manage WhatsApp message templates for marketing and transactional messages.' },
    { icon: 'perm_media', title: 'Media Support', description: 'Upload, download, and manage media files. Send images, videos, documents, and audio messages.' },
    { icon: 'speed', title: 'Queue & Retry', description: 'Built-in message queue with automatic retry logic. Messages are reliably delivered even under heavy load.' },
    { icon: 'security', title: 'Enterprise Security', description: 'JWT authentication, HMAC webhook validation, rate limiting, and multi-tenant data isolation.' },
  ];

  capabilities = [
    { icon: 'api', title: 'RESTful API', description: 'Clean, well-documented REST API that wraps the full WhatsApp Cloud API surface.' },
    { icon: 'groups', title: 'Multi-Tenant', description: 'Each customer gets isolated data, credentials, and phone numbers. No cross-tenant leakage.' },
    { icon: 'trending_up', title: 'Auto-Scaling Queue', description: 'Background workers process messages with configurable concurrency and retry policies.' },
    { icon: 'hub', title: 'Full Graph API Proxy', description: 'Direct access to any Facebook Graph API endpoint with automatic auth injection.' },
  ];

  stats = [
    { value: '50+', label: 'API Endpoints' },
    { value: '99.9%', label: 'Uptime SLA' },
    { value: '<100ms', label: 'Avg Latency' },
    { value: '10M+', label: 'Messages/mo' },
  ];
}
