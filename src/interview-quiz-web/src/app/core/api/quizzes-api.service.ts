import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { pageAndCriteria } from './list-query';
import {
  CreateQuizRequest,
  IncludeQuestionsRequest,
  PagedResult,
  PublishTemplateResponse,
  QuizListCriteria,
  QuizResponse,
  UpdateQuizRequest,
} from './contracts';

@Injectable({
  providedIn: 'root',
})
export class QuizzesApi {
  private readonly http = inject(HttpClient);

  list(page = 1, pageSize = 20, criteria?: QuizListCriteria): Observable<PagedResult<QuizResponse>> {
    return this.http.get<PagedResult<QuizResponse>>('/api/quizzes', {
      params: pageAndCriteria(page, pageSize, criteria),
    });
  }

  get(id: string): Observable<QuizResponse> {
    return this.http.get<QuizResponse>(`/api/quizzes/${id}`);
  }

  create(body: CreateQuizRequest): Observable<QuizResponse> {
    return this.http.post<QuizResponse>('/api/quizzes', body);
  }

  update(id: string, body: UpdateQuizRequest): Observable<QuizResponse> {
    return this.http.put<QuizResponse>(`/api/quizzes/${id}`, body);
  }

  publishTemplate(id: string): Observable<PublishTemplateResponse> {
    return this.http.post<PublishTemplateResponse>(`/api/quizzes/${id}/publish-template`, {});
  }

  includeQuestions(quizId: string, body: IncludeQuestionsRequest): Observable<QuizResponse> {
    return this.http.post<QuizResponse>(`/api/quizzes/${quizId}/include-questions`, body);
  }
}
