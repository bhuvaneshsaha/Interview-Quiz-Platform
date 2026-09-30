import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateFilterRequest,
  FilterResponse,
  FilterTarget,
  PagedResult,
  ShareFilterRequest,
  UpdateFilterRequest,
} from './contracts';

@Injectable({
  providedIn: 'root',
})
export class FiltersApi {
  private readonly http = inject(HttpClient);

  list(target: FilterTarget, page = 1, pageSize = 50): Observable<PagedResult<FilterResponse>> {
    const params = new HttpParams()
      .set('target', target)
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http.get<PagedResult<FilterResponse>>('/api/filters', { params });
  }

  get(id: string): Observable<FilterResponse> {
    return this.http.get<FilterResponse>(`/api/filters/${id}`);
  }

  create(body: CreateFilterRequest): Observable<FilterResponse> {
    return this.http.post<FilterResponse>('/api/filters', body);
  }

  update(id: string, body: UpdateFilterRequest): Observable<FilterResponse> {
    return this.http.put<FilterResponse>(`/api/filters/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/filters/${id}`);
  }

  share(id: string, body: ShareFilterRequest): Observable<FilterResponse> {
    return this.http.post<FilterResponse>(`/api/filters/${id}/share`, body);
  }

  unshare(id: string): Observable<FilterResponse> {
    return this.http.post<FilterResponse>(`/api/filters/${id}/unshare`, {});
  }
}
