import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { NavbarComponent } from '../navbar/navbar.component';

@Component({
  selector: 'app-dashboard-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, NavbarComponent],
  template: `
    <app-navbar />
    <div class="flex min-h-[calc(100vh-64px)] pt-16">
      <app-sidebar />
      <main class="flex-1 ml-[70px] lg:ml-64 p-6 md:p-8 bg-slate-50 dark:bg-slate-900 overflow-y-auto transition-all duration-300">
        <router-outlet />
      </main>
    </div>
  `,
})
export class DashboardLayoutComponent {}
