import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CandidateTokenResponse, ConsumeMagicLinkRequest } from './contracts';

@Injectable({
  providedIn: 'root',
})
export class MagicLinkApi {
  private readonly http = inject(HttpClient);

  consume(token: string): Observable<CandidateTokenResponse> {
    const body: ConsumeMagicLinkRequest = { token };
    return this.http.post<CandidateTokenResponse>('/api/auth/magic-link/consume', body);
  }
}
