import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-logo',
  standalone: true,
  template: `
    <div class="logo-wrapper" [class.logo-sm]="size === 'sm'" [class.logo-lg]="size === 'lg'">
      <div class="logo-icon">
        <svg viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg" class="logo-svg">
          <defs>
            <linearGradient id="waGrad" x1="0" y1="0" x2="40" y2="40" gradientUnits="userSpaceOnUse">
              <stop offset="0%" stop-color="#25D366">
                <animate attributeName="stop-color" values="#25D366;#128C7E;#25D366" dur="4s" repeatCount="indefinite" />
              </stop>
              <stop offset="100%" stop-color="#128C7E">
                <animate attributeName="stop-color" values="#128C7E;#075E54;#128C7E" dur="4s" repeatCount="indefinite" />
              </stop>
            </linearGradient>
            <filter id="glowFilter" x="-50%" y="-50%" width="200%" height="200%">
              <feGaussianBlur stdDeviation="2" result="coloredBlur" />
              <feMerge>
                <feMergeNode in="coloredBlur" />
                <feMergeNode in="SourceGraphic" />
              </feMerge>
            </filter>
          </defs>
          <!-- Outer glow pulse -->
          <circle cx="20" cy="20" r="19" class="logo-glow" />
          <!-- Wave ripple (WhatsApp-style message wave) -->
          <circle cx="20" cy="20" r="16" class="logo-wave" />
          <circle cx="20" cy="20" r="14" class="logo-wave logo-wave-delayed" />
          <!-- Gradient bg circle -->
          <circle cx="20" cy="20" r="18" fill="url(#waGrad)" class="logo-bg" />
          <!-- WhatsApp phone icon -->
          <path d="M20 8.5C13.65 8.5 8.5 13.65 8.5 20c0 2.05.54 3.97 1.47 5.63L8.5 31.5l6.07-1.59A11.42 11.42 0 0020 31.5c6.35 0 11.5-5.15 11.5-11.5S26.35 8.5 20 8.5zm0 20.85c-1.78 0-3.47-.48-4.95-1.38l-.35-.21-3.64.95.97-3.55-.23-.37A9.38 9.38 0 0110.65 20c0-5.15 4.2-9.35 9.35-9.35S29.35 14.85 29.35 20 25.15 29.35 20 29.35z"
            class="logo-phone" />
          <!-- Phone handset -->
          <path d="M26.15 23.28c-.35-.18-2.07-1.02-2.39-1.14-.32-.12-.55-.18-.78.18-.23.35-.89 1.14-1.09 1.37-.2.23-.4.26-.75.09-.35-.18-1.48-.55-2.82-1.74-1.04-.93-1.74-2.07-1.95-2.42-.2-.35-.02-.54.15-.72.15-.16.35-.41.52-.62.17-.2.23-.35.35-.58.12-.23.06-.44-.03-.62-.09-.18-.78-1.88-1.07-2.57-.28-.67-.57-.58-.78-.59l-.67-.01c-.23 0-.6.09-.92.44-.32.35-1.21 1.18-1.21 2.88s1.24 3.34 1.41 3.57c.18.23 2.44 3.73 5.92 5.23.83.36 1.48.57 1.98.73.83.26 1.59.23 2.19.14.67-.1 2.07-.85 2.36-1.67.29-.82.29-1.52.2-1.67-.09-.15-.32-.23-.67-.41z"
            class="logo-phone-inner" />
        </svg>
      </div>
      @if (showText) {
        <span class="logo-text">
          <span class="logo-text-whatsapp">WhatsApp</span>
          <span class="logo-text-egypt">Egypt</span>
        </span>
      }
    </div>
  `,
  styles: [`
    :host { display: inline-flex; }

    .logo-wrapper {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      cursor: pointer;
    }

    .logo-icon {
      width: 40px;
      height: 40px;
      flex-shrink: 0;
      position: relative;
    }

    .logo-sm .logo-icon { width: 32px; height: 32px; }
    .logo-lg .logo-icon { width: 56px; height: 56px; }

    .logo-svg {
      width: 100%;
      height: 100%;
      filter: drop-shadow(0 4px 12px rgba(37, 211, 102, 0.3));
      transition: filter 0.3s ease, transform 0.3s ease;
    }

    /* Hover effects */
    .logo-wrapper:hover .logo-svg {
      filter: drop-shadow(0 6px 20px rgba(37, 211, 102, 0.5));
      transform: scale(1.08);
    }

    .logo-wrapper:hover .logo-text-whatsapp,
    .logo-wrapper:hover .logo-text-egypt {
      filter: brightness(1.15);
    }

    .logo-glow {
      fill: none;
      stroke: rgba(37, 211, 102, 0.25);
      stroke-width: 1;
      animation: logoGlowPulse 2.5s ease-in-out infinite;
    }

    /* Wave animation (WhatsApp-style message ripple) */
    .logo-wave {
      fill: none;
      stroke: rgba(37, 211, 102, 0.15);
      stroke-width: 0.5;
      opacity: 0;
      animation: logoWaveRipple 3s ease-out infinite;
    }

    .logo-wave-delayed {
      animation-delay: 1.5s;
    }

    .logo-bg {
      transition: filter 0.3s ease;
    }

    .logo-wrapper:hover .logo-bg {
      filter: url(#glowFilter);
    }

    .logo-phone,
    .logo-phone-inner {
      fill: white;
      animation: logoFadeIn 0.6s ease-out both;
    }

    .logo-phone { animation-delay: 0.2s; }
    .logo-phone-inner { animation-delay: 0.4s; }

    .logo-text {
      display: flex;
      gap: 0.25rem;
      font-size: 1.25rem;
      font-weight: 800;
      line-height: 1;
      transition: filter 0.3s ease;
    }

    .logo-sm .logo-text { font-size: 1rem; }
    .logo-lg .logo-text { font-size: 1.75rem; }

    .logo-text-whatsapp {
      background: linear-gradient(135deg, #25D366 0%, #128C7E 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
      transition: filter 0.3s ease;
    }

    .logo-text-egypt {
      background: linear-gradient(135deg, #128C7E 0%, #075E54 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
      transition: filter 0.3s ease;
    }

    @keyframes logoGlowPulse {
      0%, 100% { stroke-opacity: 0.2; r: 19; }
      50% { stroke-opacity: 0.6; r: 20; }
    }

    @keyframes logoWaveRipple {
      0% { r: 12; opacity: 0.4; stroke-width: 1; }
      100% { r: 22; opacity: 0; stroke-width: 0.2; }
    }

    @keyframes logoFadeIn {
      from { opacity: 0; transform: scale(0.8); }
      to { opacity: 1; transform: scale(1); }
    }
  `],
})
export class LogoComponent {
  @Input() size: 'sm' | 'md' | 'lg' = 'md';
  @Input() showText = true;
}
