import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import {
  CandidateAttemptPreview,
  CandidateAttemptResponse,
  CandidateTokenResponse,
} from '../../../core/api/contracts';
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

const previewWelcome: CandidateAttemptPreview = {
  attemptStatus: 'notStarted',
  quizTitle: 'Backend interview — working copy',
  openingName: 'Senior backend engineer',
  durationMinutes: 30,
  questionCount: 8,
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
      id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2102',
      sortOrder: 0,
      type: 'multipleChoiceMulti',
      stem: 'Which of these are ASP.NET Core hosting concerns?',
      body: {
        options: [
          { id: 'opt-kestrel', text: 'Kestrel' },
          { id: 'opt-health', text: 'Health checks' },
        ],
      },
      sourceQuestionId: null,
    },
    {
      id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2108',
      sortOrder: 1,
      type: 'ordering',
      stem: 'Order the request pipeline from first to last.',
      body: {
        items: [
          { id: 'item-authn', text: 'Authentication' },
          { id: 'item-authz', text: '' },
        ],
      },
      sourceQuestionId: null,
    },
  ],
  answers: [],
};

describe('Attempt', () => {
  let fixture: ComponentFixture<Attempt>;
  let consumeCalls: string[];
  let previewCalls: string[];
  let currentCalls: string[];
  let startCalls: string[];
  let submitCalls: string[];
  let startedAttempt: CandidateAttemptResponse;
  let previewResponse: CandidateAttemptPreview;

  beforeEach(async () => {
    consumeCalls = [];
    previewCalls = [];
    currentCalls = [];
    startCalls = [];
    submitCalls = [];
    startedAttempt = attempt;
    previewResponse = previewWelcome;
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
            preview: (id: string) => {
              previewCalls.push(id);
              return of(previewResponse);
            },
            getCurrent: (id: string) => {
              currentCalls.push(id);
              return of(startedAttempt);
            },
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

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(Attempt);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows a welcome page and starts the timer only from Start quiz', async () => {
    await render();

    expect(consumeCalls).toEqual(['opaque-token']);
    expect(previewCalls).toEqual([assignmentId]);
    expect(startCalls).toEqual([]);
    expect(currentCalls).toEqual([]);
    expect(fixture.nativeElement.querySelector('app-shell')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/assignments"]')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Sign out');
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Backend interview — working copy');
    expect(text).toContain('Senior backend engineer');
    expect(text).toContain('30 minutes');
    expect(text).toContain('8');
    expect(text).toContain('Start quiz');
    expect(text).toContain("Don't close this tab");
    expect(text).not.toContain('Which of these are ASP.NET Core hosting concerns?');
    expect(fixture.nativeElement.querySelector('[role="timer"]')).toBeNull();
    expect(TestBed.inject(CandidateSession).accessToken()).toBe('candidate-1');

    (fixture.nativeElement.querySelector('button.button') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(startCalls).toEqual([assignmentId]);
    const started = fixture.nativeElement.textContent as string;
    expect(started).toContain('Which of these are ASP.NET Core hosting concerns?');
    expect(started).toContain('Select all that apply.');
    expect(started).toContain('Kestrel');
    expect(started).toContain('Health checks');
    expect(started).toContain('Question 1');
    expect(started).not.toContain('Multiple choice');
    expect(started).not.toContain('pts');
    expect(started).not.toContain('opt-kestrel');
    expect(started).toContain('(untitled option)');
    expect(started).not.toContain('item-authz');
    expect(fixture.nativeElement.querySelector('[cdkDragHandle], .drag-handle')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[role="timer"]')).not.toBeNull();
  });

  it('skips the welcome page when the attempt is already in progress', async () => {
    previewResponse = { ...previewWelcome, attemptStatus: 'inProgress' };
    await render();

    expect(startCalls).toEqual([]);
    expect(currentCalls).toEqual([assignmentId]);
    expect(fixture.nativeElement.textContent).not.toContain('Start quiz');
    expect(fixture.nativeElement.textContent).toContain('Which of these are ASP.NET Core hosting concerns?');
    expect(fixture.nativeElement.querySelector('[role="timer"]')).not.toBeNull();
  });

  it('confirms manual submit and humanizes the result status', async () => {
    previewResponse = { ...previewWelcome, attemptStatus: 'inProgress' };
    await render();

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
    previewResponse = { ...previewWelcome, attemptStatus: 'inProgress' };
    startedAttempt = {
      ...attempt,
      dueAtUtc: new Date(Date.now() + 90_000).toISOString(),
      remainingSeconds: 90,
    };
    await render();

    const timer = fixture.nativeElement.querySelector('[role="timer"]') as HTMLElement;
    expect(timer.classList.contains('timer-warn')).toBe(true);
    expect(timer.textContent).toContain('Less than 5 minutes.');
    const live = fixture.nativeElement.querySelector('[aria-live="polite"]') as HTMLElement;
    expect(live.textContent).toContain('Less than 5 minutes remaining.');
  });

  it('explains automatic submit when time runs out', async () => {
    previewResponse = { ...previewWelcome, attemptStatus: 'inProgress' };
    await render();

    fixture.componentInstance.submit('timeout');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(submitCalls).toEqual([assignmentId]);
    expect(fixture.nativeElement.querySelector('[role="alertdialog"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Time ran out');
    expect(fixture.nativeElement.textContent).toContain("you can't change your answers");
  });
});
