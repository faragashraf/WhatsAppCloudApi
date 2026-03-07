import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TokenService, ThemeService } from '../../core/services';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
  ],
  template: `
    <nav class="fixed top-0 left-0 right-0 z-50 backdrop-blur-xl bg-white/80 dark:bg-slate-900/80 border-b border-slate-200 dark:border-slate-700/50">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center h-16">
          <!-- Logo -->
          <a routerLink="/" class="flex items-center gap-2 no-underline">
            <div class="w-9 h-9 bg-gradient-to-br from-emerald-500 to-green-600 rounded-xl flex items-center justify-center shadow-lg shadow-emerald-500/25">
              <mat-icon class="text-white !text-[20px]">chat</mat-icon>
            </div>
            <span class="text-xl font-bold bg-gradient-to-r from-emerald-600 to-green-600 bg-clip-text text-transparent">
              WaCloud
            </span>
          </a>

          <!-- Desktop Nav -->
          <div class="hidden md:flex items-center gap-1">
            <a routerLink="/" routerLinkActive="!text-emerald-600 !font-semibold"
               [routerLinkActiveOptions]="{exact: true}"
               class="px-4 py-2 text-sm font-medium text-slate-600 dark:text-slate-300 hover:text-emerald-600 dark:hover:text-emerald-400 transition-colors rounded-lg hover:bg-emerald-50 dark:hover:bg-emerald-950/30 no-underline">
              Home
            </a>
            <a routerLink="/pricing" routerLinkActive="!text-emerald-600 !font-semibold"
               class="px-4 py-2 text-sm font-medium text-slate-600 dark:text-slate-300 hover:text-emerald-600 dark:hover:text-emerald-400 transition-colors rounded-lg hover:bg-emerald-50 dark:hover:bg-emerald-950/30 no-underline">
              Pricing
            </a>
            <a routerLink="/about" routerLinkActive="!text-emerald-600 !font-semibold"
               class="px-4 py-2 text-sm font-medium text-slate-600 dark:text-slate-300 hover:text-emerald-600 dark:hover:text-emerald-400 transition-colors rounded-lg hover:bg-emerald-50 dark:hover:bg-emerald-950/30 no-underline">
              About
            </a>
            <a routerLink="/contact" routerLinkActive="!text-emerald-600 !font-semibold"
               class="px-4 py-2 text-sm font-medium text-slate-600 dark:text-slate-300 hover:text-emerald-600 dark:hover:text-emerald-400 transition-colors rounded-lg hover:bg-emerald-50 dark:hover:bg-emerald-950/30 no-underline">
              Contact
            </a>
          </div>

          <!-- Actions -->
          <div class="flex items-center gap-2">
            <button mat-icon-button (click)="themeService.toggle()" class="!text-slate-500 dark:!text-slate-400">
              <mat-icon>{{ themeService.mode() === 'dark' ? 'light_mode' : 'dark_mode' }}</mat-icon>
            </button>

            @if (tokenService.isAuthenticated()) {
              <a routerLink="/dashboard" mat-flat-button class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700 !no-underline">
                Dashboard
              </a>
              <button mat-icon-button [matMenuTriggerFor]="userMenu" class="!text-slate-500 dark:!text-slate-400">
                <mat-icon>account_circle</mat-icon>
              </button>
              <mat-menu #userMenu="matMenu">
                <button mat-menu-item routerLink="/dashboard/settings">
                  <mat-icon>settings</mat-icon>
                  <span>Settings</span>
                </button>
                <button mat-menu-item (click)="tokenService.logout()">
                  <mat-icon>logout</mat-icon>
                  <span>Logout</span>
                </button>
              </mat-menu>
            } @else {
              <a routerLink="/login" mat-button class="!text-slate-600 dark:!text-slate-300 !no-underline">
                Sign in
              </a>
              <a routerLink="/register" mat-flat-button class="!bg-emerald-600 !text-white !rounded-xl hover:!bg-emerald-700 !no-underline">
                Get Started
              </a>
            }

            <!-- Mobile menu -->
            <button mat-icon-button class="md:!hidden !text-slate-500" (click)="mobileOpen.set(!mobileOpen())">
              <mat-icon>{{ mobileOpen() ? 'close' : 'menu' }}</mat-icon>
            </button>
          </div>
        </div>
      </div>

      <!-- Mobile Nav -->
      @if (mobileOpen()) {
        <div class="md:hidden border-t border-slate-200 dark:border-slate-700/50 bg-white dark:bg-slate-900 px-4 pb-4 pt-2 space-y-1">
          <a routerLink="/" (click)="mobileOpen.set(false)" class="block px-4 py-2 text-sm text-slate-600 dark:text-slate-300 hover:bg-emerald-50 dark:hover:bg-emerald-950/30 rounded-lg no-underline">Home</a>
          <a routerLink="/pricing" (click)="mobileOpen.set(false)" class="block px-4 py-2 text-sm text-slate-600 dark:text-slate-300 hover:bg-emerald-50 dark:hover:bg-emerald-950/30 rounded-lg no-underline">Pricing</a>
          <a routerLink="/about" (click)="mobileOpen.set(false)" class="block px-4 py-2 text-sm text-slate-600 dark:text-slate-300 hover:bg-emerald-50 dark:hover:bg-emerald-950/30 rounded-lg no-underline">About</a>
          <a routerLink="/contact" (click)="mobileOpen.set(false)" class="block px-4 py-2 text-sm text-slate-600 dark:text-slate-300 hover:bg-emerald-50 dark:hover:bg-emerald-950/30 rounded-lg no-underline">Contact</a>
        </div>
      }
    </nav>
  `,
})
export class NavbarComponent {
  readonly tokenService = inject(TokenService);
  readonly themeService = inject(ThemeService);
  readonly mobileOpen = signal(false);
}
