import { Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { TokenService } from './token.service';
import { AuthResult, LoginRequest, RegisterRequest, RefreshTokenRequest, VerifyOtpRequest, ResetPasswordRequest } from '../models';

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

  forgotPassword(email: string): Observable<any> {
    return this.api.post('/auth/forgot-password', { email });
  }

  verifyOtp(email: string, otp: string): Observable<boolean> {
    return this.api.post<boolean>('/auth/verify-otp', { email, otp });
  }

  resetPassword(email: string, otp: string, newPassword: string): Observable<any> {
    return this.api.post('/auth/reset-password', { email, otp, newPassword });
  }

  logout(): void {
    this.tokenService.logout();
  }

  get isAuthenticated(): boolean {
    return this.tokenService.isAuthenticated();
  }
}
