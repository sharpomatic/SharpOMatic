import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { SettingsService } from '../services/settings.service';
import { AuthTokenService } from './auth-token.service';

export const sharpomaticAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const authTokenService = inject(AuthTokenService);
  const settingsService = inject(SettingsService);

  if (!req.url.startsWith(settingsService.apiUrl())) {
    return next(req);
  }

  return from(authTokenService.getAuthorizationHeader()).pipe(
    switchMap((authorization) => {
      const authorizedRequest = authorization
        ? req.clone({ setHeaders: { Authorization: authorization } })
        : req;

      return next(authorizedRequest);
    }),
    catchError((error) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        void authTokenService.onUnauthorized();
      }

      return throwError(() => error);
    }),
  );
};
