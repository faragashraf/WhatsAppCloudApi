import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { TokenService } from '../services/token.service';
import { AuthService } from '../services/auth.service';

let isRefreshing = false;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenService = inject(TokenService);
  const authService = inject(AuthService);
  const router = inject(Router);

  const token = tokenService.accessToken;
  let authReq = req;

  if (token && !req.url.includes('/auth/')) {
    authReq = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.includes('/auth/') && !isRefreshing) {
        isRefreshing = true;
        return authService.refreshToken().pipe(
          switchMap((result) => {
            isRefreshing = false;
            const retryReq = req.clone({
              setHeaders: { Authorization: `Bearer ${result.tokens.accessToken}` },
            });
            return next(retryReq);
          }),
          catchError((refreshErr) => {
            isRefreshing = false;
            tokenService.logout({
              redirectUrl: router.url,
              reason: 'session_expired',
            });
            return throwError(() => refreshErr);
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};
