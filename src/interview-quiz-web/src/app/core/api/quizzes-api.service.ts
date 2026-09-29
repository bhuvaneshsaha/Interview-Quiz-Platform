import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateQuizRequest, PagedResult, QuizResponse, UpdateQuizRequest } from './contracts';

@Injectable({
  providedIn: 'root',
})
export class QuizzesApi {
  private readonly http = inject(HttpClient);

  list(page = 1, pageSize = 20, openingId?: string): Observable<PagedResult<QuizResponse>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (openingId) {
      params = params.set('openingId', openingId);
    }
    return this.http.get<PagedResult<QuizResponse>>('/api/quizzes', { params });
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
}
