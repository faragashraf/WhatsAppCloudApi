import { Component, inject, OnInit, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TooltipModule } from 'primeng/tooltip';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ApiService } from '../../../core/services';
import { WhatsAppTemplate, WhatsAppTemplateComponent as TemplateComp, CreateTemplateRequest } from '../../../core/models';

@Component({
  selector: 'app-templates',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, ButtonModule, ProgressSpinnerModule, TooltipModule, TranslateModule],
  template: `
    <div class="max-w-6xl mx-auto space-y-6 min-w-0 app-wrap-safe">
      <!-- Header -->
      <div class="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'templates.title' | translate }}</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ 'templates.subtitle' | translate }}</p>
        </div>
        <button pButton (click)="showCreateDialog.set(true)"
          class="!bg-emerald-500 hover:!bg-emerald-600 !text-white !rounded-xl !border-0 !shadow-md w-full sm:!w-auto">
          <i class="pi pi-plus me-2"></i>
          {{ 'templates.create' | translate }}
        </button>
      </div>

      <!-- Search & Filter -->
      <div class="flex flex-wrap gap-3">
        <div class="relative w-full lg:flex-1 lg:min-w-[260px]">
          <i class="pi pi-search absolute start-3 top-2.5 text-slate-400 !text-[16px]"></i>
          <input [(ngModel)]="searchQuery" [placeholder]="'templates.search' | translate"
            class="w-full ps-10 pe-4 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-sm outline-none focus:ring-2 focus:ring-emerald-500/30 text-slate-900 dark:text-white" />
        </div>
        <div class="flex flex-wrap gap-2 w-full xl:w-auto">
          @for (cat of categories; track cat) {
            <button (click)="filterCategory.set(cat)"
              class="px-3 py-2 rounded-xl text-xs font-medium transition-all border"
              [class]="filterCategory() === cat
                ? 'bg-emerald-500 text-white border-emerald-500 shadow-sm'
                : 'bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:border-emerald-300'">
              {{ cat === 'all' ? ('templates.all' | translate) : cat }}
            </button>
          }
        </div>
        <div class="flex flex-wrap gap-2 w-full xl:w-auto">
          @for (st of statuses; track st) {
            <button (click)="filterStatus.set(st)"
              class="px-3 py-2 rounded-xl text-xs font-medium transition-all border"
              [class]="filterStatus() === st
                ? 'bg-emerald-500 text-white border-emerald-500 shadow-sm'
                : 'bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:border-emerald-300'">
              {{ st === 'all' ? ('templates.all' | translate) : st }}
            </button>
          }
        </div>
      </div>

      <!-- Stats -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white dark:bg-slate-800 rounded-xl p-4 border border-slate-200 dark:border-slate-700">
          <p class="text-2xl font-bold text-slate-900 dark:text-white">{{ templates().length }}</p>
          <p class="text-xs text-slate-500">{{ 'templates.totalTemplates' | translate }}</p>
        </div>
        <div class="bg-white dark:bg-slate-800 rounded-xl p-4 border border-slate-200 dark:border-slate-700">
          <p class="text-2xl font-bold text-emerald-600">{{ approvedCount() }}</p>
          <p class="text-xs text-slate-500">{{ 'templates.approved' | translate }}</p>
        </div>
        <div class="bg-white dark:bg-slate-800 rounded-xl p-4 border border-slate-200 dark:border-slate-700">
          <p class="text-2xl font-bold text-amber-500">{{ pendingCount() }}</p>
          <p class="text-xs text-slate-500">{{ 'templates.pending' | translate }}</p>
        </div>
        <div class="bg-white dark:bg-slate-800 rounded-xl p-4 border border-slate-200 dark:border-slate-700">
          <p class="text-2xl font-bold text-red-500">{{ rejectedCount() }}</p>
          <p class="text-xs text-slate-500">{{ 'templates.rejected' | translate }}</p>
        </div>
      </div>

      <!-- Templates Grid -->
      @if (loading()) {
        <div class="flex justify-center py-20">
          <p-progressSpinner [style]="{'width':'36px','height':'36px'}" strokeWidth="4" />
        </div>
      } @else if (filteredTemplates().length === 0) {
        <div class="text-center py-20">
          <div class="w-20 h-20 mx-auto mb-4 rounded-full bg-slate-100 dark:bg-slate-800 flex items-center justify-center">
            <i class="pi pi-file-edit !text-[36px] text-slate-300 dark:text-slate-600"></i>
          </div>
          <h3 class="text-lg font-semibold text-slate-700 dark:text-slate-300">{{ 'templates.noTemplates' | translate }}</h3>
          <p class="text-sm text-slate-400 mt-1">{{ 'templates.noTemplatesHint' | translate }}</p>
        </div>
      } @else {
        <div class="grid md:grid-cols-2 xl:grid-cols-3 gap-4">
          @for (tpl of filteredTemplates(); track tpl.id) {
            <div class="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 hover:border-emerald-300 dark:hover:border-emerald-700 hover:shadow-lg transition-all group overflow-hidden">
              <!-- Card Header -->
              <div class="p-4 pb-3 border-b border-slate-100 dark:border-slate-700/50">
                <div class="flex items-start justify-between gap-2">
                  <div class="flex-1 min-w-0">
                    <h3 class="text-sm font-bold text-slate-900 dark:text-white truncate" [pTooltip]="tpl.name">{{ tpl.name }}</h3>
                    <div class="flex items-center gap-2 mt-1.5">
                      <span class="text-[10px] px-2 py-0.5 rounded-full font-medium"
                        [class]="getStatusClass(tpl.status)">
                        {{ tpl.status }}
                      </span>
                      <span class="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 font-medium">
                        {{ tpl.category }}
                      </span>
                      <span class="text-[10px] text-slate-400 flex items-center gap-1">
                        <i class="pi pi-globe !text-[10px]"></i>
                        {{ tpl.language }}
                      </span>
                    </div>
                  </div>
                  <!-- Actions -->
                  <div class="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button (click)="previewTemplate(tpl)"
                      class="w-7 h-7 rounded-lg hover:bg-emerald-50 dark:hover:bg-emerald-950/30 flex items-center justify-center text-slate-400 hover:text-emerald-600 transition-colors"
                      [pTooltip]="'templates.preview' | translate">
                      <i class="pi pi-eye !text-[14px]"></i>
                    </button>
                    <button (click)="duplicateTemplate(tpl)"
                      class="w-7 h-7 rounded-lg hover:bg-blue-50 dark:hover:bg-blue-950/30 flex items-center justify-center text-slate-400 hover:text-blue-600 transition-colors"
                      [pTooltip]="'templates.duplicate' | translate">
                      <i class="pi pi-copy !text-[14px]"></i>
                    </button>
                    <button (click)="deleteTemplate(tpl)"
                      class="w-7 h-7 rounded-lg hover:bg-red-50 dark:hover:bg-red-950/30 flex items-center justify-center text-slate-400 hover:text-red-500 transition-colors"
                      [pTooltip]="'templates.delete' | translate">
                      <i class="pi pi-trash !text-[14px]"></i>
                    </button>
                  </div>
                </div>
              </div>
              <!-- Card Body - Template Preview -->
              <div class="p-4 space-y-2">
                @for (comp of tpl.components; track $index) {
                  @switch (comp.type) {
                    @case ('HEADER') {
                      <div class="text-xs font-semibold text-slate-700 dark:text-slate-300 pb-1 border-b border-dashed border-slate-200 dark:border-slate-700">
                        @if (comp.format === 'TEXT') { {{ comp.text }} }
                        @else if (comp.format) {
                          <span class="flex items-center gap-1 text-emerald-600 dark:text-emerald-400">
                            <i class="pi !text-[12px]" [ngClass]="comp.format === 'IMAGE' ? 'pi-image' : comp.format === 'VIDEO' ? 'pi-video' : 'pi-file'"></i>
                            {{ comp.format }}
                          </span>
                        }
                      </div>
                    }
                    @case ('BODY') {
                      <p class="text-xs text-slate-600 dark:text-slate-400 leading-relaxed line-clamp-3">{{ comp.text }}</p>
                    }
                    @case ('FOOTER') {
                      <p class="text-[10px] text-slate-400 dark:text-slate-500 italic">{{ comp.text }}</p>
                    }
                    @case ('BUTTONS') {
                      <div class="flex flex-wrap gap-1.5 pt-1">
                        @for (btn of comp.buttons; track $index) {
                          <span class="inline-flex items-center gap-1 px-2 py-1 bg-emerald-50 dark:bg-emerald-950/20 text-emerald-700 dark:text-emerald-400 rounded-md text-[10px] font-medium">
                            <i class="pi !text-[10px]" [ngClass]="btn.type === 'URL' ? 'pi-external-link' : btn.type === 'PHONE_NUMBER' ? 'pi-phone' : 'pi-reply'"></i>
                            {{ btn.text }}
                          </span>
                        }
                      </div>
                    }
                  }
                }
              </div>
              @if (tpl.id) {
                <div class="px-4 pb-3">
                  <span class="text-[10px] text-slate-400 font-mono">ID: {{ tpl.id }}</span>
                </div>
              }
            </div>
          }
        </div>
      }
    </div>

    <!-- ━━ Create Template Dialog ━━━━━━━━━━━━━━━━━━━━━━━━━━ -->
    @if (showCreateDialog()) {
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm" (click)="showCreateDialog.set(false)">
        <div class="bg-white dark:bg-slate-800 rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto mx-4" (click)="$event.stopPropagation()">
          <div class="p-6 border-b border-slate-200 dark:border-slate-700 flex items-center justify-between">
            <h2 class="text-lg font-bold text-slate-900 dark:text-white">{{ 'templates.createNew' | translate }}</h2>
            <button (click)="showCreateDialog.set(false)" class="w-8 h-8 rounded-full hover:bg-slate-100 dark:hover:bg-slate-700 flex items-center justify-center">
              <i class="pi pi-times text-slate-400"></i>
            </button>
          </div>
          <div class="p-6 space-y-4">
            <!-- Name -->
            <div>
              <label class="text-xs font-medium text-slate-700 dark:text-slate-300 block mb-1">{{ 'templates.name' | translate }}</label>
              <input [(ngModel)]="form.name"
                class="w-full px-4 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl text-sm outline-none focus:ring-2 focus:ring-emerald-500/30 text-slate-900 dark:text-white"
                [placeholder]="'templates.namePlaceholder' | translate" />
              <p class="text-[10px] text-slate-400 mt-1">{{ 'templates.nameHint' | translate }}</p>
            </div>
            <!-- Category -->
            <div>
              <label class="text-xs font-medium text-slate-700 dark:text-slate-300 block mb-1">{{ 'templates.category' | translate }}</label>
              <div class="flex flex-wrap gap-2">
                @for (cat of ['MARKETING', 'UTILITY', 'AUTHENTICATION']; track cat) {
                  <button (click)="form.category = cat"
                    class="px-4 py-2 rounded-xl text-xs font-medium border transition-all"
                    [class]="form.category === cat
                      ? 'bg-emerald-500 text-white border-emerald-500'
                      : 'bg-white dark:bg-slate-900 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:border-emerald-300'">
                    {{ cat }}
                  </button>
                }
              </div>
            </div>
            <!-- Language -->
            <div>
              <label class="text-xs font-medium text-slate-700 dark:text-slate-300 block mb-1">{{ 'templates.language' | translate }}</label>
              <select [(ngModel)]="form.language"
                class="w-full px-4 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl text-sm outline-none focus:ring-2 focus:ring-emerald-500/30 text-slate-900 dark:text-white">
                <option value="en_US">English (US)</option>
                <option value="en">English</option>
                <option value="ar">العربية</option>
                <option value="es">Español</option>
                <option value="fr">Français</option>
                <option value="de">Deutsch</option>
                <option value="pt_BR">Português (BR)</option>
                <option value="hi">हिन्दी</option>
                <option value="tr">Türkçe</option>
              </select>
            </div>
            <!-- Header -->
            <div>
              <label class="text-xs font-medium text-slate-700 dark:text-slate-300 block mb-1">{{ 'templates.header' | translate }}</label>
              <div class="flex gap-2 mb-2">
                @for (fmt of ['NONE', 'TEXT', 'IMAGE', 'VIDEO', 'DOCUMENT']; track fmt) {
                  <button (click)="form.headerFormat = fmt"
                    class="px-3 py-1.5 rounded-lg text-[10px] font-medium border transition-all"
                    [class]="form.headerFormat === fmt
                      ? 'bg-emerald-500 text-white border-emerald-500'
                      : 'bg-white dark:bg-slate-900 text-slate-500 dark:text-slate-400 border-slate-200 dark:border-slate-700'">
                    {{ fmt }}
                  </button>
                }
              </div>
              @if (form.headerFormat === 'TEXT') {
                <input [(ngModel)]="form.headerText"
                  class="w-full px-4 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl text-sm outline-none focus:ring-2 focus:ring-emerald-500/30 text-slate-900 dark:text-white"
                  [placeholder]="'templates.headerTextPlaceholder' | translate" />
              }
            </div>
            <!-- Body -->
            <div>
              <label class="text-xs font-medium text-slate-700 dark:text-slate-300 block mb-1">{{ 'templates.body' | translate }} *</label>
              <textarea [(ngModel)]="form.bodyText" rows="4"
                class="w-full px-4 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl text-sm outline-none focus:ring-2 focus:ring-emerald-500/30 text-slate-900 dark:text-white resize-none"
                [placeholder]="'templates.bodyPlaceholder' | translate"></textarea>
              <p class="text-[10px] text-slate-400 mt-1">{{ 'templates.bodyHint' | translate }}</p>
            </div>
            <!-- Footer -->
            <div>
              <label class="text-xs font-medium text-slate-700 dark:text-slate-300 block mb-1">{{ 'templates.footer' | translate }}</label>
              <input [(ngModel)]="form.footerText"
                class="w-full px-4 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl text-sm outline-none focus:ring-2 focus:ring-emerald-500/30 text-slate-900 dark:text-white"
                [placeholder]="'templates.footerPlaceholder' | translate" />
            </div>
            <!-- Buttons -->
            <div>
              <div class="flex items-center justify-between mb-2">
                <label class="text-xs font-medium text-slate-700 dark:text-slate-300">{{ 'templates.buttons' | translate }}</label>
                <button (click)="addButton()" [disabled]="form.buttons.length >= 3"
                  class="text-[10px] text-emerald-600 hover:text-emerald-700 font-medium disabled:opacity-40">
                  + {{ 'templates.addButton' | translate }}
                </button>
              </div>
              @for (btn of form.buttons; track $index; let i = $index) {
                <div class="flex items-center gap-2 mb-2">
                  <select [(ngModel)]="btn.type"
                    class="px-2 py-1.5 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-xs outline-none text-slate-900 dark:text-white w-28">
                    <option value="QUICK_REPLY">Quick Reply</option>
                    <option value="URL">URL</option>
                    <option value="PHONE_NUMBER">Phone</option>
                  </select>
                  <input [(ngModel)]="btn.text" placeholder="Button text"
                    class="flex-1 px-3 py-1.5 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-xs outline-none text-slate-900 dark:text-white" />
                  @if (btn.type === 'URL') {
                    <input [(ngModel)]="btn.url" placeholder="https://..."
                      class="flex-1 px-3 py-1.5 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-xs outline-none text-slate-900 dark:text-white" />
                  }
                  @if (btn.type === 'PHONE_NUMBER') {
                    <input [(ngModel)]="btn.phoneNumber" placeholder="+1234..."
                      class="flex-1 px-3 py-1.5 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-xs outline-none text-slate-900 dark:text-white" />
                  }
                  <button (click)="removeButton(i)" class="text-red-400 hover:text-red-600 transition-colors">
                    <i class="pi pi-times !text-[12px]"></i>
                  </button>
                </div>
              }
            </div>
          </div>
          <!-- Dialog Footer -->
          <div class="p-6 border-t border-slate-200 dark:border-slate-700 flex items-center justify-end gap-3">
            <button pButton [text]="true" severity="secondary" (click)="showCreateDialog.set(false)">
              {{ 'common.cancel' | translate }}
            </button>
            <button pButton (click)="submitTemplate()" [disabled]="creating() || !form.name || !form.bodyText"
              class="!bg-emerald-500 hover:!bg-emerald-600 !text-white !rounded-xl !border-0">
              @if (creating()) {
                <p-progressSpinner [style]="{'width':'16px','height':'16px'}" strokeWidth="4" />
              } @else {
                <i class="pi pi-check me-2"></i>
                {{ 'templates.submit' | translate }}
              }
            </button>
          </div>
        </div>
      </div>
    }

    <!-- ━━ Preview Dialog ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ -->
    @if (previewTpl()) {
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm" (click)="previewTpl.set(null)">
        <div class="bg-white dark:bg-slate-800 rounded-2xl shadow-2xl w-full max-w-md mx-4" (click)="$event.stopPropagation()">
          <div class="p-4 border-b border-slate-200 dark:border-slate-700 flex items-center justify-between">
            <h3 class="text-sm font-bold text-slate-900 dark:text-white">{{ previewTpl()!.name }}</h3>
            <button (click)="previewTpl.set(null)" class="w-8 h-8 rounded-full hover:bg-slate-100 dark:hover:bg-slate-700 flex items-center justify-center">
              <i class="pi pi-times text-slate-400"></i>
            </button>
          </div>
          <!-- WhatsApp-style preview -->
          <div class="p-6">
            <div class="max-w-[280px] mx-auto bg-[#d9fdd3] dark:bg-emerald-900/70 rounded-lg p-3 shadow-sm space-y-1.5">
              @for (comp of previewTpl()!.components; track $index) {
                @switch (comp.type) {
                  @case ('HEADER') {
                    @if (comp.format === 'TEXT') {
                      <p class="text-sm font-bold text-slate-900 dark:text-white">{{ comp.text }}</p>
                    } @else if (comp.format) {
                      <div class="h-28 rounded-md bg-emerald-100 dark:bg-emerald-800/40 flex items-center justify-center">
                        <i class="pi !text-[24px] text-emerald-400" [ngClass]="comp.format === 'IMAGE' ? 'pi-image' : comp.format === 'VIDEO' ? 'pi-video' : 'pi-file'"></i>
                      </div>
                    }
                  }
                  @case ('BODY') {
                    <p class="text-[13px] text-slate-800 dark:text-slate-200 whitespace-pre-wrap leading-relaxed">{{ comp.text }}</p>
                  }
                  @case ('FOOTER') {
                    <p class="text-[11px] text-slate-500 dark:text-slate-400 italic">{{ comp.text }}</p>
                  }
                  @case ('BUTTONS') {
                    <div class="border-t border-emerald-300/30 pt-2 mt-2 space-y-1">
                      @for (btn of comp.buttons; track $index) {
                        <div class="text-center text-[12px] text-blue-600 dark:text-blue-400 font-medium py-1">
                          {{ btn.text }}
                        </div>
                      }
                    </div>
                  }
                }
              }
            </div>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .line-clamp-3 { display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
  `],
})
export class TemplatesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly translate = inject(TranslateService);

  templates = signal<WhatsAppTemplate[]>([]);
  loading = signal(true);
  creating = signal(false);
  showCreateDialog = signal(false);
  previewTpl = signal<WhatsAppTemplate | null>(null);
  filterCategory = signal('all');
  filterStatus = signal('all');
  searchQuery = '';

  categories = ['all', 'MARKETING', 'UTILITY', 'AUTHENTICATION'];
  statuses = ['all', 'APPROVED', 'PENDING', 'REJECTED'];

  form = this.getEmptyForm();

  // ─── Computed ───
  filteredTemplates = computed(() => {
    let list = this.templates();
    const q = this.searchQuery.toLowerCase();
    if (q) list = list.filter(t => t.name.toLowerCase().includes(q) || t.language.toLowerCase().includes(q));
    const cat = this.filterCategory();
    if (cat !== 'all') list = list.filter(t => t.category?.toUpperCase() === cat);
    const st = this.filterStatus();
    if (st !== 'all') list = list.filter(t => t.status?.toUpperCase() === st);
    return list;
  });

  approvedCount = computed(() => this.templates().filter(t => t.status?.toUpperCase() === 'APPROVED').length);
  pendingCount = computed(() => this.templates().filter(t => t.status?.toUpperCase() === 'PENDING').length);
  rejectedCount = computed(() => this.templates().filter(t => t.status?.toUpperCase() === 'REJECTED').length);

  ngOnInit(): void {
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.loading.set(true);
    this.api.get<any>('/whatsapp/templates').subscribe({
      next: (result) => {
        const data = result?.data || result || [];
        this.templates.set(Array.isArray(data) ? data : []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  submitTemplate(): void {
    if (!this.form.name || !this.form.bodyText) return;
    this.creating.set(true);

    const components: TemplateComp[] = [];

    // Header
    if (this.form.headerFormat !== 'NONE') {
      components.push({
        type: 'HEADER',
        format: this.form.headerFormat,
        text: this.form.headerFormat === 'TEXT' ? this.form.headerText : undefined,
      });
    }

    // Body
    components.push({ type: 'BODY', text: this.form.bodyText });

    // Footer
    if (this.form.footerText) {
      components.push({ type: 'FOOTER', text: this.form.footerText });
    }

    // Buttons
    if (this.form.buttons.length > 0) {
      components.push({
        type: 'BUTTONS',
        buttons: this.form.buttons.map(b => ({
          type: b.type,
          text: b.text,
          url: b.url || undefined,
          phoneNumber: b.phoneNumber || undefined,
        })),
      });
    }

    const request: CreateTemplateRequest = {
      name: this.form.name,
      language: this.form.language,
      category: this.form.category,
      components,
    };

    this.api.post<any>('/whatsapp/templates', request).subscribe({
      next: () => {
        this.creating.set(false);
        this.showCreateDialog.set(false);
        this.form = this.getEmptyForm();
        this.loadTemplates();
      },
      error: () => this.creating.set(false),
    });
  }

  deleteTemplate(tpl: WhatsAppTemplate): void {
    if (!confirm(this.translate.instant('templates.confirmDelete', { name: tpl.name }))) return;
    this.api.delete(`/whatsapp/templates/${tpl.id}`).subscribe({
      next: () => this.loadTemplates(),
    });
  }

  previewTemplate(tpl: WhatsAppTemplate): void {
    this.previewTpl.set(tpl);
  }

  duplicateTemplate(tpl: WhatsAppTemplate): void {
    const bodyComp = tpl.components.find(c => c.type === 'BODY');
    const headerComp = tpl.components.find(c => c.type === 'HEADER');
    const footerComp = tpl.components.find(c => c.type === 'FOOTER');
    const buttonsComp = tpl.components.find(c => c.type === 'BUTTONS');

    this.form = {
      name: tpl.name + '_copy',
      category: tpl.category || 'MARKETING',
      language: tpl.language || 'en_US',
      headerFormat: headerComp?.format || 'NONE',
      headerText: headerComp?.text || '',
      bodyText: bodyComp?.text || '',
      footerText: footerComp?.text || '',
      buttons: buttonsComp?.buttons?.map(b => ({ ...b })) || [],
    };
    this.showCreateDialog.set(true);
  }

  addButton(): void {
    if (this.form.buttons.length >= 3) return;
    this.form.buttons.push({ type: 'QUICK_REPLY', text: '', url: '', phoneNumber: '' });
  }

  removeButton(index: number): void {
    this.form.buttons.splice(index, 1);
  }

  getStatusClass(status: string): string {
    switch (status?.toUpperCase()) {
      case 'APPROVED': return 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400';
      case 'PENDING': return 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400';
      case 'REJECTED': return 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400';
      default: return 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300';
    }
  }

  private getEmptyForm() {
    return {
      name: '',
      category: 'MARKETING',
      language: 'en_US',
      headerFormat: 'NONE',
      headerText: '',
      bodyText: '',
      footerText: '',
      buttons: [] as { type: string; text: string; url?: string; phoneNumber?: string }[],
    };
  }
}
