import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserPermissions } from '../models';
import { PermissionService, TokenService } from '../services';

const toLoginTree = (router: Router, targetUrl: string) =>
  router.createUrlTree(['/login'], {
    queryParams: {
      redirectUrl: targetUrl,
      reason: 'auth_required',
    },
  });

const toForbiddenTree = (router: Router, targetUrl: string) =>
  router.createUrlTree(['/forbidden'], {
    queryParams: { from: targetUrl },
  });

export const authGuard: CanActivateFn = (_route, state) => {
  const tokenService = inject(TokenService);
  const router = inject(Router);

  if (tokenService.isAuthenticated()) {
    return true;
  }
  return toLoginTree(router, state.url || '/dashboard');
};

export const publicGuard: CanActivateFn = () => {
  const tokenService = inject(TokenService);
  const router = inject(Router);

  if (!tokenService.isAuthenticated()) {
    return true;
  }
  return router.createUrlTree(['/dashboard']);
};

export const adminGuard: CanActivateFn = (_route, state) => {
  const tokenService = inject(TokenService);
  const router = inject(Router);

  if (!tokenService.isAuthenticated()) {
    return toLoginTree(router, state.url || '/dashboard');
  }

  if (tokenService.role() === 'Admin') {
    return true;
  }

  return toForbiddenTree(router, state.url || '/dashboard');
};

export const superAdminGuard: CanActivateFn = (_route, state) => {
  const tokenService = inject(TokenService);
  const router = inject(Router);

  if (!tokenService.isAuthenticated()) {
    return toLoginTree(router, state.url || '/dashboard');
  }

  if (tokenService.isSuperAdmin()) {
    return true;
  }

  return toForbiddenTree(router, state.url || '/dashboard');
};

export const permissionGuard: CanActivateFn = (route, state) => {
  const tokenService = inject(TokenService);
  const permissionService = inject(PermissionService);
  const router = inject(Router);

  if (!tokenService.isAuthenticated()) {
    return toLoginTree(router, state.url || '/dashboard');
  }

  const requiredPermission = route.data?.['requiredPermission'] as keyof UserPermissions | undefined;
  if (!requiredPermission) {
    return true;
  }

  if (permissionService.has(requiredPermission)) {
    return true;
  }

  return toForbiddenTree(router, state.url || '/dashboard');
};
