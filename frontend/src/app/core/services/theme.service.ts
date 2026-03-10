import { Injectable, signal, effect } from '@angular/core';

export type ThemeMode = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly STORAGE_KEY = 'wa_theme';
  readonly mode = signal<ThemeMode>(this.loadTheme());

  constructor() {
    effect(() => {
      const m = this.mode();
      document.documentElement.classList.toggle('dark', m === 'dark');
      document.body.style.colorScheme = m;
      localStorage.setItem(this.STORAGE_KEY, m);
    });
  }

  toggle(): void {
    this.mode.update((m) => (m === 'light' ? 'dark' : 'light'));
  }

  private loadTheme(): ThemeMode {
    const stored = localStorage.getItem(this.STORAGE_KEY) as ThemeMode | null;
    if (stored) return stored;
    return 'light';
  }
}
