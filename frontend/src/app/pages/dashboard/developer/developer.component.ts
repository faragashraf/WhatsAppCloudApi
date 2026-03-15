import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { finalize } from 'rxjs/operators';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TabsModule } from 'primeng/tabs';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ApiService, CompanyService } from '../../../core/services';
import { DeveloperInfo, WhatsAppConnectionStatus, WhatsAppPhoneNumber } from '../../../core/models';

interface CodeSample {
  language: string;
  code: string;
}

interface CategoryInfo {
  key: string;
  icon: string;
  color: string;
}

type DeveloperEndpoint = DeveloperInfo['endpoints'][number];
type RunnerMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

interface RunnerParam {
  key: string;
  value: string;
  enabled: boolean;
  required: boolean;
  isCatchAll: boolean;
}

interface RunnerContext {
  companyId: string;
  businessAccountId: string;
  phoneNumberId: string;
  whatsAppAccountId: string;
}

interface RunnerResponseHeader {
  key: string;
  value: string;
}

@Component({
  selector: 'app-developer',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, ProgressSpinnerModule, TabsModule, ToastModule, TooltipModule, TranslateModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="space-y-6 min-w-0 app-wrap-safe">
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
          <button pButton class="!bg-[var(--app-primary)] hover:!bg-[var(--app-primary-strong)] !text-white" (click)="loadInfo()">
            <i class="pi pi-refresh"></i>
            {{ 'developer.retry' | translate }}
          </button>
        </div>
      } @else if (info()) {
        <!-- API Info Cards -->
        <div class="grid lg:grid-cols-3 gap-4">
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
          <div class="relative w-full lg:flex-1 lg:min-w-[280px]">
            <i class="pi pi-search absolute start-3 top-2.5 text-slate-400 !text-[16px]"></i>
            <input [ngModel]="searchQuery()" (ngModelChange)="searchQuery.set($event)" [placeholder]="'developer.searchEndpoints' | translate"
              class="w-full ps-10 pe-4 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-sm outline-none focus:ring-2 focus:ring-emerald-500/30 text-slate-900 dark:text-white" />
          </div>
          <div class="flex gap-1.5 flex-wrap w-full lg:w-auto">
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

        <!-- API Runner -->
        <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 overflow-hidden">
          <div class="p-5 border-b border-slate-200 dark:border-slate-700/50 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 class="text-lg font-bold text-slate-900 dark:text-white">{{ 'developer.apiRunner' | translate }}</h2>
              <p class="text-xs text-slate-500 mt-1">{{ 'developer.apiRunnerHint' | translate }}</p>
            </div>
            <button pButton [outlined]="true" class="!rounded-xl !text-xs" (click)="applyAccountDefaultsToParams(true)">
              <i class="pi pi-sparkles"></i>
              {{ 'developer.useAccountDefaults' | translate }}
            </button>
          </div>

          <div class="p-5 space-y-4">
            <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
              <div class="space-y-4">
                <div>
                  <label class="block text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.selectEndpoint' | translate }}</label>
                  <select [ngModel]="runnerEndpointKey() || ''" (ngModelChange)="onRunnerEndpointChange($event)"
                    class="w-full px-3 py-2 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-emerald-500/30">
                    <option value="">{{ 'developer.chooseEndpoint' | translate }}</option>
                    @for (ep of info()!.endpoints; track ep.path + ep.method) {
                      <option [value]="ep.path + ep.method">{{ ep.method }} {{ ep.path }}</option>
                    }
                  </select>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-12 gap-2 items-end">
                  <div class="md:col-span-3">
                    <label class="block text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.httpMethod' | translate }}</label>
                    <select [ngModel]="runnerMethod()" (ngModelChange)="runnerMethod.set($event)"
                      class="w-full px-3 py-2 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-emerald-500/30">
                      @for (method of runnerMethods; track method) {
                        <option [value]="method">{{ method }}</option>
                      }
                    </select>
                  </div>
                  <div class="md:col-span-6">
                    <label class="block text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.requestPath' | translate }}</label>
                    <input [ngModel]="runnerPathTemplate()" (ngModelChange)="onRunnerPathTemplateChange($event)"
                      class="w-full px-3 py-2 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-emerald-500/30 font-mono"
                      placeholder="/whatsapp/messages/text" />
                  </div>
                  <div class="md:col-span-3">
                    <button pButton class="!bg-emerald-600 hover:!bg-emerald-700 !text-white !rounded-xl w-full" (click)="executeRunnerRequest()" [disabled]="runnerExecuting()">
                      @if (runnerExecuting()) {
                        <p-progressSpinner [style]="{'width':'14px','height':'14px'}" strokeWidth="4" class="!inline-block me-2" />
                        {{ 'developer.running' | translate }}
                      } @else {
                        <i class="pi pi-play"></i>
                        {{ 'developer.runRequest' | translate }}
                      }
                    </button>
                  </div>
                </div>

                <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 bg-slate-50 dark:bg-slate-900/30 p-3">
                  <div class="text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-2">{{ 'developer.accountDefaults' | translate }}</div>
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs">
                    <div class="rounded-lg border border-slate-200 dark:border-slate-700/50 bg-white dark:bg-slate-800/70 p-2">
                      <div class="text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.companyId' | translate }}</div>
                      <code class="text-slate-700 dark:text-slate-200 break-all">{{ runnerContext().companyId || ('developer.notAvailable' | translate) }}</code>
                    </div>
                    <div class="rounded-lg border border-slate-200 dark:border-slate-700/50 bg-white dark:bg-slate-800/70 p-2">
                      <div class="text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.businessAccountId' | translate }}</div>
                      <code class="text-slate-700 dark:text-slate-200 break-all">{{ runnerContext().businessAccountId || ('developer.notAvailable' | translate) }}</code>
                    </div>
                    <div class="rounded-lg border border-slate-200 dark:border-slate-700/50 bg-white dark:bg-slate-800/70 p-2">
                      <div class="text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.phoneNumberId' | translate }}</div>
                      <code class="text-slate-700 dark:text-slate-200 break-all">{{ runnerContext().phoneNumberId || ('developer.notAvailable' | translate) }}</code>
                    </div>
                    <div class="rounded-lg border border-slate-200 dark:border-slate-700/50 bg-white dark:bg-slate-800/70 p-2">
                      <div class="text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.whatsappAccountId' | translate }}</div>
                      <code class="text-slate-700 dark:text-slate-200 break-all">{{ runnerContext().whatsAppAccountId || ('developer.notAvailable' | translate) }}</code>
                    </div>
                  </div>
                </div>
              </div>

              <div class="space-y-4">
                <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 p-3">
                  <div class="text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-2">{{ 'developer.pathParameters' | translate }}</div>
                  @if (runnerPathParams.length > 0) {
                    <div class="space-y-2">
                      @for (param of runnerPathParams; track param.key; let idx = $index) {
                        <div class="grid grid-cols-1 sm:grid-cols-12 gap-2 items-center">
                          <input [value]="param.key" disabled class="sm:col-span-4 px-2.5 py-2 rounded-lg bg-slate-100 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 text-xs text-slate-500 dark:text-slate-300" />
                          <input [ngModel]="param.value" (ngModelChange)="updatePathParamValue(idx, $event)"
                            [placeholder]="'developer.parameterValue' | translate"
                            class="sm:col-span-8 px-2.5 py-2 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-emerald-500/30" />
                        </div>
                      }
                    </div>
                  } @else {
                    <p class="text-xs text-slate-400">{{ 'developer.noPathParameters' | translate }}</p>
                  }
                </div>

                <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 p-3">
                  <div class="flex items-center justify-between gap-2 mb-2">
                    <div class="text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400">{{ 'developer.queryParameters' | translate }}</div>
                    <button pButton [text]="true" class="!text-xs" (click)="addQueryParam()">
                      <i class="pi pi-plus"></i>
                      {{ 'developer.addQueryParam' | translate }}
                    </button>
                  </div>
                  @if (runnerQueryParams.length > 0) {
                    <div class="space-y-2">
                      @for (param of runnerQueryParams; track $index; let idx = $index) {
                        <div class="grid grid-cols-12 gap-2 items-center">
                          <div class="col-span-1 flex justify-center">
                            <input type="checkbox" [ngModel]="param.enabled" (ngModelChange)="updateQueryParamEnabled(idx, $event)" class="h-4 w-4 accent-emerald-600" />
                          </div>
                          <input [ngModel]="param.key" (ngModelChange)="updateQueryParamKey(idx, $event)"
                            [placeholder]="'developer.parameterName' | translate"
                            class="col-span-4 px-2.5 py-2 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-emerald-500/30" />
                          <input [ngModel]="param.value" (ngModelChange)="updateQueryParamValue(idx, $event)"
                            [placeholder]="'developer.parameterValue' | translate"
                            class="col-span-6 px-2.5 py-2 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-emerald-500/30" />
                          <button pButton [text]="true" class="col-span-1 !text-red-500" (click)="removeQueryParam(idx)">
                            <i class="pi pi-trash"></i>
                          </button>
                        </div>
                      }
                    </div>
                  } @else {
                    <p class="text-xs text-slate-400">{{ 'developer.noQueryParameters' | translate }}</p>
                  }
                </div>
              </div>
            </div>

            <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 p-3 space-y-2">
              <div class="flex items-center justify-between gap-2">
                <div class="text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {{ isRunnerMultipartUpload() ? ('developer.requestBodyForm' | translate) : ('developer.requestBodyEditor' | translate) }}
                </div>
                @if (!methodSupportsBody(runnerMethod()) && !isRunnerMultipartUpload()) {
                  <span class="text-[11px] text-slate-400">{{ 'developer.noBody' | translate }}</span>
                }
              </div>
              @if (isRunnerMultipartUpload()) {
                <div class="space-y-3">
                  <p class="text-[11px] text-slate-500 dark:text-slate-400">{{ 'developer.multipartHint' | translate }}</p>
                  <div>
                    <label class="block text-[11px] text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.uploadFile' | translate }}</label>
                    <input type="file" (change)="onRunnerFileChange($event)"
                      class="block w-full text-sm text-slate-700 dark:text-slate-200 file:me-3 file:px-3 file:py-1.5 file:rounded-lg file:border-0 file:bg-emerald-50 file:text-emerald-700 dark:file:bg-emerald-900/30 dark:file:text-emerald-300" />
                  </div>
                  @if (runnerFile()) {
                    <div class="text-[11px] text-slate-500 dark:text-slate-400">
                      {{ 'developer.selectedFile' | translate }}: <span class="font-medium text-slate-700 dark:text-slate-200">{{ runnerFile()!.name }}</span>
                      ({{ runnerFile()!.size }} B)
                    </div>
                  }
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
                    <div>
                      <label class="block text-[11px] text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.fileNameOverride' | translate }}</label>
                      <input [ngModel]="runnerFileNameOverride()" (ngModelChange)="runnerFileNameOverride.set($event)"
                        class="w-full px-3 py-2 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-emerald-500/30" />
                    </div>
                    <div>
                      <label class="block text-[11px] text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.contentTypeOverride' | translate }}</label>
                      <input [ngModel]="runnerFileContentTypeOverride()" (ngModelChange)="runnerFileContentTypeOverride.set($event)"
                        class="w-full px-3 py-2 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-emerald-500/30"
                        placeholder="image/png" />
                    </div>
                  </div>
                </div>
              } @else if (methodSupportsBody(runnerMethod())) {
                <textarea [ngModel]="runnerBody()" (ngModelChange)="runnerBody.set($event)" rows="8"
                  class="w-full px-3 py-2 rounded-xl bg-slate-900 border border-slate-700 text-xs text-emerald-300 outline-none focus:ring-2 focus:ring-emerald-500/30 font-mono"
                  spellcheck="false"></textarea>
                <p class="text-[11px] text-slate-500 dark:text-slate-400">{{ 'developer.bodyTemplateHint' | translate }}</p>
              }
            </div>

            <div class="rounded-xl border border-slate-200 dark:border-slate-700/50 p-3 space-y-3">
              <div class="flex items-center justify-between gap-2">
                <h3 class="text-sm font-semibold text-slate-900 dark:text-white">{{ 'developer.response' | translate }}</h3>
                <button pButton [outlined]="true" size="small" class="!rounded-lg !text-xs" (click)="clearRunnerResponse()">
                  {{ 'developer.clearResponse' | translate }}
                </button>
              </div>

              <div class="flex flex-wrap items-center gap-2 text-xs">
                <span class="px-2 py-1 rounded-lg border" [class]="getRunnerStatusClass(runnerResponseStatus())">
                  {{ 'developer.status' | translate }}: {{ runnerResponseStatus() ?? '-' }}
                </span>
                <span class="px-2 py-1 rounded-lg border border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300">
                  {{ 'developer.duration' | translate }}: {{ runnerResponseDurationMs() ?? 0 }} ms
                </span>
              </div>

              @if (runnerLastUrl()) {
                <div>
                  <div class="text-xs text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.executedUrl' | translate }}</div>
                  <code class="block text-xs bg-slate-100 dark:bg-slate-800 px-3 py-2 rounded-lg text-slate-700 dark:text-slate-300 break-all">{{ runnerLastUrl() }}</code>
                </div>
              }

              @if (runnerResponseError()) {
                <div class="text-xs text-red-600 dark:text-red-400">{{ runnerResponseError() }}</div>
              }

              @if (runnerResponseHeaders().length > 0) {
                <div>
                  <div class="text-xs text-slate-500 dark:text-slate-400 mb-1">{{ 'developer.responseHeaders' | translate }}</div>
                  <div class="rounded-lg border border-slate-200 dark:border-slate-700/50 p-2 bg-slate-50 dark:bg-slate-900/30 space-y-1 max-h-36 overflow-auto">
                    @for (header of runnerResponseHeaders(); track header.key) {
                      <code class="block text-[11px] text-slate-600 dark:text-slate-300 break-all">{{ header.key }}: {{ header.value }}</code>
                    }
                  </div>
                </div>
              }

              @if (runnerResponseBody()) {
                <pre class="bg-slate-900 rounded-xl p-3 overflow-x-auto app-code-block"><code class="text-xs text-emerald-300 whitespace-pre font-mono">{{ runnerResponseBody() }}</code></pre>
              } @else if (!runnerExecuting() && !runnerResponseError()) {
                <p class="text-xs text-slate-400">{{ 'developer.noResponseYet' | translate }}</p>
              }
            </div>
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
                    <span class="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-500 dark:text-slate-400 font-medium hidden lg:block">
                      {{ ep.category }}
                    </span>
                  }
                  <span class="text-xs text-slate-400 hidden 2xl:block max-w-[200px] truncate">{{ ep.description }}</span>
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
                            <pre class="bg-slate-900 rounded-xl p-3 overflow-x-auto app-code-block"><code class="text-xs text-amber-400 whitespace-pre font-mono">{{ formatJson(ep.requestBody) }}</code></pre>
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
                        <pre class="bg-slate-900 rounded-xl p-3 overflow-x-auto app-code-block"><code class="text-xs text-emerald-400 whitespace-pre font-mono">{{ getResponseExample(ep) }}</code></pre>
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
                    <pre class="bg-slate-900 rounded-xl p-4 overflow-x-auto app-code-block"><code class="text-sm text-emerald-400 whitespace-pre font-mono">{{ sample.code }}</code></pre>
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
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly companyService = inject(CompanyService);
  private readonly messageService = inject(MessageService);
  private readonly translate = inject(TranslateService);

  loading = signal(true);
  info = signal<DeveloperInfo | null>(null);
  error = signal<string | null>(null);
  selectedEndpointKey = signal<string | null>(null);
  filterCategory = signal('all');
  searchQuery = signal('');

  readonly runnerMethods: RunnerMethod[] = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'];
  runnerEndpointKey = signal<string | null>(null);
  runnerMethod = signal<RunnerMethod>('GET');
  runnerPathTemplate = signal('');
  runnerBody = signal('');
  runnerPathParams: RunnerParam[] = [];
  runnerQueryParams: RunnerParam[] = [];
  runnerExecuting = signal(false);
  runnerResponseStatus = signal<number | null>(null);
  runnerResponseDurationMs = signal<number | null>(null);
  runnerResponseBody = signal('');
  runnerResponseHeaders = signal<RunnerResponseHeader[]>([]);
  runnerResponseError = signal<string | null>(null);
  runnerLastUrl = signal('');
  runnerFile = signal<File | null>(null);
  runnerFileNameOverride = signal('');
  runnerFileContentTypeOverride = signal('');
  runnerContext = signal<RunnerContext>({
    companyId: '',
    businessAccountId: '',
    phoneNumberId: '',
    whatsAppAccountId: '',
  });
  isRunnerMultipartUpload = computed(() => this.isMultipartUploadEndpoint(this.runnerPathTemplate(), this.runnerMethod()));

  private connectionStatusCache: WhatsAppConnectionStatus | null = null;
  private phoneNumbersCache: WhatsAppPhoneNumber[] = [];

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
    const q = this.searchQuery().trim().toLowerCase();
    if (q) list = list.filter(ep => ep.path.toLowerCase().includes(q) || ep.description.toLowerCase().includes(q) || ep.method.toLowerCase().includes(q));
    return list;
  });

  selectedEp = computed(() => {
    const key = this.selectedEndpointKey();
    const data = this.info();
    if (!key || !data) return null;
    return data.endpoints.find(ep => this.endpointKey(ep) === key) || null;
  });

  currentCodeSamples = computed<CodeSample[]>(() => {
    const data = this.info();
    if (!data) return [];
    const ep = this.selectedEp();
    if (!ep) return data.codeSamples;

    const method = this.normalizeHttpMethod(ep.method);
    const fullUrl = data.baseUrl + this.normalizeEndpointPath(ep.path);
    const isBodyMethod = this.methodSupportsBody(method) && !!ep.requestBody;
    const bodyStr = ep.requestBody || (isBodyMethod ? '{ "key": "value" }' : '');

    return [
      { language: 'cURL', code: this.generateCurl(method, fullUrl, isBodyMethod, bodyStr) },
      { language: 'JavaScript', code: this.generateJavaScript(method, fullUrl, isBodyMethod, bodyStr) },
      { language: 'Python', code: this.generatePython(method, fullUrl, isBodyMethod, bodyStr) },
      { language: 'C#', code: this.generateCSharp(method, fullUrl, isBodyMethod, bodyStr) },
    ];
  });

  ngOnInit(): void {
    this.loadInfo();
    this.loadRunnerAccountDefaults();
  }

  loadInfo(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.get<DeveloperInfo>('/developer/info').subscribe({
      next: (data) => {
        this.info.set(data);
        this.loading.set(false);
        this.runnerContext.update(ctx => ({ ...ctx, companyId: String(data.companyId ?? '') }));
        if (!this.runnerEndpointKey() && data.endpoints.length > 0) {
          this.configureRunnerForEndpoint(data.endpoints[0]);
        }
      },
      error: (err) => {
        this.error.set(err?.status === 401 ? 'developer.errorUnauthorized' : 'developer.errorLoading');
        this.loading.set(false);
      },
    });
  }

  private loadRunnerAccountDefaults(): void {
    this.companyService.loadConnectionStatus().subscribe((status) => {
      this.connectionStatusCache = status;
      this.refreshRunnerContext();
    });

    this.companyService.loadPhoneNumbers().subscribe((phones) => {
      this.phoneNumbersCache = phones || [];
      this.refreshRunnerContext();
    });
  }

  private refreshRunnerContext(): void {
    const firstMetaPhoneId = this.connectionStatusCache?.phoneNumbers?.[0]?.phoneNumberId || '';
    const firstStoredPhone = this.phoneNumbersCache[0] || null;

    this.runnerContext.update(ctx => ({
      ...ctx,
      businessAccountId: this.connectionStatusCache?.businessAccountId || '',
      phoneNumberId: firstMetaPhoneId || firstStoredPhone?.phoneNumberId || '',
      whatsAppAccountId: firstStoredPhone?.whatsAppAccountId || '',
    }));

    this.applyAccountDefaultsToParams(false);
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

  getRunnerStatusClass(status: number | null): string {
    if (status === null) return 'border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300';
    if (status >= 200 && status < 300) return 'border-emerald-200 dark:border-emerald-700/50 text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-900/20';
    if (status >= 400 && status < 500) return 'border-amber-200 dark:border-amber-700/50 text-amber-700 dark:text-amber-300 bg-amber-50 dark:bg-amber-900/20';
    if (status >= 500) return 'border-red-200 dark:border-red-700/50 text-red-700 dark:text-red-300 bg-red-50 dark:bg-red-900/20';
    return 'border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300';
  }

  toggleEndpoint(ep: DeveloperEndpoint): void {
    const key = this.endpointKey(ep);
    const isOpen = this.selectedEndpointKey() === key;
    this.selectedEndpointKey.set(isOpen ? null : key);
    if (!isOpen) this.configureRunnerForEndpoint(ep);
  }

  onRunnerEndpointChange(key: string): void {
    if (!key) {
      this.runnerEndpointKey.set(null);
      return;
    }

    const endpoint = this.info()?.endpoints.find(ep => this.endpointKey(ep) === key);
    if (!endpoint) return;

    this.configureRunnerForEndpoint(endpoint);
    this.selectedEndpointKey.set(key);
  }

  onRunnerPathTemplateChange(path: string): void {
    this.runnerPathTemplate.set(path);
    this.rebuildPathParams(path);
    if (!this.isMultipartUploadEndpoint(path, this.runnerMethod())) {
      this.clearRunnerFileSelection();
    }
  }

  addQueryParam(): void {
    this.runnerQueryParams = [...this.runnerQueryParams, { key: '', value: '', enabled: true, required: false, isCatchAll: false }];
  }

  removeQueryParam(index: number): void {
    this.runnerQueryParams = this.runnerQueryParams.filter((_, idx) => idx !== index);
  }

  updateQueryParamEnabled(index: number, enabled: boolean): void {
    this.runnerQueryParams = this.runnerQueryParams.map((param, idx) => idx === index ? { ...param, enabled } : param);
  }

  updateQueryParamKey(index: number, key: string): void {
    this.runnerQueryParams = this.runnerQueryParams.map((param, idx) => idx === index ? { ...param, key } : param);
  }

  updateQueryParamValue(index: number, value: string): void {
    this.runnerQueryParams = this.runnerQueryParams.map((param, idx) => idx === index ? { ...param, value } : param);
  }

  updatePathParamValue(index: number, value: string): void {
    this.runnerPathParams = this.runnerPathParams.map((param, idx) => idx === index ? { ...param, value } : param);
  }

  onRunnerFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input?.files?.[0] ?? null;
    this.runnerFile.set(file);
    if (!file) return;

    if (!this.runnerFileNameOverride().trim()) {
      this.runnerFileNameOverride.set(file.name || '');
    }

    if (!this.runnerFileContentTypeOverride().trim() && file.type) {
      this.runnerFileContentTypeOverride.set(file.type);
    }
  }

  applyAccountDefaultsToParams(overwriteExisting: boolean): void {
    this.runnerPathParams = this.runnerPathParams.map((param) => {
      const value = this.getDefaultParamValue(param.key);
      if (!value) return param;
      if (overwriteExisting || !param.value.trim()) return { ...param, value };
      return param;
    });

    this.runnerQueryParams = this.runnerQueryParams.map((param) => {
      const value = this.getDefaultParamValue(param.key);
      if (!value) return param;
      if (overwriteExisting || !param.value.trim()) return { ...param, value };
      return param;
    });
  }

  methodSupportsBody(method: string): boolean {
    return method !== 'GET';
  }

  executeRunnerRequest(): void {
    const data = this.info();
    if (!data) {
      this.messageService.add({ severity: 'warn', summary: this.translate.instant('developer.endpointRequired'), life: 2500 });
      return;
    }

    const method = this.runnerMethod();
    const path = this.normalizeRunnerPath(this.runnerPathTemplate());
    if (!path) {
      this.messageService.add({ severity: 'warn', summary: this.translate.instant('developer.endpointRequired'), life: 2500 });
      return;
    }

    const resolvedPath = this.resolvePathWithParams(path);
    if (resolvedPath.missing.length > 0) {
      this.messageService.add({
        severity: 'warn',
        summary: this.translate.instant('developer.missingPathParam', { name: resolvedPath.missing[0] }),
        life: 3000,
      });
      return;
    }

    const params = this.buildQueryParams();
    const requestUrl = `${data.baseUrl}${resolvedPath.path}`;

    const isMultipartUpload = this.isMultipartUploadEndpoint(path, method);
    let requestBody: unknown = undefined;
    let requestHeaders: HttpHeaders | undefined;

    if (isMultipartUpload) {
      const selectedFile = this.runnerFile();
      if (!selectedFile) {
        this.messageService.add({ severity: 'warn', summary: this.translate.instant('developer.fileRequired'), life: 3000 });
        return;
      }

      const formData = new FormData();
      const fileName = this.applyContextPlaceholders(this.runnerFileNameOverride().trim()) || selectedFile.name || 'upload.bin';
      const contentType = this.applyContextPlaceholders(this.runnerFileContentTypeOverride().trim());

      formData.append('file', selectedFile, fileName);
      formData.append('fileName', fileName);
      if (contentType) {
        formData.append('contentType', contentType);
      }

      requestBody = formData;
    } else if (this.methodSupportsBody(method)) {
      const rawBody = this.applyContextPlaceholders(this.runnerBody().trim());
      if (rawBody) {
        try {
          requestBody = JSON.parse(rawBody);
          requestHeaders = new HttpHeaders({ 'Content-Type': 'application/json' });
        } catch {
          this.messageService.add({ severity: 'error', summary: this.translate.instant('developer.invalidJsonBody'), life: 3500 });
          return;
        }
      }
    }

    this.runnerExecuting.set(true);
    this.runnerResponseStatus.set(null);
    this.runnerResponseDurationMs.set(null);
    this.runnerResponseHeaders.set([]);
    this.runnerResponseBody.set('');
    this.runnerResponseError.set(null);
    this.runnerLastUrl.set(this.appendQueryPreview(requestUrl, params));

    const startedAt = performance.now();
    const options: { observe: 'response'; responseType: 'text'; params: HttpParams; body?: unknown; headers?: HttpHeaders; } = {
      observe: 'response',
      responseType: 'text',
      params,
    };

    if (requestBody !== undefined) {
      options.body = requestBody;
      if (requestHeaders) {
        options.headers = requestHeaders;
      }
    }

    this.http.request(method, requestUrl, options)
      .pipe(finalize(() => this.runnerExecuting.set(false)))
      .subscribe({
        next: (response: HttpResponse<string>) => {
          this.runnerResponseStatus.set(response.status);
          this.runnerResponseDurationMs.set(Math.round(performance.now() - startedAt));
          this.runnerResponseHeaders.set(this.headersToArray(response.headers));
          this.runnerResponseBody.set(this.formatJson(response.body || ''));
        },
        error: (err: HttpErrorResponse) => {
          this.runnerResponseStatus.set(err.status || null);
          this.runnerResponseDurationMs.set(Math.round(performance.now() - startedAt));
          this.runnerResponseHeaders.set(this.headersToArray(err.headers));
          this.runnerResponseError.set(err.message || this.translate.instant('developer.runnerFailed'));
          this.runnerResponseBody.set(this.formatJson(this.extractErrorBody(err)));
        },
      });
  }

  clearRunnerResponse(): void {
    this.runnerResponseStatus.set(null);
    this.runnerResponseDurationMs.set(null);
    this.runnerResponseHeaders.set([]);
    this.runnerResponseBody.set('');
    this.runnerResponseError.set(null);
    this.runnerLastUrl.set('');
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
    if (!json || !json.trim()) return '';
    try { return JSON.stringify(JSON.parse(json), null, 2); } catch { return json; }
  }

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(
      () => this.messageService.add({ severity: 'success', summary: this.translate.instant('developer.copiedToClipboard'), life: 2000 }),
      () => this.messageService.add({ severity: 'error', summary: this.translate.instant('developer.failedToCopy'), life: 2000 }),
    );
  }

  private endpointKey(ep: DeveloperEndpoint): string {
    return `${ep.path}${ep.method}`;
  }

  private configureRunnerForEndpoint(endpoint: DeveloperEndpoint): void {
    this.runnerEndpointKey.set(this.endpointKey(endpoint));
    this.runnerMethod.set(this.normalizeHttpMethod(endpoint.method));

    const path = this.normalizeEndpointPath(endpoint.path);
    this.runnerPathTemplate.set(path);
    this.rebuildPathParams(path);
    this.runnerQueryParams = [];
    if (!this.isMultipartUploadEndpoint(path, this.runnerMethod())) {
      this.clearRunnerFileSelection();
    }

    if (endpoint.requestBody && this.methodSupportsBody(this.runnerMethod())) {
      this.runnerBody.set(this.formatJson(endpoint.requestBody));
    } else if (this.methodSupportsBody(this.runnerMethod())) {
      this.runnerBody.set('{\n  \n}');
    } else {
      this.runnerBody.set('');
    }
  }

  private normalizeEndpointPath(path: string): string {
    const trimmed = path.trim();
    if (trimmed.startsWith('/api/')) return trimmed.slice(4);
    if (trimmed === '/api') return '/';
    return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
  }

  private normalizeRunnerPath(path: string): string {
    const trimmed = path.trim();
    if (!trimmed) return '';
    if (trimmed.startsWith('/api/')) return trimmed.slice(4);
    if (trimmed === '/api') return '/';
    return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
  }

  private rebuildPathParams(path: string): void {
    const extracted = this.extractPathParams(path);
    if (extracted.length === 0) {
      this.runnerPathParams = [];
      return;
    }

    const existingMap = new Map(this.runnerPathParams.map(param => [param.key.toLowerCase(), param]));
    this.runnerPathParams = extracted.map((param) => {
      const existing = existingMap.get(param.key.toLowerCase());
      return existing ? { ...param, value: existing.value } : param;
    });

    this.applyAccountDefaultsToParams(false);
  }

  private extractPathParams(path: string): RunnerParam[] {
    const matches = [...path.matchAll(/\{([^}]+)\}/g)];
    const seen = new Set<string>();
    const params: RunnerParam[] = [];

    for (const match of matches) {
      const raw = (match[1] || '').trim();
      if (!raw) continue;

      const isCatchAll = raw.startsWith('*');
      const key = raw.replace(/^\*/, '').trim();
      if (!key) continue;

      const normalized = key.toLowerCase();
      if (seen.has(normalized)) continue;
      seen.add(normalized);

      params.push({
        key,
        value: this.getDefaultParamValue(key),
        enabled: true,
        required: true,
        isCatchAll,
      });
    }

    return params;
  }

  private getDefaultParamValue(paramName: string): string {
    const name = paramName.toLowerCase();
    const context = this.runnerContext();

    if (name === 'companyid' || name === 'company_id') return context.companyId;
    if (name.includes('business') && name.includes('id')) return context.businessAccountId;
    if (name.includes('phone') && name.includes('id')) return context.phoneNumberId;
    if ((name.includes('whatsapp') || name.includes('waba')) && name.includes('id')) return context.whatsAppAccountId;
    if (name.includes('account') && name.includes('id')) return context.whatsAppAccountId;
    return '';
  }

  private resolvePathWithParams(pathTemplate: string): { path: string; missing: string[] } {
    let resolved = pathTemplate;
    const missing: string[] = [];

    for (const param of this.runnerPathParams) {
      const currentValue = this.applyContextPlaceholders((param.value || '').trim());
      if (!currentValue) {
        missing.push(param.key);
        continue;
      }

      const encoded = param.isCatchAll ? encodeURI(currentValue) : encodeURIComponent(currentValue);
      const pattern = new RegExp(`\\{\\*?${this.escapeRegExp(param.key)}\\}`, 'g');
      resolved = resolved.replace(pattern, encoded);
    }

    const unresolved = [...resolved.matchAll(/\{([^}]+)\}/g)]
      .map(match => (match[1] || '').replace(/^\*/, '').trim())
      .filter(Boolean);

    for (const key of unresolved) {
      if (!missing.includes(key)) missing.push(key);
    }

    return { path: resolved, missing };
  }

  private buildQueryParams(): HttpParams {
    let params = new HttpParams();
    for (const param of this.runnerQueryParams) {
      if (!param.enabled) continue;
      const key = (param.key || '').trim();
      if (!key) continue;
      params = params.set(key, this.applyContextPlaceholders((param.value || '').trim()));
    }
    return params;
  }

  private appendQueryPreview(url: string, params: HttpParams): string {
    const query = params.toString();
    return query ? `${url}?${query}` : url;
  }

  private isMultipartUploadEndpoint(path: string, method: string): boolean {
    const normalizedPath = this.normalizeRunnerPath(path).toLowerCase();
    const normalizedMethod = (method || '').toUpperCase();
    return normalizedMethod === 'POST' && normalizedPath === '/whatsapp/media/upload/file';
  }

  private clearRunnerFileSelection(): void {
    this.runnerFile.set(null);
    this.runnerFileNameOverride.set('');
    this.runnerFileContentTypeOverride.set('');
  }

  private headersToArray(headers: HttpHeaders): RunnerResponseHeader[] {
    if (!headers) return [];
    return headers.keys().map((key) => ({
      key,
      value: headers.get(key) || '',
    }));
  }

  private applyContextPlaceholders(text: string): string {
    if (!text) return text;

    const ctx = this.runnerContext();
    const replacements: Record<string, string> = {
      companyId: ctx.companyId,
      businessAccountId: ctx.businessAccountId,
      phoneNumberId: ctx.phoneNumberId,
      whatsAppAccountId: ctx.whatsAppAccountId,
      whatsappAccountId: ctx.whatsAppAccountId,
    };

    let result = text;
    for (const [placeholder, value] of Object.entries(replacements)) {
      const regex = new RegExp(`\\{\\{\\s*${this.escapeRegExp(placeholder)}\\s*\\}\\}`, 'gi');
      result = result.replace(regex, value || '');
    }

    return result;
  }

  private extractErrorBody(err: HttpErrorResponse): string {
    if (typeof err.error === 'string') return err.error;
    if (err.error && typeof err.error === 'object') {
      try { return JSON.stringify(err.error); } catch { return String(err.error); }
    }
    return err.message || this.translate.instant('developer.runnerFailed');
  }

  private escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  private normalizeHttpMethod(method: string): RunnerMethod {
    const upper = (method || '').toUpperCase();
    if (upper === 'POST' || upper === 'PUT' || upper === 'PATCH' || upper === 'DELETE') return upper;
    return 'GET';
  }

  // --- Code Generation ---------------------
  private generateCurl(method: RunnerMethod, url: string, hasBody: boolean, body: string): string {
    let code = `curl -X ${method} "${url}" \\\n  -H "Authorization: Bearer YOUR_TOKEN" \\\n  -H "Content-Type: application/json"`;
    if (hasBody && body) code += ` \\\n  -d '${body}'`;
    return code;
  }

  private generateJavaScript(method: RunnerMethod, url: string, hasBody: boolean, body: string): string {
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

  private generatePython(method: RunnerMethod, url: string, hasBody: boolean, body: string): string {
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

  private generateCSharp(method: RunnerMethod, url: string, hasBody: boolean, body: string): string {
    const bodyStr = hasBody && body
      ? `\nvar content = new StringContent(\n    @"${body.replace(/"/g, '""')}",\n    Encoding.UTF8, "application/json");\n`
      : '';

    const methodMap: Record<RunnerMethod, string> = {
      GET: 'Get',
      POST: 'Post',
      PUT: 'Put',
      PATCH: 'Patch',
      DELETE: 'Delete',
    };
    const methodName = methodMap[method];

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

