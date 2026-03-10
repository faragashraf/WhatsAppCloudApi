import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { definePreset } from '@primeuix/themes';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';

const AppAuraPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#ebfbf2',
      100: '#d3f6e3',
      200: '#aaecc9',
      300: '#78ddaa',
      400: '#40cb86',
      500: '#10a861',
      600: '#0d8b53',
      700: '#0a6f45',
      800: '#0a5a39',
      900: '#0b4a31',
      950: '#042a1d',
    },
    colorScheme: {
      light: {
        surface: {
          0: '#ffffff',
          50: '#f8fbff',
          100: '#f1f6fb',
          200: '#d9e4ef',
          300: '#c8d6e5',
          400: '#9cb1c8',
          500: '#72859a',
          600: '#4b5f75',
          700: '#34485f',
          800: '#1d334c',
          900: '#11243a',
          950: '#0a1624',
        },
        primary: {
          color: '#10a861',
          contrastColor: '#ffffff',
          hoverColor: '#0d8b53',
          activeColor: '#0a6f45',
        },
        highlight: {
          background: '#dbf7e8',
          focusBackground: '#c6f2da',
          color: '#0a6f45',
          focusColor: '#0b4a31',
        },
        formField: {
          background: '#ffffff',
          disabledBackground: '#eaf1f8',
          filledBackground: '#f7fafc',
          filledHoverBackground: '#f1f6fb',
          filledFocusBackground: '#ffffff',
          borderColor: '#d9e4ef',
          hoverBorderColor: '#9cb1c8',
          focusBorderColor: '#10a861',
          invalidBorderColor: '#dc4c4c',
          color: '#11243a',
          disabledColor: '#72859a',
          placeholderColor: '#72859a',
          invalidPlaceholderColor: '#dc4c4c',
          floatLabelColor: '#72859a',
          floatLabelFocusColor: '#0d8b53',
          floatLabelActiveColor: '#4b5f75',
          floatLabelInvalidColor: '#dc4c4c',
          iconColor: '#72859a',
          shadow: 'none',
        },
        text: {
          color: '#11243a',
          hoverColor: '#0a1a2c',
          mutedColor: '#72859a',
          hoverMutedColor: '#4b5f75',
        },
        content: {
          background: '#ffffff',
          hoverBackground: '#f1f6fb',
          borderColor: '#d9e4ef',
          color: '#11243a',
          hoverColor: '#0a1a2c',
        },
        overlay: {
          select: {
            background: '#ffffff',
            borderColor: '#d9e4ef',
            color: '#11243a',
          },
          popover: {
            background: '#ffffff',
            borderColor: '#d9e4ef',
            color: '#11243a',
          },
          modal: {
            background: '#ffffff',
            borderColor: '#d9e4ef',
            color: '#11243a',
          },
        },
      },
    },
  },
});

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withViewTransitions()),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: AppAuraPreset,
        options: {
          darkModeSelector: '.dark',
        },
      },
    }),
    provideTranslateService({ defaultLanguage: 'ar' }),
    provideTranslateHttpLoader({ prefix: './i18n/', suffix: '.json' }),
  ]
};
