import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ActivityStreamService } from '../../../core/services/activity-stream.service';
import { ActivityEvent, ACTIVITY_TYPES, StreamConnectionStatus } from '../../../core/models';

@Component({
  selector: 'app-activity-feed',
  standalone: true,
  imports: [TranslateModule],
  template: `
    <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 h-full flex flex-col">
      <!-- Header -->
      <div class="flex items-center justify-between mb-4 shrink-0">
        <div class="flex items-center gap-2">
          <h3 class="text-lg font-semibold text-slate-900 dark:text-white">{{ 'activity.title' | translate }}</h3>
          <!-- Connection status dot -->
          <span class="relative flex h-2.5 w-2.5">
            @if (connectionStatus() === 'connected') {
              <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
              <span class="relative inline-flex rounded-full h-2.5 w-2.5 bg-emerald-500"></span>
            } @else if (connectionStatus() === 'reconnecting') {
              <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-amber-400 opacity-75"></span>
              <span class="relative inline-flex rounded-full h-2.5 w-2.5 bg-amber-500"></span>
            } @else {
              <span class="relative inline-flex rounded-full h-2.5 w-2.5 bg-slate-400"></span>
            }
          </span>
        </div>
        <span class="text-xs font-medium px-2.5 py-1 rounded-full"
          [class]="connectionStatus() === 'connected'
            ? 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400'
            : connectionStatus() === 'reconnecting'
              ? 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400'
              : 'bg-slate-100 dark:bg-slate-700/30 text-slate-500 dark:text-slate-400'">
          {{ ('activity.status.' + connectionStatus()) | translate }}
        </span>
      </div>

      <!-- Feed list -->
      <div class="flex-1 overflow-y-auto space-y-2 min-h-0 max-h-[400px] pr-1 -mr-1">
        @if (events().length === 0) {
          <div class="text-center py-10 text-slate-400">
            <i class="pi pi-chart-line text-[36px] mb-2"></i>
            <p class="text-sm">{{ 'activity.noEvents' | translate }}</p>
          </div>
        } @else {
          @for (event of events(); track event.id; let i = $index) {
            <div class="flex items-start gap-3 py-2.5 px-3 rounded-xl hover:bg-slate-50 dark:hover:bg-slate-700/20 transition-colors animate-fadeInUp"
              [style.animation-delay]="i < 5 ? (i * 50) + 'ms' : '0ms'">
              <!-- Icon -->
              <div class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 mt-0.5"
                [class]="getSeverityBg(event.severity)">
                <i class="!text-[16px]" [class]="'pi ' + getEventIcon(event.type) + ' ' + getSeverityIcon(event.severity)"></i>
              </div>
              <!-- Content -->
              <div class="flex-1 min-w-0">
                <p class="text-sm text-slate-800 dark:text-slate-200 leading-snug truncate">{{ event.message }}</p>
                <span class="text-[11px] text-slate-400 dark:text-slate-500">{{ formatTime(event.timestamp) }}</span>
              </div>
              <!-- Severity badge -->
              <span class="shrink-0 w-1.5 h-1.5 rounded-full mt-2"
                [class]="getSeverityDot(event.severity)"></span>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    @keyframes fadeInUp {
      from { opacity: 0; transform: translateY(8px); }
      to   { opacity: 1; transform: translateY(0); }
    }
    .animate-fadeInUp {
      animation: fadeInUp 0.3s ease-out both;
    }
  `],
})
export class ActivityFeedComponent implements OnInit, OnDestroy {
  private readonly activityStream = inject(ActivityStreamService);

  readonly events = this.activityStream.events;
  readonly connectionStatus = this.activityStream.connectionStatus;

  ngOnInit(): void {
    this.activityStream.startPolling();
  }

  ngOnDestroy(): void {
    this.activityStream.stopPolling();
  }

  getEventIcon(type: string): string {
    const iconMap: Record<string, string> = {
      [ACTIVITY_TYPES.MESSAGE_SENT]: 'pi-check-circle',
      [ACTIVITY_TYPES.MESSAGE_DELIVERED]: 'pi-check',
      [ACTIVITY_TYPES.MESSAGE_READ]: 'pi-eye',
      [ACTIVITY_TYPES.MESSAGE_FAILED]: 'pi-times-circle',
      [ACTIVITY_TYPES.WEBHOOK_RECEIVED]: 'pi-link',
      [ACTIVITY_TYPES.WEBHOOK_VALIDATION]: 'pi-shield',
      [ACTIVITY_TYPES.INCOMING_MESSAGE]: 'pi-comments',
      [ACTIVITY_TYPES.NUMBER_CONNECTED]: 'pi-phone',
      [ACTIVITY_TYPES.NUMBER_DISCONNECTED]: 'pi-phone',
      [ACTIVITY_TYPES.API_KEY_CREATED]: 'pi-key',
      [ACTIVITY_TYPES.SUBSCRIPTION_UPDATED]: 'pi-credit-card',
    };
    return iconMap[type] || 'pi-bell';
  }

  getSeverityBg(severity: string): string {
    return {
      success: 'bg-emerald-100 dark:bg-emerald-900/30',
      error: 'bg-red-100 dark:bg-red-900/30',
      warning: 'bg-amber-100 dark:bg-amber-900/30',
      info: 'bg-blue-100 dark:bg-blue-900/30',
    }[severity] || 'bg-slate-100 dark:bg-slate-700/30';
  }

  getSeverityIcon(severity: string): string {
    return {
      success: '!text-emerald-600',
      error: '!text-red-600',
      warning: '!text-amber-600',
      info: '!text-blue-600',
    }[severity] || '!text-slate-500';
  }

  getSeverityDot(severity: string): string {
    return {
      success: 'bg-emerald-500',
      error: 'bg-red-500',
      warning: 'bg-amber-500',
      info: 'bg-blue-500',
    }[severity] || 'bg-slate-400';
  }

  formatTime(date: Date): string {
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
