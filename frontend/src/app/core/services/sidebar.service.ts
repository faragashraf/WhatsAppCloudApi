import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class SidebarService {
  /** true = expanded (w-64 / 256px), false = collapsed (70px) */
  readonly expanded = signal(true);

  toggle(): void {
    this.expanded.update((v) => !v);
  }

  get width(): string {
    return this.expanded() ? '16rem' : '70px';
  }
}
