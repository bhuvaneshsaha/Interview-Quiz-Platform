import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { TokenStore } from '../auth/token-store.service';

const RETRIED = new HttpContextToken(() => false);

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokens = inject(TokenStore);
  const auth = inject(AuthService);

  if (isAnonymousAuthRequest(req)) {
    return next(req);
  }

  const access = tokens.accessToken();
  const authed = access
    ? req.clone({ setHeaders: { Authorization: `Bearer ${access}` } })
    : req;

  return next(authed).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || authed.context.get(RETRIED)) {
        return throwError(() => error);
      }
      return auth.refreshTokens().pipe(
        switchMap(() => {
          const retryToken = tokens.accessToken();
          if (!retryToken) {
            auth.logout();
            return throwError(() => error);
          }
          const retry = authed.clone({
            setHeaders: { Authorization: `Bearer ${retryToken}` },
            context: authed.context.set(RETRIED, true),
          });
          return next(retry);
        }),
        catchError(() => {
          auth.logout();
          return throwError(() => error);
        }),
      );
    }),
  );
};

export function isAnonymousAuthRequest(req: HttpRequest<unknown>): boolean {
  const url = req.url.split('?')[0];
  return (
    url.endsWith('/api/auth/login') ||
    url.endsWith('/api/auth/refresh') ||
    url.endsWith('/api/auth/logout')
  );
}
