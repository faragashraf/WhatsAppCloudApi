import { Injectable, signal, effect, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { TranslateService } from '@ngx-translate/core';

export type Lang = 'ar' | 'en';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly currentLang = signal<Lang>(this.loadLang());
  readonly isRtl = signal(this.loadLang() === 'ar');
  readonly dir = signal<'rtl' | 'ltr'>(this.loadLang() === 'ar' ? 'rtl' : 'ltr');

  constructor() {
    this.translate.addLangs(['ar', 'en']);
    this.translate.setDefaultLang('ar');
    this.translate.use(this.currentLang());

    effect(() => {
      const lang = this.currentLang();
      this.isRtl.set(lang === 'ar');
      this.dir.set(lang === 'ar' ? 'rtl' : 'ltr');

      if (isPlatformBrowser(this.platformId)) {
        document.documentElement.lang = lang;
        document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
        document.documentElement.classList.toggle('rtl', lang === 'ar');
        document.documentElement.classList.toggle('ltr', lang !== 'ar');
        localStorage.setItem('wa-lang', lang);
      }

      this.translate.use(lang);
    });
  }

  toggle(): void {
    this.currentLang.set(this.currentLang() === 'ar' ? 'en' : 'ar');
  }

  setLang(lang: Lang): void {
    this.currentLang.set(lang);
  }

  private loadLang(): Lang {
    if (isPlatformBrowser(this.platformId)) {
      const stored = localStorage.getItem('wa-lang');
      if (stored === 'ar' || stored === 'en') return stored;
    }
    return 'ar';
  }
}
