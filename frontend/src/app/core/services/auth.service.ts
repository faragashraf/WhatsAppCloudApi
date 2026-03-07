import { Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { TokenService } from './token.service';
import { AuthResult, LoginRequest, RegisterRequest, RefreshTokenRequest } from '../models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(
    private api: ApiService,
    private tokenService: TokenService,
  ) {}

  login(request: LoginRequest): Observable<AuthResult> {
    return this.api.post<AuthResult>('/auth/login', request).pipe(
      tap((result) => this.tokenService.saveAuth(result)),
    );
  }

  register(request: RegisterRequest): Observable<AuthResult> {
    return this.api.post<AuthResult>('/auth/register-company', request).pipe(
      tap((result) => this.tokenService.saveAuth(result)),
    );
  }

  refreshToken(): Observable<AuthResult> {
    const refreshToken = this.tokenService.refreshToken;
    const body: RefreshTokenRequest = { refreshToken: refreshToken ?? '' };
    return this.api.post<AuthResult>('/auth/refresh-token', body).pipe(
      tap((result) => this.tokenService.saveAuth(result)),
    );
  }

  logout(): void {
    this.tokenService.logout();
  }

  get isAuthenticated(): boolean {
    return this.tokenService.isAuthenticated();
  }
}
