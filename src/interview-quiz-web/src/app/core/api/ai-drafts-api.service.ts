import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AcceptAiDraftRequest, AiDraftAcceptResponse, AiDraftResponse, CreateAiDraftRequest } from './contracts';

@Injectable({
  providedIn: 'root',
})
export class AiDraftsApi {
  private readonly http = inject(HttpClient);

  create(body: CreateAiDraftRequest): Observable<AiDraftResponse> {
    return this.http.post<AiDraftResponse>('/api/ai-drafts', body);
  }

  get(id: string): Observable<AiDraftResponse> {
    return this.http.get<AiDraftResponse>(`/api/ai-drafts/${id}`);
  }

  accept(id: string, body: AcceptAiDraftRequest): Observable<AiDraftAcceptResponse> {
    return this.http.post<AiDraftAcceptResponse>(`/api/ai-drafts/${id}/accept`, body);
  }
}
