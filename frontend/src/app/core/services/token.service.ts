import { Injectable, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { AuthResult, AuthTokens, UserPermissions, FULL_PERMISSIONS, DEFAULT_PERMISSIONS } from '../models';

const TOKEN_KEY = 'wa_access_token';
const REFRESH_KEY = 'wa_refresh_token';
const USER_KEY = 'wa_user';

@Injectable({ providedIn: 'root' })
export class TokenService {
  private readonly _user = signal<AuthResult | null>(this.loadUser());

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => !!this._user() && !this.isTokenExpired());
  readonly role = computed(() => this._user()?.role ?? '');
  readonly companyId = computed(() => this._user()?.companyId ?? 0);
  readonly userId = computed(() => this._user()?.userId ?? 0);
  readonly fullName = computed(() => this._user()?.fullName ?? '');
  readonly companyName = computed(() => this._user()?.companyName ?? '');
  readonly isSuperAdmin = computed(() => this._user()?.isSuperAdmin ?? false);
  readonly permissions = computed<UserPermissions>(() => {
    const u = this._user();
    if (!u) return DEFAULT_PERMISSIONS;
    if (u.role === 'Admin') return FULL_PERMISSIONS;
    return u.permissions ?? DEFAULT_PERMISSIONS;
  });

  constructor(private router: Router) {}

  get accessToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  get refreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY);
  }

  saveAuth(result: AuthResult): void {
    localStorage.setItem(TOKEN_KEY, result.tokens.accessToken);
    localStorage.setItem(REFRESH_KEY, result.tokens.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(result));
    this._user.set(result);
  }

  clearAuth(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
    this._user.set(null);
  }

  logout(): void {
    this.clearAuth();
    this.router.navigate(['/login']);
  }

  isTokenExpired(): boolean {
    const token = this.accessToken;
    if (!token) return true;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000 < Date.now();
    } catch {
      return true;
    }
  }

  private loadUser(): AuthResult | null {
    try {
      const raw = localStorage.getItem(USER_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }
}
