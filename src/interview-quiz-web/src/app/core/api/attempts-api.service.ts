import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AttemptResultResponse,
  AttemptSummaryResponse,
  CandidateAttemptResponse,
  CandidateSubmitResponse,
  PagedResult,
  SaveAnswersRequest,
} from './contracts';

@Injectable({
  providedIn: 'root',
})
export class AttemptsApi {
  private readonly http = inject(HttpClient);

  listByAssignment(
    assignmentId: string,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<AttemptSummaryResponse>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<AttemptSummaryResponse>>(
      `/api/assignments/${assignmentId}/attempts`,
      { params },
    );
  }

  get(id: string): Observable<AttemptResultResponse> {
    return this.http.get<AttemptResultResponse>(`/api/attempts/${id}`);
  }

  start(assignmentId: string): Observable<CandidateAttemptResponse> {
    return this.http.post<CandidateAttemptResponse>(
      `/api/assignments/${assignmentId}/attempts`,
      {},
    );
  }

  getCurrent(assignmentId: string): Observable<CandidateAttemptResponse> {
    return this.http.get<CandidateAttemptResponse>(
      `/api/assignments/${assignmentId}/attempts/current`,
    );
  }

  saveAnswers(
    assignmentId: string,
    body: SaveAnswersRequest,
  ): Observable<CandidateAttemptResponse> {
    return this.http.put<CandidateAttemptResponse>(
      `/api/assignments/${assignmentId}/attempts/current/answers`,
      body,
    );
  }

  submit(assignmentId: string): Observable<CandidateSubmitResponse> {
    return this.http.post<CandidateSubmitResponse>(
      `/api/assignments/${assignmentId}/attempts/current/submit`,
      {},
    );
  }
}
