import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { NavbarComponent } from '../navbar/navbar.component';
import { LanguageService, SidebarService } from '../../core/services';

@Component({
  selector: 'app-dashboard-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, NavbarComponent],
  template: `
    <app-navbar />
    <div class="flex min-h-[calc(100vh-64px)] pt-16">
      <app-sidebar />
      <main
        class="flex-1 p-6 md:p-8 bg-slate-50 dark:bg-slate-900 overflow-y-auto transition-all duration-300"
        [style.margin-inline-start]="sidebarService.width">
        <router-outlet />
      </main>
    </div>
  `,
})
export class DashboardLayoutComponent {
  readonly langService = inject(LanguageService);
  readonly sidebarService = inject(SidebarService);
}
