import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { isDevMode } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { CORRELATION_HEADER, toApiError } from './api-error';

export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }
      const apiError = toApiError(error, req);
      const correlationId = apiError.correlationId ?? req.headers.get(CORRELATION_HEADER);
      // Status + correlation only — never request/response bodies (passwords, tokens, PII).
      if (isDevMode()) {
        console.warn('HTTP request failed', {
          status: error.status,
          url: req.url,
          correlationId,
        });
      }
      return throwError(() => apiError);
    }),
  );
};
