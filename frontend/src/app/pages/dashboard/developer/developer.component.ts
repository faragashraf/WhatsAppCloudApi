import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TabsModule } from 'primeng/tabs';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService } from '../../../core/services';
import { DeveloperInfo } from '../../../core/models';

interface CodeSample {
  language: string;
  code: string;
}

interface CategoryInfo {
  key: string;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-developer',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, ProgressSpinnerModule, TabsModule, ToastModule, TooltipModule, TranslateModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="space-y-6">
      <!-- Header -->
      <div>
        <h1 class="text-2xl font-bold text-slate-900 dark:text-white">{{ 'developer.title' | translate }}</h1>
        <p class="text-sm text-slate-500 mt-1">{{ 'developer.subtitle' | translate }}</p>
      </div>

      @if (loading()) {
        <div class="flex justify-center py-12"><p-progressSpinner [style]="{'width':'32px','height':'32px'}" strokeWidth="4" /></div>
      } @else if (error()) {
        <div class="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 rounded-2xl p-8 text-center space-y-4">
          <i class="pi pi-times-circle !text-[48px] !w-12 !h-12 text-red-400"></i>
          <p class="text-red-600 dark:text-red-400">{{ error()! | translate }}</p>
          <button pButton class="!bg-[#25D366] hover:!bg-[#128C7E] !text-white" (click)="loadInfo()">
            <i class="pi pi-refresh"></i>
            {{ 'developer.retry' | translate }}
          </button>
        </div>
      } @else if (info()) {
        <!-- API Info Cards -->
        <div class="grid md:grid-cols-3 gap-4">
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-5">
            <div class="flex items-center gap-3 mb-3">
              <div class="w-10 h-10 bg-emerald-100 dark:bg-emerald-900/30 rounded-xl flex items-center justify-center">
                <i class="pi pi-code !text-[20px] text-emerald-600"></i>
              </div>
              <h3 class="font-bold text-slate-900 dark:text-white">{{ 'developer.baseUrl' | translate }}</h3>
            </div>
            <div class="flex items-center gap-2">
              <code class="block flex-1 bg-slate-100 dark:bg-slate-700 rounded-xl p-3 text-sm text-emerald-600 dark:text-emerald-400 break-all">{{ info()!.baseUrl }}</code>
              <button pButton [text]="true" [rounded]="true" (click)="copyToClipboard(info()!.baseUrl)" class="shrink-0">
                <i class="pi pi-copy !text-[18px] text-slate-400 hover:text-emerald-500"></i>
              </button>
            </div>
          </div>
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-5">
            <div class="flex items-center gap-3 mb-3">
              <div class="w-10 h-10 bg-blue-100 dark:bg-blue-900/30 rounded-xl flex items-center justify-center">
                <i class="pi pi-check-circle !text-[20px] text-blue-600"></i>
              </div>
              <h3 class="font-bold text-slate-900 dark:text-white">{{ 'developer.version' | translate }}</h3>
            </div>
            <span class="text-2xl font-bold text-slate-900 dark:text-white">{{ info()!.version }}</span>
          </div>
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-5">
            <div class="flex items-center gap-3 mb-3">
              <div class="w-10 h-10 bg-purple-100 dark:bg-purple-900/30 rounded-xl flex items-center justify-center">
                <i class="pi pi-shield !text-[20px] text-purple-600"></i>
              </div>
              <h3 class="font-bold text-slate-900 dark:text-white">{{ 'developer.auth' | translate }}</h3>
            </div>
            <span class="text-sm text-slate-600 dark:text-slate-300">{{ info()!.authType }}</span>
          </div>
        </div>

        <!-- Stats bar -->
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-4 flex items-center gap-6 flex-wrap">
          <span class="text-sm text-slate-500">{{ 'developer.totalEndpoints' | translate }}:</span>
          <span class="text-lg font-bold text-slate-900 dark:text-white">{{ info()!.endpoints.length }}</span>
          <span class="text-slate-300 dark:text-slate-600">|</span>
          <span class="text-sm text-slate-500">{{ 'developer.showing' | translate }}:</span>
          <span class="text-lg font-bold text-emerald-600">{{ filteredEndpoints().length }}</span>
        </div>

        <!-- Search + Category Filter -->
        <div class="flex flex-wrap gap-3">
          <div class="relative flex-1 min-w-[240px]">
            <i class="pi pi-search absolute start-3 top-2.5 text-slate-400 !text-[16px]"></i>
            <input [(ngModel)]="searchQuery" [placeholder]="'developer.searchEndpoints' | translate"
              class="w-full ps-10 pe-4 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-sm outline-none focus:ring-2 focus:ring-emerald-500/30 text-slate-900 dark:text-white" />
          </div>
          <div class="flex gap-1.5 flex-wrap">
            <button (click)="filterCategory.set('all')"
              class="px-3 py-2 rounded-xl text-xs font-medium transition-all border"
              [class]="filterCategory() === 'all'
                ? 'bg-emerald-500 text-white border-emerald-500 shadow-sm'
                : 'bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:border-emerald-300'">
              {{ 'developer.allCategories' | translate }}
            </button>
            @for (cat of categoryList; track cat.key) {
              <button (click)="filterCategory.set(cat.key)"
                class="px-3 py-2 rounded-xl text-xs font-medium transition-all border flex items-center gap-1.5"
                [class]="filterCategory() === cat.key
                  ? 'bg-emerald-500 text-white border-emerald-500 shadow-sm'
                  : 'bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:border-emerald-300'">
                <i class="pi !text-[11px]" [class]="cat.icon"></i>
                {{ cat.key }}
              </button>
            }
          </div>
        </div>

        <!-- Endpoints -->
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 overflow-hidden">
          <div class="p-5 border-b border-slate-200 dark:border-slate-700/50">
            <h2 class="text-lg font-bold text-slate-900 dark:text-white">{{ 'developer.endpoints' | translate }}</h2>
            <p class="text-xs text-slate-500 mt-1">{{ 'developer.endpointsHint' | translate }}</p>
          </div>
          <div class="divide-y divide-slate-100 dark:divide-slate-700/30">
            @for (ep of filteredEndpoints(); track ep.path + ep.method; let i = $index) {
              <div class="cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-700/20 transition-colors"
                [class.bg-emerald-50]="selectedEndpointKey() === ep.path + ep.method"
                [class.dark:bg-emerald-900/10]="selectedEndpointKey() === ep.path + ep.method"
                (click)="toggleEndpoint(ep)">
                <div class="flex items-center gap-4 px-5 py-3">
                  <span class="px-2.5 py-1 text-xs font-bold rounded-lg shrink-0 min-w-[60px] text-center"
                    [class]="getMethodClass(ep.method)">{{ ep.method }}</span>
                  <code class="text-sm text-slate-700 dark:text-slate-300 flex-1 font-mono truncate">{{ ep.path }}</code>
                  @if (ep.category) {
                    <span class="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-500 dark:text-slate-400 font-medium hidden md:block">
                      {{ ep.category }}
                    </span>
                  }
                  <span class="text-xs text-slate-400 hidden lg:block max-w-[200px] truncate">{{ ep.description }}</span>
                  <i class="pi pi-chevron-down !text-[16px] text-slate-400 transition-transform"
                    [class.rotate-180]="selectedEndpointKey() === ep.path + ep.method"></i>
                </div>
                @if (selectedEndpointKey() === ep.path + ep.method) {
                  <div class="px-5 pb-5 border-t border-slate-100 dark:border-slate-700/30 bg-slate-50 dark:bg-slate-800/30" (click)="$event.stopPropagation()">
                    <div class="pt-4 space-y-4">
                      <div>
                        <div class="text-xs text-slate-500 uppercase tracking-wider font-semibold mb-1">{{ 'developer.description' | translate }}</div>
                        <p class="text-sm text-slate-700 dark:text-slate-300">{{ ep.description }}</p>
                      </div>
                      <div>
                        <div class="text-xs text-slate-500 uppercase tracking-wider font-semibold mb-1">{{ 'developer.fullUrl' | translate }}</div>
                        <div class="flex items-center gap-2">
                          <code class="text-xs bg-slate-200 dark:bg-slate-700 px-3 py-1.5 rounded-lg text-slate-600 dark:text-slate-300 font-mono flex-1 overflow-x-auto">
                            {{ ep.method }} {{ info()!.baseUrl }}{{ ep.path.replace('/api', '') }}
                          </code>
                          <button pButton [text]="true" [rounded]="true" (click)="copyToClipboard(info()!.baseUrl + ep.path.replace('/api', ''))" class="shrink-0">
                            <i class="pi pi-copy !text-[16px] text-slate-400 hover:text-emerald-500"></i>
                          </button>
                        </div>
                      </div>
                      <!-- Headers -->
                      <div>
                        <div class="text-xs text-slate-500 uppercase tracking-wider font-semibold mb-1">{{ 'developer.headers' | translate }}</div>
                        <div class="bg-white dark:bg-slate-800/50 rounded-lg border border-slate-200 dark:border-slate-700/30 p-3 space-y-1">
                          <code class="block text-xs text-slate-500 font-mono">Content-Type: application/json</code>
                          <code class="block text-xs text-slate-500 font-mono">Authorization: Bearer &lt;token&gt;</code>
                        </div>
                      </div>
                      <!-- Request Body -->
                      @if (ep.requestBody) {
                        <div>
                          <div class="text-xs text-slate-500 uppercase tracking-wider font-semibold mb-1">{{ 'developer.requestBody' | translate }}</div>
                          <div class="relative">
                            <pre class="bg-slate-900 rounded-xl p-3 overflow-x-auto"><code class="text-xs text-amber-400 whitespace-pre font-mono">{{ formatJson(ep.requestBody) }}</code></pre>
                            <button pButton [text]="true" [rounded]="true" (click)="copyToClipboard(ep.requestBody!)" class="!absolute !top-1 !right-1 shrink-0">
                              <i class="pi pi-copy !text-[14px] text-slate-500 hover:text-emerald-400"></i>
                            </button>
                          </div>
                        </div>
                      } @else if (ep.method === 'GET' || ep.method === 'DELETE') {
                        <div>
                          <div class="text-xs text-slate-500 uppercase tracking-wider font-semibold mb-1">{{ 'developer.requestBody' | translate }}</div>
                          <p class="text-xs text-slate-400 italic">{{ 'developer.noBody' | translate }}</p>
                        </div>
                      }
                      <!-- Response -->
                      <div>
                        <div class="text-xs text-slate-500 uppercase tracking-wider font-semibold mb-1">{{ 'developer.responseExample' | translate }}</div>
                        <pre class="bg-slate-900 rounded-xl p-3 overflow-x-auto"><code class="text-xs text-emerald-400 whitespace-pre font-mono">{{ getResponseExample(ep) }}</code></pre>
                      </div>
                    </div>
                  </div>
                }
              </div>
            }
          </div>
          @if (filteredEndpoints().length === 0) {
            <div class="text-center py-12 text-slate-400">
              <i class="pi pi-search !text-[32px] opacity-40 mb-2"></i>
              <p class="text-sm">{{ 'developer.noEndpointsFound' | translate }}</p>
            </div>
          }
        </div>

        <!-- Code Samples -->
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 overflow-hidden">
          <div class="p-5 border-b border-slate-200 dark:border-slate-700/50">
            <h2 class="text-lg font-bold text-slate-900 dark:text-white">{{ 'developer.codeSamples' | translate }}</h2>
            @if (selectedEp()) {
              <p class="text-xs text-emerald-600 dark:text-emerald-400 mt-1">
                {{ selectedEp()!.method }} {{ selectedEp()!.path }}
              </p>
            }
          </div>
          <p-tabs value="0">
            <p-tablist>
              @for (sample of currentCodeSamples(); track sample.language; let idx = $index) {
                <p-tab [value]="'' + idx">{{ sample.language }}</p-tab>
              }
            </p-tablist>
            <p-tabpanels>
              @for (sample of currentCodeSamples(); track sample.language; let idx = $index) {
                <p-tabpanel [value]="'' + idx">
                  <div class="p-5 relative">
                    <button pButton [text]="true" [rounded]="true" class="!absolute !top-2 !right-2" (click)="copyToClipboard(sample.code)">
                      <i class="pi pi-copy !text-[16px] text-slate-400 hover:text-emerald-400"></i>
                    </button>
                    <pre class="bg-slate-900 rounded-xl p-4 overflow-x-auto"><code class="text-sm text-emerald-400 whitespace-pre font-mono">{{ sample.code }}</code></pre>
                  </div>
                </p-tabpanel>
              }
            </p-tabpanels>
          </p-tabs>
        </div>
      }
    </div>
  `,
})
export class DeveloperComponent implements OnInit {
  private api = inject(ApiService);
  private messageService = inject(MessageService);

  loading = signal(true);
  info = signal<DeveloperInfo | null>(null);
  error = signal<string | null>(null);
  selectedEndpointKey = signal<string | null>(null);
  filterCategory = signal('all');
  searchQuery = '';

  readonly categoryList: CategoryInfo[] = [
    { key: 'Messaging', icon: 'pi-comments', color: 'emerald' },
    { key: 'Media', icon: 'pi-image', color: 'blue' },
    { key: 'Templates', icon: 'pi-file-edit', color: 'purple' },
    { key: 'Phone', icon: 'pi-phone', color: 'amber' },
    { key: 'Business', icon: 'pi-building', color: 'cyan' },
    { key: 'Webhooks', icon: 'pi-link', color: 'orange' },
    { key: 'Flows', icon: 'pi-sitemap', color: 'indigo' },
    { key: 'CRM', icon: 'pi-users', color: 'rose' },
    { key: 'Graph', icon: 'pi-globe', color: 'slate' },
    { key: 'Other', icon: 'pi-ellipsis-h', color: 'gray' },
  ];

  filteredEndpoints = computed(() => {
    const data = this.info();
    if (!data) return [];
    let list = data.endpoints;
    const cat = this.filterCategory();
    if (cat !== 'all') list = list.filter(ep => (ep.category || '').toLowerCase() === cat.toLowerCase());
    const q = this.searchQuery.toLowerCase();
    if (q) list = list.filter(ep => ep.path.toLowerCase().includes(q) || ep.description.toLowerCase().includes(q) || ep.method.toLowerCase().includes(q));
    return list;
  });

  selectedEp = computed(() => {
    const key = this.selectedEndpointKey();
    if (!key || !this.info()) return null;
    return this.info()!.endpoints.find(ep => ep.path + ep.method === key) || null;
  });

  currentCodeSamples = computed<CodeSample[]>(() => {
    const data = this.info();
    if (!data) return [];
    const ep = this.selectedEp();
    if (!ep) return data.codeSamples;

    const baseUrl = data.baseUrl;
    const fullUrl = baseUrl + ep.path.replace('/api', '');
    const isBodyMethod = ep.method === 'POST' || ep.method === 'PUT' || ep.method === 'PATCH';
    const bodyStr = ep.requestBody || (isBodyMethod ? '{ "key": "value" }' : '');

    return [
      { language: 'cURL', code: this.generateCurl(ep.method, fullUrl, isBodyMethod, bodyStr) },
      { language: 'JavaScript', code: this.generateJavaScript(ep.method, fullUrl, isBodyMethod, bodyStr) },
      { language: 'Python', code: this.generatePython(ep.method, fullUrl, isBodyMethod, bodyStr) },
      { language: 'C#', code: this.generateCSharp(ep.method, fullUrl, isBodyMethod, bodyStr) },
    ];
  });

  ngOnInit(): void {
    this.loadInfo();
  }

  loadInfo(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.get<DeveloperInfo>('/developer/info').subscribe({
      next: (data) => { this.info.set(data); this.loading.set(false); },
      error: (err) => {
        this.error.set(err?.status === 401 ? 'developer.errorUnauthorized' : 'developer.errorLoading');
        this.loading.set(false);
      },
    });
  }

  getMethodClass(method: string): string {
    return {
      GET: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300',
      POST: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300',
      PUT: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300',
      DELETE: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300',
      PATCH: 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-300',
      '*': 'bg-slate-100 text-slate-600 dark:bg-slate-700 dark:text-slate-300',
    }[method] || 'bg-slate-100 text-slate-600';
  }

  toggleEndpoint(ep: { path: string; method: string }): void {
    const key = ep.path + ep.method;
    this.selectedEndpointKey.set(this.selectedEndpointKey() === key ? null : key);
  }

  getResponseExample(ep: { method: string; path: string; description: string }): string {
    return JSON.stringify({
      success: true,
      message: null,
      data: ep.method === 'GET' ? { items: [], totalCount: 0 } : { id: '...', status: 'ok' },
      error: null,
      correlationId: 'abc-123',
    }, null, 2);
  }

  formatJson(json: string): string {
    try { return JSON.stringify(JSON.parse(json), null, 2); } catch { return json; }
  }

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(
      () => this.messageService.add({ severity: 'success', summary: 'Copied to clipboard!', life: 2000 }),
      () => this.messageService.add({ severity: 'error', summary: 'Failed to copy', life: 2000 }),
    );
  }

  // ─── Code Generation ─────────────────────
  private generateCurl(method: string, url: string, hasBody: boolean, body: string): string {
    let code = `curl -X ${method} "${url}" \\\n  -H "Authorization: Bearer YOUR_TOKEN" \\\n  -H "Content-Type: application/json"`;
    if (hasBody && body) code += ` \\\n  -d '${body}'`;
    return code;
  }

  private generateJavaScript(method: string, url: string, hasBody: boolean, body: string): string {
    const bodyStr = hasBody && body ? `\n  body: JSON.stringify(${body}),` : '';
    return `const response = await fetch("${url}", {
  method: "${method}",
  headers: {
    "Authorization": "Bearer YOUR_TOKEN",
    "Content-Type": "application/json",
  },${bodyStr}
});

const data = await response.json();
console.log(data);`;
  }

  private generatePython(method: string, url: string, hasBody: boolean, body: string): string {
    const bodyStr = hasBody && body ? `\npayload = ${body}\n` : '\n';
    const reqArgs = hasBody && body ? `json=payload, ` : '';
    return `import requests
${bodyStr}
response = requests.${method.toLowerCase()}(
    "${url}",
    ${reqArgs}headers={
        "Authorization": "Bearer YOUR_TOKEN",
        "Content-Type": "application/json",
    }
)

print(response.json())`;
  }

  private generateCSharp(method: string, url: string, hasBody: boolean, body: string): string {
    const bodyStr = hasBody && body
      ? `\nvar content = new StringContent(\n    @"${body.replace(/"/g, '""')}",\n    Encoding.UTF8, "application/json");\n`
      : '';
    const methodName = method.charAt(0) + method.slice(1).toLowerCase();
    const sendArg = hasBody && body
      ? `new HttpRequestMessage(HttpMethod.${methodName}, url) { Content = content }`
      : `new HttpRequestMessage(HttpMethod.${methodName}, url)`;

    return `using var client = new HttpClient();
client.DefaultRequestHeaders.Add("Authorization", "Bearer YOUR_TOKEN");

var url = "${url}";${bodyStr}
var response = await client.SendAsync(${sendArg});
var json = await response.Content.ReadAsStringAsync();
Console.WriteLine(json);`;
  }
}
