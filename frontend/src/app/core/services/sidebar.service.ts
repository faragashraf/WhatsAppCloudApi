import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class SidebarService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly destroyRef = inject(DestroyRef);

  /** true = expanded (w-64 / 256px), false = collapsed (70px) */
  readonly expanded = signal(true);
  readonly isMobile = signal(false);
  readonly compactDesktop = signal(false);
  readonly mobileOpen = signal(false);

  constructor() {
    if (!isPlatformBrowser(this.platformId)) return;

    const mediaQuery = window.matchMedia('(max-width: 1023px)');
    const compactDesktopQuery = window.matchMedia('(min-width: 1024px) and (max-width: 1279px)');

    const applyBreakpoint = (matches: boolean) => {
      this.isMobile.set(matches);
      if (!matches) this.mobileOpen.set(false);
    };

    const applyCompactDesktop = (matches: boolean) => {
      this.compactDesktop.set(matches);
      if (matches) {
        this.expanded.set(false);
      }
    };

    applyBreakpoint(mediaQuery.matches);
    applyCompactDesktop(compactDesktopQuery.matches);

    const onChange = (event: MediaQueryListEvent) => applyBreakpoint(event.matches);
    const onCompactDesktopChange = (event: MediaQueryListEvent) => applyCompactDesktop(event.matches);
    mediaQuery.addEventListener('change', onChange);
    compactDesktopQuery.addEventListener('change', onCompactDesktopChange);
    this.destroyRef.onDestroy(() => {
      mediaQuery.removeEventListener('change', onChange);
      compactDesktopQuery.removeEventListener('change', onCompactDesktopChange);
    });
  }

  toggle(): void {
    if (this.isMobile()) {
      this.mobileOpen.update((v) => !v);
      return;
    }
    this.expanded.update((v) => !v);
  }

  closeMobile(): void {
    this.mobileOpen.set(false);
  }

  get width(): string {
    if (this.expanded()) return '16rem';
    return this.compactDesktop() ? '4.5rem' : '70px';
  }

  get contentOffset(): string {
    return this.isMobile() ? '0px' : this.width;
  }
}
