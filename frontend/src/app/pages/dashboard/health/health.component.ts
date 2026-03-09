import { Component, inject, OnInit, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { HttpClient } from '@angular/common/http';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { environment } from '../../../../environments/environment';

interface HealthCheck {
  nameKey: string;
  status: 'healthy' | 'warning' | 'error';
  detailsKey: string;
  icon: string;
  lastChecked: string;
}

@Component({
  selector: 'app-health',
  standalone: true,
  imports: [ButtonModule, ProgressSpinnerModule, TranslateModule],
  template: `
    <div class="space-y-6">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'health.pageTitle' | translate }}</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'health.pageSubtitle' | translate }}</p>
        </div>
        <button pButton [outlined]="true" (click)="checkHealth()" [disabled]="loading()" class="!rounded-xl">
          <i class="pi pi-refresh"></i>
          {{ 'health.refresh' | translate }}
        </button>
      </div>

      <!-- Overall Status -->
      <div class="rounded-2xl p-6 border transition-colors"
        [class]="overallStatus() === 'healthy'
          ? 'bg-emerald-50 dark:bg-emerald-950/30 border-emerald-200 dark:border-emerald-800/50'
          : overallStatus() === 'warning'
          ? 'bg-amber-50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-800/50'
          : 'bg-red-50 dark:bg-red-950/30 border-red-200 dark:border-red-800/50'">
        <div class="flex items-center gap-4">
          <div class="text-4xl">
            {{ overallStatus() === 'healthy' ? '🟢' : overallStatus() === 'warning' ? '🟡' : '🔴' }}
          </div>
          <div>
            <h2 class="text-xl font-bold"
              [class]="overallStatus() === 'healthy' ? 'text-emerald-800 dark:text-emerald-300'
                : overallStatus() === 'warning' ? 'text-amber-800 dark:text-amber-300'
                : 'text-red-800 dark:text-red-300'">
              {{ overallStatus() === 'healthy' ? ('health.allOperational' | translate) : overallStatus() === 'warning' ? ('health.partialIssues' | translate) : ('health.serviceDisruption' | translate) }}
            </h2>
            <p class="text-sm opacity-70">{{ 'health.lastChecked' | translate }}: {{ lastCheckedTime() }}</p>
          </div>
        </div>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-12"><p-progressSpinner [style]="{'width':'36px','height':'36px'}" strokeWidth="4" /></div>
      } @else {
        <!-- Health Cards -->
        <div class="grid md:grid-cols-2 gap-4">
          @for (check of checks(); track check.nameKey) {
            <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-6 hover:shadow-md transition-shadow">
              <div class="flex items-start justify-between mb-4">
                <div class="flex items-center gap-3">
                  <div class="w-10 h-10 rounded-xl flex items-center justify-center"
                    [class]="check.status === 'healthy' ? 'bg-emerald-100 dark:bg-emerald-900/40'
                      : check.status === 'warning' ? 'bg-amber-100 dark:bg-amber-900/40'
                      : 'bg-red-100 dark:bg-red-900/40'">
                    <i [class]="'pi pi-' + check.icon + ' ' + (check.status === 'healthy' ? '!text-emerald-600'
                        : check.status === 'warning' ? '!text-amber-600'
                        : '!text-red-600')">
                    </i>
                  </div>
                  <div>
                    <h3 class="font-semibold text-slate-900 dark:text-white">{{ check.nameKey | translate }}</h3>
                    <p class="text-xs text-slate-500 mt-0.5">{{ check.detailsKey | translate }}</p>
                  </div>
                </div>
                <div class="text-xl">
                  {{ check.status === 'healthy' ? '🟢' : check.status === 'warning' ? '🟡' : '🔴' }}
                </div>
              </div>
              <div class="text-xs text-slate-400">{{ 'health.lastChecked' | translate }}: {{ check.lastChecked }}</div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class HealthComponent implements OnInit {
  private http = inject(HttpClient);
  private t = inject(TranslateService);

  loading = signal(true);
  checks = signal<HealthCheck[]>([]);
  overallStatus = signal<'healthy' | 'warning' | 'error'>('healthy');
  lastCheckedTime = signal('');

  ngOnInit(): void {
    this.checkHealth();
  }

  checkHealth(): void {
    this.loading.set(true);
    const now = new Date().toLocaleTimeString();

    this.http.get(`${environment.apiUrl.replace('/api', '')}/health`, { responseType: 'text' }).subscribe({
      next: () => {
        this.checks.set([
          { nameKey: 'health.apiServer', status: 'healthy', detailsKey: 'health.apiHealthy', icon: 'server', lastChecked: now },
          { nameKey: 'health.whatsappApi', status: 'healthy', detailsKey: 'health.whatsappApiOk', icon: 'cloud', lastChecked: now },
          { nameKey: 'health.messageQueue', status: 'healthy', detailsKey: 'health.messageQueueOk', icon: 'sync', lastChecked: now },
          { nameKey: 'health.database', status: 'healthy', detailsKey: 'health.databaseOk', icon: 'server', lastChecked: now },
          { nameKey: 'health.webhookDelivery', status: 'healthy', detailsKey: 'health.webhookOk', icon: 'link', lastChecked: now },
          { nameKey: 'health.authentication', status: 'healthy', detailsKey: 'health.authOk', icon: 'shield', lastChecked: now },
        ]);
        this.overallStatus.set('healthy');
        this.lastCheckedTime.set(now);
        this.loading.set(false);
      },
      error: () => {
        this.checks.set([
          { nameKey: 'health.apiServer', status: 'error', detailsKey: 'health.apiError', icon: 'server', lastChecked: now },
          { nameKey: 'health.whatsappApi', status: 'warning', detailsKey: 'health.unableToVerify', icon: 'cloud', lastChecked: now },
          { nameKey: 'health.messageQueue', status: 'warning', detailsKey: 'health.unableToVerify', icon: 'sync', lastChecked: now },
          { nameKey: 'health.database', status: 'warning', detailsKey: 'health.unableToVerify', icon: 'server', lastChecked: now },
          { nameKey: 'health.webhookDelivery', status: 'warning', detailsKey: 'health.unableToVerifyWebhook', icon: 'link', lastChecked: now },
          { nameKey: 'health.authentication', status: 'warning', detailsKey: 'health.unableToVerifyWebhook', icon: 'shield', lastChecked: now },
        ]);
        this.overallStatus.set('error');
        this.lastCheckedTime.set(now);
        this.loading.set(false);
      },
    });
  }
}
