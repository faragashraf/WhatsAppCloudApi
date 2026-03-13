import { Routes } from '@angular/router';
import { adminGuard, authGuard, permissionGuard, publicGuard, superAdminGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // --- Public pages (with public layout) ---
  {
    path: '',
    loadComponent: () => import('./layout/public-layout/public-layout.component').then(m => m.PublicLayoutComponent),
    children: [
      { path: '', loadComponent: () => import('./pages/home/home.component').then(m => m.HomeComponent) },
      { path: 'pricing', loadComponent: () => import('./pages/pricing/pricing.component').then(m => m.PricingComponent) },
      { path: 'about', loadComponent: () => import('./pages/about/about.component').then(m => m.AboutComponent) },
      { path: 'system-guide', loadComponent: () => import('./pages/system-guide/system-guide.component').then(m => m.SystemGuideComponent) },
      { path: 'contact', loadComponent: () => import('./pages/contact/contact.component').then(m => m.ContactComponent) },
      { path: 'whatsapp-meta-guide', loadComponent: () => import('./pages/whatsapp-meta-guide/whatsapp-meta-guide.component').then(m => m.WhatsappMetaGuideComponent) },
      { path: 'forbidden', loadComponent: () => import('./pages/system/forbidden/forbidden.component').then(m => m.ForbiddenComponent) },
      { path: 'not-found', loadComponent: () => import('./pages/system/not-found/not-found.component').then(m => m.NotFoundComponent) },
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
  {
    path: 'forgot-password',
    canActivate: [publicGuard],
    loadComponent: () => import('./pages/auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent),
  },

  // --- Dashboard (protected) ---
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/dashboard-layout/dashboard-layout.component').then(m => m.DashboardLayoutComponent),
    children: [
      { path: '', loadComponent: () => import('./pages/dashboard/dashboard-home/dashboard-home.component').then(m => m.DashboardHomeComponent) },
      {
        path: 'inbox',
        canActivate: [permissionGuard],
        data: { requiredPermission: 'conversationsView' },
        loadComponent: () => import('./pages/dashboard/inbox/inbox.component').then(m => m.InboxComponent),
      },
      {
        path: 'contacts',
        canActivate: [permissionGuard],
        data: { requiredPermission: 'contactsView' },
        loadComponent: () => import('./pages/dashboard/contacts/contacts.component').then(m => m.ContactsComponent),
      },
      {
        path: 'campaigns',
        canActivate: [permissionGuard],
        data: { requiredPermission: 'campaignsView' },
        loadComponent: () => import('./pages/dashboard/campaigns/campaigns.component').then(m => m.CampaignsComponent),
      },
      {
        path: 'automation',
        canActivate: [permissionGuard],
        data: { requiredPermission: 'automationView' },
        loadComponent: () => import('./pages/dashboard/automation/automation.component').then(m => m.AutomationComponent),
      },
      {
        path: 'form-submissions',
        canActivate: [permissionGuard],
        data: { requiredPermission: 'automationView' },
        loadComponent: () => import('./pages/dashboard/form-submissions/form-submissions.component').then(m => m.FormSubmissionsComponent),
      },
      {
        path: 'custom-webhooks',
        canActivate: [permissionGuard],
        data: { requiredPermission: 'automationView' },
        loadComponent: () => import('./pages/dashboard/custom-webhooks/custom-webhooks.component').then(m => m.CustomWebhooksComponent),
      },
      {
        path: 'instances',
        canActivate: [adminGuard],
        loadComponent: () => import('./pages/dashboard/instances/instances.component').then(m => m.InstancesComponent),
      },
      { path: 'numbers', loadComponent: () => import('./pages/dashboard/numbers/numbers.component').then(m => m.NumbersComponent) },
      {
        path: 'messages',
        canActivate: [permissionGuard],
        data: { requiredPermission: 'messagesView' },
        loadComponent: () => import('./pages/dashboard/messages/messages.component').then(m => m.MessagesComponent),
      },
      {
        path: 'templates',
        canActivate: [permissionGuard],
        data: { requiredPermission: 'templatesView' },
        loadComponent: () => import('./pages/dashboard/templates/templates.component').then(m => m.TemplatesComponent),
      },
      { path: 'health', loadComponent: () => import('./pages/dashboard/health/health.component').then(m => m.HealthComponent) },
      { path: 'notifications', loadComponent: () => import('./pages/dashboard/notifications/notifications.component').then(m => m.NotificationsComponent) },
      { path: 'email', canActivate: [adminGuard], loadComponent: () => import('./pages/dashboard/email-center/email-center.component').then(m => m.EmailCenterComponent) },
      { path: 'developer', canActivate: [adminGuard], loadComponent: () => import('./pages/dashboard/developer/developer.component').then(m => m.DeveloperComponent) },
      { path: 'billing', canActivate: [adminGuard], loadComponent: () => import('./pages/dashboard/billing/billing.component').then(m => m.BillingComponent) },
      { path: 'users', canActivate: [adminGuard], loadComponent: () => import('./pages/dashboard/users/users.component').then(m => m.UsersComponent) },
      { path: 'settings', canActivate: [adminGuard], loadComponent: () => import('./pages/dashboard/settings/settings.component').then(m => m.SettingsComponent) },
      {
        path: 'send-message',
        canActivate: [permissionGuard],
        data: { requiredPermission: 'conversationsSend' },
        loadComponent: () => import('./pages/dashboard/send-message/send-message.component').then(m => m.SendMessageComponent),
      },
      { path: 'activity', loadComponent: () => import('./pages/dashboard/activity/activity.component').then(m => m.ActivityComponent) },
      { path: 'super-admin', canActivate: [superAdminGuard], loadComponent: () => import('./pages/dashboard/super-admin/super-admin.component').then(m => m.SuperAdminComponent) },
      { path: 'super-admin/logs', canActivate: [superAdminGuard], loadComponent: () => import('./pages/dashboard/super-admin-logs/super-admin-logs.component').then(m => m.SuperAdminLogsComponent) },
    ],
  },

  // --- Catch-all redirect ---
  { path: '**', redirectTo: '/not-found' },
];
