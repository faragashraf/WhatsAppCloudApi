import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-logo',
  standalone: true,
  template: `
    <div class="logo-wrapper" [class.logo-sm]="size === 'sm'" [class.logo-lg]="size === 'lg'">
      <div class="logo-image-shell">
        @if (!logoUnavailable) {
          <img
            [src]="logoSrc"
            alt="BotGlobal Services logo"
            class="logo-image"
            (error)="onLogoError()" />
        } @else {
          <span class="logo-fallback">BGS</span>
        }
      </div>

      @if (showText) {
        <span class="logo-text">
          <span class="logo-text-main">BotGlobal</span>
          <span class="logo-text-sub">Services</span>
        </span>
      }
    </div>
  `,
  styles: [`
    :host { display: inline-flex; }

    .logo-wrapper {
      display: inline-flex;
      align-items: center;
      gap: 0.55rem;
      cursor: pointer;
      user-select: none;
    }

    .logo-image-shell {
      width: 42px;
      height: 42px;
      border-radius: 50%;
      overflow: hidden;
      flex-shrink: 0;
      background: radial-gradient(circle at 50% 35%, #0d5bb2 0%, #0b2d68 55%, #051736 100%);
      box-shadow: 0 8px 20px rgba(10, 46, 107, 0.35);
      border: 1px solid rgba(56, 189, 248, 0.35);
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }

    .logo-image {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .logo-fallback {
      font-weight: 800;
      font-size: 0.7rem;
      letter-spacing: 0.08em;
      color: #dbeafe;
    }

    .logo-text {
      display: inline-flex;
      align-items: baseline;
      gap: 0.22rem;
      line-height: 1;
      white-space: nowrap;
      font-weight: 800;
      font-size: 1.18rem;
    }

    .logo-text-main {
      background: linear-gradient(135deg, #22d3ee 0%, #3b82f6 45%, #22c55e 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .logo-text-sub {
      font-size: 0.86em;
      letter-spacing: 0.13em;
      text-transform: uppercase;
      color: #0f172a;
    }

    :host-context(.dark) .logo-text-sub {
      color: #f1f5f9;
    }

    .logo-sm .logo-image-shell { width: 32px; height: 32px; }
    .logo-lg .logo-image-shell { width: 56px; height: 56px; }

    .logo-sm .logo-text { font-size: 0.95rem; }
    .logo-lg .logo-text { font-size: 1.62rem; }
  `],
})
export class LogoComponent {
  @Input() size: 'sm' | 'md' | 'lg' = 'md';
  @Input() showText = true;
  @Input() logoSrc = 'botglobal-services-logo.png';

  logoUnavailable = false;

  onLogoError(): void {
    this.logoUnavailable = true;
  }
}
