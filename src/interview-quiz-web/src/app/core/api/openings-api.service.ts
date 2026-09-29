import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateOpeningRequest,
  OpeningFieldDefinitionResponse,
  OpeningResponse,
  PagedResult,
  ReplaceOpeningFieldDefinitionsRequest,
  UpdateOpeningRequest,
} from './contracts';

@Injectable({
  providedIn: 'root',
})
export class OpeningsApi {
  private readonly http = inject(HttpClient);

  list(page = 1, pageSize = 20, owner?: string): Observable<PagedResult<OpeningResponse>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (owner) {
      params = params.set('owner', owner);
    }
    return this.http.get<PagedResult<OpeningResponse>>('/api/openings', { params });
  }

  get(id: string): Observable<OpeningResponse> {
    return this.http.get<OpeningResponse>(`/api/openings/${id}`);
  }

  create(body: CreateOpeningRequest): Observable<OpeningResponse> {
    return this.http.post<OpeningResponse>('/api/openings', body);
  }

  update(id: string, body: UpdateOpeningRequest): Observable<OpeningResponse> {
    return this.http.put<OpeningResponse>(`/api/openings/${id}`, body);
  }

  listFieldDefinitions(): Observable<OpeningFieldDefinitionResponse[]> {
    return this.http.get<OpeningFieldDefinitionResponse[]>('/api/opening-field-definitions');
  }

  replaceFieldDefinitions(
    body: ReplaceOpeningFieldDefinitionsRequest,
  ): Observable<OpeningFieldDefinitionResponse[]> {
    return this.http.put<OpeningFieldDefinitionResponse[]>('/api/opening-field-definitions', body);
  }
}
