import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AssignmentListQuery,
  AssignmentResponse,
  AssignmentSummaryResponse,
  CreateAssignmentRequest,
  InviteResponse,
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
    const keyword = query?.keyword?.trim();
    if (openingId) {
      params = params.set('openingId', openingId);
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

  invite(id: string): Observable<InviteResponse> {
    return this.http.post<InviteResponse>(`/api/assignments/${id}/invite`, {});
  }
}
