import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService, PermissionService } from '../../../core/services';
import { Campaign, CampaignCreateRequest, PagedResult } from '../../../core/models';

@Component({
  selector: 'app-campaigns',
  standalone: true,
  imports: [CommonModule, ProgressSpinnerModule, FormsModule, TranslateModule],
  template: `
    <div class="space-y-6 min-w-0 app-wrap-safe">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'campaigns.title' | translate }}</h1>
        @if (perm.has('campaignsCreate')) {
        <button
          (click)="openCreateForm()"
          class="flex items-center justify-center gap-2 px-4 py-2 bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white rounded-xl text-sm font-medium transition-colors w-full sm:w-auto">
          <i class="pi pi-megaphone !text-[18px]"></i>
          {{ 'campaigns.create' | translate }}
        </button>
        }
      </div>

      <!-- Stats Row -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        @for (stat of stats(); track stat.label) {
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-4 text-center">
            <div class="text-2xl font-bold" [style.color]="stat.color">{{ stat.value }}</div>
            <div class="text-xs text-slate-500 mt-1">{{ stat.label | translate }}</div>
          </div>
        }
      </div>

      <!-- Campaign Cards -->
      @if (loading()) {
        <div class="flex justify-center py-12"><p-progressSpinner [style]="{'width':'32px','height':'32px'}" strokeWidth="4" /></div>
      } @else if (campaigns().length === 0) {
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 text-center py-12 text-slate-400">
          <i class="pi pi-megaphone !text-[48px] mb-2"></i>
          <p>{{ 'campaigns.noCampaigns' | translate }}</p>
        </div>
      } @else {
        <div class="grid md:grid-cols-2 xl:grid-cols-3 gap-4">
          @for (c of campaigns(); track c.campaignId) {
            <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-5 space-y-4">
              <div class="flex items-start justify-between">
                <div>
                  <h3 class="font-bold text-slate-900 dark:text-white">{{ c.name }}</h3>
                  <p class="text-xs text-slate-500 mt-1">{{ c.templateName }} · {{ c.languageCode }}</p>
                </div>
                <span class="px-2.5 py-1 text-xs font-semibold rounded-full"
                  [class]="getStatusClass(c.status)">{{ c.status }}</span>
              </div>

              @if (c.description) {
                <p class="text-sm text-slate-600 dark:text-slate-400 line-clamp-2">{{ c.description }}</p>
              }

              <!-- Progress -->
              <div>
                <div class="flex items-center justify-between text-xs text-slate-500 mb-1.5">
                  <span>{{ 'campaigns.progress' | translate }}</span>
                  <span>{{ c.sentCount }}/{{ c.totalContacts }}</span>
                </div>
                <div class="w-full h-2 bg-slate-100 dark:bg-slate-700 rounded-full overflow-hidden">
                  <div class="h-full bg-emerald-500 rounded-full transition-all" [style.width.%]="c.totalContacts > 0 ? (c.sentCount / c.totalContacts) * 100 : 0"></div>
                </div>
              </div>

              <!-- Stats -->
              <div class="grid grid-cols-4 gap-2 text-center text-xs">
                <div>
                  <div class="font-bold text-emerald-600">{{ c.sentCount }}</div>
                  <div class="text-slate-400">{{ 'campaigns.sent' | translate }}</div>
                </div>
                <div>
                  <div class="font-bold text-blue-600">{{ c.deliveredCount }}</div>
                  <div class="text-slate-400">{{ 'campaigns.delivered' | translate }}</div>
                </div>
                <div>
                  <div class="font-bold text-purple-600">{{ c.readCount }}</div>
                  <div class="text-slate-400">{{ 'campaigns.read' | translate }}</div>
                </div>
                <div>
                  <div class="font-bold text-red-500">{{ c.failedCount }}</div>
                  <div class="text-slate-400">{{ 'campaigns.failed' | translate }}</div>
                </div>
              </div>

              <!-- Actions -->
              <div class="flex flex-wrap items-center gap-2 pt-2 border-t border-slate-100 dark:border-slate-700/30">
                @if ((c.status === 'DRAFT' || c.status === 'SCHEDULED') && perm.has('campaignsLaunch')) {
                  <button (click)="launchCampaign(c)" class="flex-1 py-2 text-xs font-medium bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white rounded-xl transition-colors">
                    {{ 'campaigns.launch' | translate }}
                  </button>
                  <button (click)="cancelCampaign(c)" class="py-2 px-3 text-xs font-medium bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 rounded-xl transition-colors text-slate-600 dark:text-slate-300">
                    {{ 'common.cancel' | translate }}
                  </button>
                }
                @if (c.status === 'RUNNING' && perm.has('campaignsLaunch')) {
                  <button (click)="cancelCampaign(c)" class="flex-1 py-2 text-xs font-medium bg-red-500 hover:bg-red-600 text-white rounded-xl transition-colors">
                    {{ 'campaigns.stop' | translate }}
                  </button>
                }
                @if (c.status === 'COMPLETED' || c.status === 'CANCELLED') {
                  <span class="text-xs text-slate-400 flex-1 text-center">{{ c.completedAtUtc | date:'short' }}</span>
                }
              </div>
            </div>
          }
        </div>
      }

      <!-- Create Modal -->
      @if (showForm()) {
        <div class="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4" (click)="showForm.set(false)">
          <div class="bg-white dark:bg-slate-800 rounded-2xl w-full max-w-lg max-h-[88vh] overflow-y-auto p-5 sm:p-6 space-y-4" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-bold text-slate-900 dark:text-white">{{ 'campaigns.create' | translate }}</h3>
            <div class="space-y-3">
              <input [(ngModel)]="formData.name" [placeholder]="'campaigns.name' | translate"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              <textarea [(ngModel)]="formData.description" [placeholder]="'campaigns.description' | translate" rows="2"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white resize-none"></textarea>
              <input [(ngModel)]="formData.templateName" [placeholder]="'campaigns.templateName' | translate"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              <div class="grid sm:grid-cols-2 gap-3">
                <input [(ngModel)]="formData.languageCode" [placeholder]="'campaigns.language' | translate" value="ar"
                  class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
                <input [(ngModel)]="formData.whatsAppPhoneNumberId" type="number" [placeholder]="'campaigns.phoneNumber' | translate"
                  class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              </div>
              <input [(ngModel)]="contactIds" [placeholder]="'campaigns.contactIds' | translate"
                class="w-full px-4 py-2.5 bg-slate-100 dark:bg-slate-700 rounded-xl text-sm border-0 focus:ring-2 focus:ring-emerald-500 outline-none text-slate-900 dark:text-white" />
              <p class="text-[11px] text-slate-400">{{ 'campaigns.contactIdsHint' | translate }}</p>
            </div>
            <div class="flex justify-end gap-2 pt-2">
              <button (click)="showForm.set(false)" class="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-xl transition-colors">
                {{ 'common.cancel' | translate }}
              </button>
              <button (click)="createCampaign()" class="px-4 py-2 text-sm bg-[var(--app-primary)] hover:bg-[var(--app-primary-strong)] text-white rounded-xl transition-colors">
                {{ 'common.save' | translate }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class CampaignsComponent implements OnInit {
  private api = inject(ApiService);
  readonly perm = inject(PermissionService);

  loading = signal(true);
  campaigns = signal<Campaign[]>([]);
  showForm = signal(false);

  stats = signal([
    { label: 'campaigns.totalCampaigns', value: 0, color: '#64748b' },
    { label: 'campaigns.running', value: 0, color: '#10b981' },
    { label: 'campaigns.completed', value: 0, color: '#6366f1' },
    { label: 'campaigns.totalSent', value: 0, color: '#f59e0b' },
  ]);

  formData: CampaignCreateRequest = {
    name: '', templateName: '', languageCode: 'ar', whatsAppPhoneNumberId: 0,
  };
  contactIds = '';

  ngOnInit(): void {
    this.loadCampaigns();
  }

  loadCampaigns(): void {
    this.api.get<PagedResult<Campaign>>('/campaigns', { pageSize: '50' }).subscribe({
      next: (r) => {
        const items = r?.items ?? [];
        this.campaigns.set(items);
        this.stats.set([
          { label: 'campaigns.totalCampaigns', value: items.length, color: '#64748b' },
          { label: 'campaigns.running', value: items.filter(c => c.status === 'RUNNING').length, color: '#10b981' },
          { label: 'campaigns.completed', value: items.filter(c => c.status === 'COMPLETED').length, color: '#6366f1' },
          { label: 'campaigns.totalSent', value: items.reduce((sum, c) => sum + c.sentCount, 0), color: '#f59e0b' },
        ]);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openCreateForm(): void {
    this.formData = { name: '', templateName: '', languageCode: 'ar', whatsAppPhoneNumberId: 0 };
    this.contactIds = '';
    this.showForm.set(true);
  }

  createCampaign(): void {
    const body: any = { ...this.formData };
    if (this.contactIds.trim()) {
      body.contactIds = this.contactIds.split(',').map(id => Number(id.trim())).filter(n => n > 0);
    }
    this.api.post('/campaigns', body).subscribe({
      next: () => { this.showForm.set(false); this.loadCampaigns(); },
    });
  }

  launchCampaign(c: Campaign): void {
    this.api.post(`/campaigns/${c.campaignId}/launch`).subscribe({ next: () => this.loadCampaigns() });
  }

  cancelCampaign(c: Campaign): void {
    this.api.post(`/campaigns/${c.campaignId}/cancel`).subscribe({ next: () => this.loadCampaigns() });
  }

  getStatusClass(status: string): string {
    const map: Record<string, string> = {
      DRAFT: 'bg-slate-100 text-slate-600 dark:bg-slate-700 dark:text-slate-300',
      SCHEDULED: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300',
      RUNNING: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300',
      COMPLETED: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300',
      CANCELLED: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300',
    };
    return map[status] || map['DRAFT'];
  }
}

