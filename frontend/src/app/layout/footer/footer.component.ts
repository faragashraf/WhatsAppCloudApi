import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { LogoComponent } from '../../shared/components/logo/logo.component';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [RouterLink, LogoComponent, TranslateModule],
  template: `
    <footer class="relative border-t border-[var(--app-border)] dark:border-slate-700/60 bg-[var(--app-surface)] dark:bg-slate-900/90 backdrop-blur-sm text-[var(--app-text-soft)] dark:text-slate-300 overflow-hidden">
      <div class="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_12%_8%,rgba(16,168,97,0.08),transparent_30%),radial-gradient(circle_at_88%_16%,rgba(13,139,202,0.08),transparent_28%)]"></div>

      <div class="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 sm:py-12">
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 sm:gap-8">
          <!-- Brand -->
          <div class="sm:col-span-2 lg:col-span-1">
            <div class="mb-4">
              <app-logo size="sm" [showText]="true" />
            </div>
            <p class="text-sm leading-relaxed text-[var(--app-text-soft)] dark:text-slate-300">
              {{ 'footer.description' | translate }}
            </p>
          </div>

          <!-- Product -->
          <div>
            <h4 class="text-[var(--app-text)] dark:text-slate-100 font-semibold text-sm mb-4 uppercase tracking-wider">{{ 'footer.product' | translate }}</h4>
            <ul class="space-y-2 list-none p-0 m-0">
              <li><a routerLink="/pricing" class="text-sm hover:text-[var(--app-primary)] transition-colors no-underline text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.pricing' | translate }}</a></li>
              <li><a routerLink="/about" class="text-sm hover:text-[var(--app-primary)] transition-colors no-underline text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.about' | translate }}</a></li>
              <li><a class="text-sm hover:text-[var(--app-primary)] transition-colors cursor-pointer text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.documentation' | translate }}</a></li>
              <li><a class="text-sm hover:text-[var(--app-primary)] transition-colors cursor-pointer text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.apiReference' | translate }}</a></li>
            </ul>
          </div>

          <!-- Company -->
          <div>
            <h4 class="text-[var(--app-text)] dark:text-slate-100 font-semibold text-sm mb-4 uppercase tracking-wider">{{ 'footer.company' | translate }}</h4>
            <ul class="space-y-2 list-none p-0 m-0">
              <li><a routerLink="/about" class="text-sm hover:text-[var(--app-primary)] transition-colors no-underline text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.aboutUs' | translate }}</a></li>
              <li><a routerLink="/contact" class="text-sm hover:text-[var(--app-primary)] transition-colors no-underline text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.contact' | translate }}</a></li>
              <li><a class="text-sm hover:text-[var(--app-primary)] transition-colors cursor-pointer text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.privacyPolicy' | translate }}</a></li>
              <li><a class="text-sm hover:text-[var(--app-primary)] transition-colors cursor-pointer text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.termsOfService' | translate }}</a></li>
            </ul>
          </div>

          <!-- Support -->
          <div>
            <h4 class="text-[var(--app-text)] dark:text-slate-100 font-semibold text-sm mb-4 uppercase tracking-wider">{{ 'footer.support' | translate }}</h4>
            <ul class="space-y-2 list-none p-0 m-0">
              <li><a class="text-sm hover:text-[var(--app-primary)] transition-colors cursor-pointer text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.helpCenter' | translate }}</a></li>
              <li><a class="text-sm hover:text-[var(--app-primary)] transition-colors cursor-pointer text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.status' | translate }}</a></li>
              <li><a class="text-sm hover:text-[var(--app-primary)] transition-colors cursor-pointer text-[var(--app-text-soft)] dark:text-slate-300">{{ 'footer.community' | translate }}</a></li>
            </ul>
          </div>
        </div>

        <div class="border-t border-[var(--app-border)] dark:border-slate-700/60 mt-8 sm:mt-10 pt-6 sm:pt-8 flex flex-col sm:flex-row justify-between items-center gap-4">
          <p class="text-xs text-[var(--app-text-muted)] dark:text-slate-400">&copy; {{ currentYear }} {{ 'footer.copyright' | translate }}</p>
          <div class="flex gap-4">
            <a class="text-[var(--app-text-muted)] dark:text-slate-400 hover:text-[var(--app-primary)] transition-colors cursor-pointer">
              <i class="pi pi-globe !text-[20px]"></i>
            </a>
          </div>
        </div>
      </div>
    </footer>
  `,
})
export class FooterComponent {
  readonly currentYear = new Date().getFullYear();
}

