import { Component, input } from '@angular/core';
import { isDevMode } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError } from '../../core/http/api-error';

@Component({
  selector: 'app-page-status',
  imports: [RouterLink],
  templateUrl: './page-status.component.html',
  styleUrl: './page-status.component.css',
})
export class PageStatus {
  readonly loading = input(false);
  readonly empty = input(false);
  readonly error = input<string | null>(null);
  readonly correlationId = input<string | null>(null);
  readonly loadingMessage = input('Loading.');
  readonly emptyMessage = input('Nothing to show yet.');
  readonly emptyActionLabel = input<string | null>(null);
  readonly emptyActionLink = input<string | null>(null);

  readonly showDevCorrelation = isDevMode();

  static fromError(error: unknown): { message: string; correlationId: string | null } {
    if (error instanceof ApiError) {
      return { message: error.message, correlationId: error.correlationId };
    }
    if (error instanceof Error) {
      return { message: error.message, correlationId: null };
    }
    return { message: 'Something went wrong. Try again.', correlationId: null };
  }
}
