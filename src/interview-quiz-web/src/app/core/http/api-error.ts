import { HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { ProblemDetails } from '../api/contracts';

export const CORRELATION_HEADER = 'X-Correlation-ID';

export class ApiError extends Error {
  readonly status: number;
  readonly correlationId: string | null;
  readonly title: string | null;

  constructor(
    message: string,
    status: number,
    correlationId: string | null,
    title: string | null,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.correlationId = correlationId;
    this.title = title;
  }
}

export function toApiError(error: HttpErrorResponse, req?: HttpRequest<unknown>): ApiError {
  const problem = isProblemDetails(error.error) ? error.error : null;
  const correlationId =
    header(error, CORRELATION_HEADER) ??
    (typeof problem?.correlationId === 'string' ? problem.correlationId : null) ??
    req?.headers.get(CORRELATION_HEADER) ??
    null;
  const title = problem?.title ?? error.statusText ?? 'Request failed';
  const detail = problem?.detail;
  const message = detail && detail.length > 0 ? detail : title;
  return new ApiError(message, error.status, correlationId, title);
}

export function userMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message;
  }
  if (error instanceof HttpErrorResponse) {
    return toApiError(error).message;
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return 'Something went wrong. Try again.';
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null;
}

function header(error: HttpErrorResponse, name: string): string | null {
  return error.headers?.get(name) ?? null;
}
