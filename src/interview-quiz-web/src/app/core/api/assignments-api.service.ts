import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AssignmentListQuery,
  AssignmentResponse,
  AssignmentSummaryResponse,
  CreateAssignmentFromTemplateRequest,
  CreateAssignmentRequest,
  InviteDeliveryResponse,
  InviteLifetimeResponse,
  InviteResponse,
  LiveProgressResponse,
  PagedResult,
} from './contracts';

@Injectable({
  providedIn: 'root',
})
export class AssignmentsApi {
  private readonly http = inject(HttpClient);

  list(
    page = 1,
    pageSize = 20,
    query?: AssignmentListQuery,
  ): Observable<PagedResult<AssignmentSummaryResponse>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const openingId = query?.openingId?.trim();
    const quizId = query?.quizId?.trim();
    const keyword = query?.keyword?.trim();
    if (openingId) {
      params = params.set('openingId', openingId);
    }
    if (quizId) {
      params = params.set('quizId', quizId);
    }
    if (keyword) {
      params = params.set('keyword', keyword);
    }
    return this.http.get<PagedResult<AssignmentSummaryResponse>>('/api/assignments', { params });
  }

  get(id: string): Observable<AssignmentResponse> {
    return this.http.get<AssignmentResponse>(`/api/assignments/${id}`);
  }

  create(body: CreateAssignmentRequest): Observable<AssignmentResponse> {
    return this.http.post<AssignmentResponse>('/api/assignments', body);
  }

  createFromTemplate(body: CreateAssignmentFromTemplateRequest): Observable<AssignmentResponse> {
    return this.http.post<AssignmentResponse>('/api/assignments/from-template', body);
  }

  invite(id: string, expiresAt?: string | null): Observable<InviteResponse> {
    return this.http.post<InviteResponse>(`/api/assignments/${id}/invite`, {
      expiresAt: expiresAt ?? null,
    });
  }

  resendInvite(id: string): Observable<InviteDeliveryResponse> {
    return this.http.post<InviteDeliveryResponse>(`/api/assignments/${id}/invite/resend`, {});
  }

  revokeInvite(id: string): Observable<InviteLifetimeResponse> {
    return this.http.post<InviteLifetimeResponse>(`/api/assignments/${id}/invite/revoke`, {});
  }

  extendInvite(id: string, expiresAt: string | null): Observable<InviteLifetimeResponse> {
    return this.http.post<InviteLifetimeResponse>(`/api/assignments/${id}/invite/extend`, { expiresAt });
  }

  liveProgress(id: string): Observable<LiveProgressResponse> {
    return this.http.get<LiveProgressResponse>(`/api/assignments/${id}/live/progress`);
  }

  startLive(id: string): Observable<LiveProgressResponse> {
    return this.http.post<LiveProgressResponse>(`/api/assignments/${id}/live/start`, {});
  }

  pauseLive(id: string): Observable<LiveProgressResponse> {
    return this.http.post<LiveProgressResponse>(`/api/assignments/${id}/live/pause`, {});
  }

  resumeLive(id: string): Observable<LiveProgressResponse> {
    return this.http.post<LiveProgressResponse>(`/api/assignments/${id}/live/resume`, {});
  }

  endLive(id: string): Observable<LiveProgressResponse> {
    return this.http.post<LiveProgressResponse>(`/api/assignments/${id}/live/end`, {});
  }
}
