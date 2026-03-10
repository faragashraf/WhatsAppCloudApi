import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class SidebarService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly destroyRef = inject(DestroyRef);

  /** true = expanded (w-64 / 256px), false = collapsed (70px) */
  readonly expanded = signal(true);
  readonly isMobile = signal(false);
  readonly mobileOpen = signal(false);

  constructor() {
    if (!isPlatformBrowser(this.platformId)) return;

    const mediaQuery = window.matchMedia('(max-width: 1023px)');

    const applyBreakpoint = (matches: boolean) => {
      this.isMobile.set(matches);
      if (!matches) this.mobileOpen.set(false);
    };

    applyBreakpoint(mediaQuery.matches);

    const onChange = (event: MediaQueryListEvent) => applyBreakpoint(event.matches);
    mediaQuery.addEventListener('change', onChange);
    this.destroyRef.onDestroy(() => mediaQuery.removeEventListener('change', onChange));
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
    return this.expanded() ? '16rem' : '70px';
  }

  get contentOffset(): string {
    return this.isMobile() ? '0px' : this.width;
  }
}
