import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { compactCriteria } from './list-query';
import {
  CandidatesReportResponse,
  FunnelReportResponse,
  PendingReviewReportResponse,
  QuestionsReportResponse,
  ReportFilterCriteria,
  ReportResponse,
  ScoresReportResponse,
  TimeReportResponse,
} from './contracts';

@Injectable({
  providedIn: 'root',
})
export class ReportsApi {
  private readonly http = inject(HttpClient);

  load(route: string, criteria: ReportFilterCriteria, page: number, pageSize: number): Observable<ReportResponse> {
    return this.http.get<ReportResponse>(`/api/reports/${route}`, {
      params: this.params(criteria, page, pageSize),
    });
  }

  funnel(criteria: ReportFilterCriteria, page: number, pageSize: number): Observable<FunnelReportResponse> {
    return this.load('funnel', criteria, page, pageSize) as Observable<FunnelReportResponse>;
  }

  scores(criteria: ReportFilterCriteria, page: number, pageSize: number): Observable<ScoresReportResponse> {
    return this.load('scores', criteria, page, pageSize) as Observable<ScoresReportResponse>;
  }

  time(criteria: ReportFilterCriteria, page: number, pageSize: number): Observable<TimeReportResponse> {
    return this.load('time', criteria, page, pageSize) as Observable<TimeReportResponse>;
  }

  questions(criteria: ReportFilterCriteria, page: number, pageSize: number): Observable<QuestionsReportResponse> {
    return this.load('questions', criteria, page, pageSize) as Observable<QuestionsReportResponse>;
  }

  candidates(criteria: ReportFilterCriteria, page: number, pageSize: number): Observable<CandidatesReportResponse> {
    return this.load('candidates', criteria, page, pageSize) as Observable<CandidatesReportResponse>;
  }

  pendingReview(
    criteria: ReportFilterCriteria,
    page: number,
    pageSize: number,
  ): Observable<PendingReviewReportResponse> {
    return this.load('pending-review', criteria, page, pageSize) as Observable<PendingReviewReportResponse>;
  }

  exportCsv(route: string, criteria: ReportFilterCriteria): Observable<Blob> {
    return this.http.get(`/api/reports/${route}/export`, {
      params: this.params(criteria),
      responseType: 'blob',
    });
  }

  private params(criteria: ReportFilterCriteria, page?: number, pageSize?: number): HttpParams {
    let params = new HttpParams();
    if (page !== undefined && pageSize !== undefined) {
      params = params.set('page', page).set('pageSize', pageSize);
    }
    const compact = compactCriteria(criteria);
    if (Object.keys(compact).length > 0) {
      params = params.set('criteria', JSON.stringify(compact));
    }
    return params;
  }
}
