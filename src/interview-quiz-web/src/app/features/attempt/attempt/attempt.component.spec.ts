import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CandidateAttemptResponse, CandidateTokenResponse } from '../../../core/api/contracts';
import { AttemptsApi } from '../../../core/api/attempts-api.service';
import { MagicLinkApi } from '../../../core/api/magic-link-api.service';
import { CandidateSession } from '../../../core/auth/candidate-session.service';
import { Attempt } from './attempt.component';

const assignmentId = '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001';

const tokens: CandidateTokenResponse = {
  accessToken: 'candidate-1',
  accessTokenExpiresAt: '2099-01-01T00:00:00Z',
  tokenType: 'Bearer',
  assignmentId,
};

const attempt: CandidateAttemptResponse = {
  id: '9a3c7d76-2b8d-4c5e-be77-6f4c8a2a7001',
  assignmentId,
  status: 'inProgress',
  startedAtUtc: '2026-01-01T00:00:00Z',
  dueAtUtc: '2099-01-01T00:00:00Z',
  submittedAtUtc: null,
  remainingSeconds: 1800,
  questions: [
    {
      id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2103',
      sortOrder: 0,
      type: 'trueFalse',
      stem: 'A quiz belongs to exactly one opening.',
      scoringMode: 'auto',
      creditMode: null,
      points: 1,
      body: {},
      sourceQuestionId: null,
    },
  ],
  answers: [],
};

describe('Attempt', () => {
  let fixture: ComponentFixture<Attempt>;
  let consumeCalls: string[];
  let startCalls: string[];

  beforeEach(async () => {
    consumeCalls = [];
    startCalls = [];
    await TestBed.configureTestingModule({
      imports: [Attempt],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap({ token: 'opaque-token' }) },
          },
        },
        {
          provide: MagicLinkApi,
          useValue: {
            consume: (token: string) => {
              consumeCalls.push(token);
              return of(tokens);
            },
          },
        },
        {
          provide: AttemptsApi,
          useValue: {
            start: (id: string) => {
              startCalls.push(id);
              return of(attempt);
            },
            saveAnswers: () => of(attempt),
            submit: () =>
              of({
                ...attempt,
                status: 'submitted',
                resultStatus: 'complete',
                autoPointsAwarded: 1,
                autoPointsAvailable: 1,
                totalPointsAvailable: 1,
                itemResults: [],
              }),
          },
        },
      ],
    }).compileComponents();
  });

  afterEach(() => {
    fixture?.destroy();
    TestBed.inject(CandidateSession).leaveAttempt();
  });

  it('consumes the token query and starts the attempt outside the employee shell', async () => {
    fixture = TestBed.createComponent(Attempt);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(consumeCalls).toEqual(['opaque-token']);
    expect(startCalls).toEqual([assignmentId]);
    expect(fixture.nativeElement.querySelector('app-shell')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/assignments"]')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Sign out');
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Quiz attempt');
    expect(fixture.nativeElement.textContent).toContain('A quiz belongs to exactly one opening.');
    expect(TestBed.inject(CandidateSession).accessToken()).toBe('candidate-1');
  });
});
