import { Component, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ActivityStreamService } from '../../../core/services/activity-stream.service';

@Component({
  selector: 'app-webhook-status',
  standalone: true,
  imports: [TranslateModule],
  template: `
    <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6">
      <div class="flex items-center justify-between mb-4">
        <h3 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'webhook.title' | translate }}</h3>
        <span class="relative flex h-3 w-3">
          @if (stream.webhookConnected()) {
            <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span class="relative inline-flex rounded-full h-3 w-3 bg-emerald-500"></span>
          } @else {
            <span class="relative inline-flex rounded-full h-3 w-3 bg-amber-500"></span>
          }
        </span>
      </div>

      <div class="space-y-3">
        <!-- Status -->
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl flex items-center justify-center"
            [class]="stream.webhookConnected()
              ? 'bg-emerald-100 dark:bg-emerald-900/30'
              : 'bg-amber-100 dark:bg-amber-900/30'">
            <i class="pi text-[20px]"
              [class]="stream.webhookConnected() ? 'pi-link text-emerald-600' : 'pi-times-circle text-amber-600'">
            </i>
          </div>
          <div>
            <div class="text-sm font-semibold"
              [class]="stream.webhookConnected()
                ? 'text-emerald-700 dark:text-emerald-400'
                : 'text-amber-700 dark:text-amber-400'">
              {{ (stream.webhookConnected() ? 'webhook.connected' : 'webhook.noActivity') | translate }}
            </div>
            @if (stream.lastWebhookEvent()) {
              <div class="text-xs text-slate-500 dark:text-slate-400">
                {{ 'webhook.lastEvent' | translate }}: {{ formatTimeAgo(stream.lastWebhookEvent()!) }}
              </div>
            } @else {
              <div class="text-xs text-slate-400">
                {{ 'webhook.noEventsDetected' | translate }}
              </div>
            }
          </div>
        </div>

        <!-- Connection quality indicator -->
        <div class="flex items-center gap-2">
          <div class="flex-1 h-1.5 rounded-full bg-slate-100 dark:bg-slate-700/50 overflow-hidden">
            <div class="h-full rounded-full transition-all duration-500"
              [class]="stream.webhookConnected() ? 'bg-emerald-500' : 'bg-amber-500'"
              [style.width]="stream.webhookConnected() ? '100%' : '30%'"></div>
          </div>
          <span class="text-[10px] font-medium uppercase tracking-wider"
            [class]="stream.webhookConnected() ? 'text-emerald-600' : 'text-amber-600'">
            {{ (stream.webhookConnected() ? 'webhook.healthy' : 'webhook.degraded') | translate }}
          </span>
        </div>
      </div>
    </div>
  `,
})
export class WebhookStatusComponent {
  readonly stream = inject(ActivityStreamService);

  formatTimeAgo(date: Date): string {
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const seconds = Math.floor(diff / 1000);
    if (seconds < 60) return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return date.toLocaleDateString();
  }
}
