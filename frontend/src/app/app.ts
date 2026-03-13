import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService, ThemeService } from './core/services';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`,
})
export class App {
  // Eagerly inject so translations + RTL initialize immediately
  private readonly lang = inject(LanguageService);
  // Eagerly inject so persisted theme is applied before first interaction
  private readonly theme = inject(ThemeService);
}
