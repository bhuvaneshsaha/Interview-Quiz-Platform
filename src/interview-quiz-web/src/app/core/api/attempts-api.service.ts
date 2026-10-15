import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AttemptResultResponse,
  AttemptReviewResponse,
  AttemptSummaryResponse,
  CandidateAttemptPreview,
  CandidateAttemptResponse,
  CandidateSubmitResponse,
  PagedResult,
  ReportPositionRequest,
  ReviewFlagDto,
  ReviewItemRequest,
  SaveAnswersRequest,
  SetReviewFlagsRequest,
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

  getReview(id: string): Observable<AttemptReviewResponse> {
    return this.http.get<AttemptReviewResponse>(`/api/attempts/${id}/review`);
  }

  scoreItem(attemptId: string, questionId: string, body: ReviewItemRequest): Observable<AttemptReviewResponse> {
    return this.http.post<AttemptReviewResponse>(
      `/api/attempts/${attemptId}/items/${questionId}/review`,
      body,
    );
  }

  suggestItem(attemptId: string, questionId: string): Observable<AttemptReviewResponse> {
    return this.http.post<AttemptReviewResponse>(
      `/api/attempts/${attemptId}/items/${questionId}/suggestion`,
      {},
    );
  }

  preview(assignmentId: string): Observable<CandidateAttemptPreview> {
    return this.http.get<CandidateAttemptPreview>(`/api/assignments/${assignmentId}/attempts/preview`);
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

  setFlags(assignmentId: string, flags: ReviewFlagDto[]): Observable<CandidateAttemptResponse> {
    const body: SetReviewFlagsRequest = { flags };
    return this.http.put<CandidateAttemptResponse>(
      `/api/assignments/${assignmentId}/attempts/current/flags`,
      body,
    );
  }

  reportPosition(assignmentId: string, questionIndex: number): Observable<void> {
    const body: ReportPositionRequest = { questionIndex };
    return this.http.put<void>(`/api/assignments/${assignmentId}/attempts/current/position`, body);
  }
}
