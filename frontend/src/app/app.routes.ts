import { Routes } from '@angular/router';
import { authGuard, publicGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // --- Public pages (with public layout) ---
  {
    path: '',
    loadComponent: () => import('./layout/public-layout/public-layout.component').then(m => m.PublicLayoutComponent),
    children: [
      { path: '', loadComponent: () => import('./pages/home/home.component').then(m => m.HomeComponent) },
      { path: 'pricing', loadComponent: () => import('./pages/pricing/pricing.component').then(m => m.PricingComponent) },
      { path: 'about', loadComponent: () => import('./pages/about/about.component').then(m => m.AboutComponent) },
      { path: 'contact', loadComponent: () => import('./pages/contact/contact.component').then(m => m.ContactComponent) },
    ],
  },

  // --- Auth pages ---
  {
    path: 'login',
    canActivate: [publicGuard],
    loadComponent: () => import('./pages/auth/login/login.component').then(m => m.LoginComponent),
  },
  {
    path: 'register',
    canActivate: [publicGuard],
    loadComponent: () => import('./pages/auth/register/register.component').then(m => m.RegisterComponent),
  },

  // --- Dashboard (protected) ---
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/dashboard-layout/dashboard-layout.component').then(m => m.DashboardLayoutComponent),
    children: [
      { path: '', loadComponent: () => import('./pages/dashboard/dashboard-home/dashboard-home.component').then(m => m.DashboardHomeComponent) },
      { path: 'instances', loadComponent: () => import('./pages/dashboard/instances/instances.component').then(m => m.InstancesComponent) },
      { path: 'numbers', loadComponent: () => import('./pages/dashboard/numbers/numbers.component').then(m => m.NumbersComponent) },
      { path: 'messages', loadComponent: () => import('./pages/dashboard/messages/messages.component').then(m => m.MessagesComponent) },
      { path: 'health', loadComponent: () => import('./pages/dashboard/health/health.component').then(m => m.HealthComponent) },
      { path: 'billing', loadComponent: () => import('./pages/dashboard/billing/billing.component').then(m => m.BillingComponent) },
      { path: 'users', loadComponent: () => import('./pages/dashboard/users/users.component').then(m => m.UsersComponent) },
      { path: 'settings', loadComponent: () => import('./pages/dashboard/settings/settings.component').then(m => m.SettingsComponent) },
    ],
  },

  // --- Catch-all redirect ---
  { path: '**', redirectTo: '' },
];
