import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { pageAndCriteria } from './list-query';
import {
  CreateOpeningRequest,
  OpeningFieldDefinitionResponse,
  OpeningListCriteria,
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

  list(
    page = 1,
    pageSize = 20,
    criteria?: OpeningListCriteria,
  ): Observable<PagedResult<OpeningResponse>> {
    return this.http.get<PagedResult<OpeningResponse>>('/api/openings', {
      params: pageAndCriteria(page, pageSize, criteria),
    });
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
