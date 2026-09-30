import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BankQuestionListCriteria,
  BankQuestionResponse,
  CreateBankQuestionRequest,
  PagedResult,
  UpdateBankQuestionRequest,
} from './contracts';
import { pageAndCriteria } from './list-query';

@Injectable({
  providedIn: 'root',
})
export class QuestionsApi {
  private readonly http = inject(HttpClient);

  list(
    page = 1,
    pageSize = 20,
    criteria?: BankQuestionListCriteria,
  ): Observable<PagedResult<BankQuestionResponse>> {
    return this.http.get<PagedResult<BankQuestionResponse>>('/api/questions', {
      params: pageAndCriteria(page, pageSize, criteria),
    });
  }

  get(id: string): Observable<BankQuestionResponse> {
    return this.http.get<BankQuestionResponse>(`/api/questions/${id}`);
  }

  create(body: CreateBankQuestionRequest): Observable<BankQuestionResponse> {
    return this.http.post<BankQuestionResponse>('/api/questions', body);
  }

  update(id: string, body: UpdateBankQuestionRequest): Observable<BankQuestionResponse> {
    return this.http.put<BankQuestionResponse>(`/api/questions/${id}`, body);
  }

  archive(id: string): Observable<BankQuestionResponse> {
    return this.http.post<BankQuestionResponse>(`/api/questions/${id}/archive`, {});
  }

  unarchive(id: string): Observable<BankQuestionResponse> {
    return this.http.post<BankQuestionResponse>(`/api/questions/${id}/unarchive`, {});
  }
}
