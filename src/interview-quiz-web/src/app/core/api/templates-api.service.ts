import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CloneTemplateVersionRequest,
  PagedResult,
  QuizResponse,
  TemplateListCriteria,
  TemplateResponse,
  TemplateSummaryResponse,
  TemplateVersionResponse,
  TemplateVersionSummaryResponse,
} from './contracts';
import { pageAndCriteria } from './list-query';

@Injectable({
  providedIn: 'root',
})
export class TemplatesApi {
  private readonly http = inject(HttpClient);

  list(
    page = 1,
    pageSize = 20,
    criteria?: TemplateListCriteria,
  ): Observable<PagedResult<TemplateSummaryResponse>> {
    return this.http.get<PagedResult<TemplateSummaryResponse>>('/api/templates', {
      params: pageAndCriteria(page, pageSize, criteria),
    });
  }

  get(id: string): Observable<TemplateResponse> {
    return this.http.get<TemplateResponse>(`/api/templates/${id}`);
  }

  listVersions(
    id: string,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<TemplateVersionSummaryResponse>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<TemplateVersionSummaryResponse>>(`/api/templates/${id}/versions`, {
      params,
    });
  }

  getVersion(id: string, versionId: string): Observable<TemplateVersionResponse> {
    return this.http.get<TemplateVersionResponse>(`/api/templates/${id}/versions/${versionId}`);
  }

  clone(id: string, versionId: string, body: CloneTemplateVersionRequest): Observable<QuizResponse> {
    return this.http.post<QuizResponse>(`/api/templates/${id}/versions/${versionId}/clone`, body);
  }
}
