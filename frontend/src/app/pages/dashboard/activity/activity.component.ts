import { Component } from '@angular/core';
import { ActivityFeedComponent } from '../../../shared/components/activity-feed/activity-feed.component';
import { WebhookStatusComponent } from '../../../shared/components/webhook-status/webhook-status.component';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-activity',
  standalone: true,
  imports: [ActivityFeedComponent, WebhookStatusComponent, TranslateModule],
  template: `
    <div class="space-y-6 min-w-0 app-wrap-safe">
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'activity.title' | translate }}</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'activity.pageSubtitle' | translate }}</p>
      </div>
      <div class="grid xl:grid-cols-3 gap-5 lg:gap-6">
        <div class="xl:col-span-2">
          <app-activity-feed />
        </div>
        <div>
          <app-webhook-status />
        </div>
      </div>
    </div>
  `,
})
export class ActivityComponent {}
