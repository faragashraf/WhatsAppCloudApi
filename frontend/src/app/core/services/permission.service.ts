import { Injectable, inject, computed } from '@angular/core';
import { TokenService } from './token.service';
import { UserPermissions } from '../models';

/**
 * Convenience service to check individual permissions.
 * Admin role always returns true for everything.
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly token = inject(TokenService);

  readonly permissions = computed<UserPermissions>(() => this.token.permissions());

  /** Check a specific permission key (e.g. 'contactsCreate') */
  has(key: keyof UserPermissions): boolean {
    if (this.token.role() === 'Admin') return true;
    return !!this.permissions()[key];
  }

  /** Returns true if user is an Admin */
  get isAdmin(): boolean {
    return this.token.role() === 'Admin';
  }
}
