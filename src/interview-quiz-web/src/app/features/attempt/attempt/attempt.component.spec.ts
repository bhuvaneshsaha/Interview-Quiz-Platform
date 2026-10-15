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
  let submitCalls: string[];
  let startedAttempt: CandidateAttemptResponse;

  beforeEach(async () => {
    consumeCalls = [];
    startCalls = [];
    submitCalls = [];
    startedAttempt = attempt;
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
              return of(startedAttempt);
            },
            saveAnswers: () => of(attempt),
            submit: (id: string) => {
              submitCalls.push(id);
              return of({
                ...attempt,
                status: 'submitted',
                resultStatus: 'complete',
                autoPointsAwarded: 1,
                autoPointsAvailable: 1,
                totalPointsAvailable: 1,
                itemResults: [],
              });
            },
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

  it('confirms manual submit and humanizes the result status', async () => {
    fixture = TestBed.createComponent(Attempt);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const submitButton = [...fixture.nativeElement.querySelectorAll('button')].find(
      (button: HTMLButtonElement) => button.textContent?.trim() === 'Submit quiz',
    ) as HTMLButtonElement;
    submitButton.click();
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="alertdialog"]') as HTMLElement;
    expect(dialog.textContent).toContain("You won't be able to change answers");
    expect(submitCalls).toEqual([]);

    const cancel = [...dialog.querySelectorAll('button')].find((button: HTMLButtonElement) =>
      button.textContent?.includes('Cancel'),
    ) as HTMLButtonElement;
    cancel.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alertdialog"]')).toBeNull();
    expect(submitCalls).toEqual([]);

    submitButton.click();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="confirm-submit"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(submitCalls).toEqual([assignmentId]);
    expect(fixture.nativeElement.textContent).toContain('Result: Complete');
    expect(fixture.nativeElement.textContent).not.toContain('Time ran out');
  });

  it('styles the timer and announces once under five minutes', async () => {
    startedAttempt = {
      ...attempt,
      dueAtUtc: new Date(Date.now() + 90_000).toISOString(),
      remainingSeconds: 90,
    };
    fixture = TestBed.createComponent(Attempt);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const timer = fixture.nativeElement.querySelector('[role="timer"]') as HTMLElement;
    expect(timer.classList.contains('timer-warn')).toBe(true);
    expect(timer.textContent).toContain('Less than 5 minutes.');
    const live = fixture.nativeElement.querySelector('[aria-live="polite"]') as HTMLElement;
    expect(live.textContent).toContain('Less than 5 minutes remaining.');
  });

  it('explains automatic submit when time runs out', async () => {
    fixture = TestBed.createComponent(Attempt);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    fixture.componentInstance.submit('timeout');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(submitCalls).toEqual([assignmentId]);
    expect(fixture.nativeElement.querySelector('[role="alertdialog"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Time ran out');
    expect(fixture.nativeElement.textContent).toContain("you can't change your answers");
  });
});
