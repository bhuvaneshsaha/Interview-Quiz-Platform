import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AttemptReviewResponse } from '../../../core/api/contracts';
import { AttemptsApi } from '../../../core/api/attempts-api.service';
import { ApiError } from '../../../core/http/api-error';
import { AttemptReview } from './attempt-review.component';

const assignmentId = '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001';
const attemptId = '9a3c7d76-2b8d-4c5e-be77-6f4c8a2a7001';
const humanId = 'q-human';

const review: AttemptReviewResponse = {
  id: attemptId,
  assignmentId,
  openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  candidateEmail: 'candidate.dev@example.com',
  status: 'submitted',
  resultStatus: 'incomplete',
  startedAtUtc: '2026-01-01T00:00:00Z',
  submittedAtUtc: '2026-01-01T00:20:00Z',
  autoPointsAwarded: 1,
  autoPointsAvailable: 1,
  totalPointsAvailable: 6,
  finalPointsAwarded: null,
  items: [
    {
      questionId: 'q-auto',
      sortOrder: 0,
      type: 'trueFalse',
      scoringMode: 'auto',
      points: 1,
      status: 'scored',
      pointsAwarded: 1,
      stem: 'A quiz belongs to exactly one opening.',
      candidateAnswer: { value: true },
      reviewable: false,
      answerKey: { correct: true },
    },
    {
      questionId: humanId,
      sortOrder: 1,
      type: 'longText',
      scoringMode: 'humanOnly',
      points: 5,
      status: 'unsettled',
      pointsAwarded: null,
      stem: 'Describe how a snapshot freezes the quiz.',
      candidateAnswer: { text: 'The assign step copies the quiz.' },
      reviewable: true,
      answerKey: { guidance: 'Mention the copy.' },
    },
  ],
};

describe('AttemptReview', () => {
  let fixture: ComponentFixture<AttemptReview>;
  let saved: { questionId: string; pointsAwarded: number; comment: string | null } | null;

  beforeEach(async () => {
    saved = null;
    await TestBed.configureTestingModule({
      imports: [AttemptReview],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ assignmentId, attemptId }) },
          },
        },
        {
          provide: AttemptsApi,
          useValue: {
            getReview: () => of(review),
            scoreItem: (_attemptId: string, questionId: string, body: { pointsAwarded: number; comment: string | null }) => {
              saved = { questionId, ...body };
              return of({
                ...review,
                resultStatus: 'complete',
                finalPointsAwarded: 6,
                items: review.items.map((item) =>
                  item.questionId === questionId
                    ? { ...item, status: 'scored', pointsAwarded: body.pointsAwarded, reviewComment: body.comment }
                    : item,
                ),
              });
            },
          },
        },
      ],
    }).compileComponents();
  });

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(AttemptReview);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows the key for a reviewable item and saves a score', async () => {
    await render();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Describe how a snapshot freezes the quiz.');
    expect(text).toContain('No keyed answer. Guidance: Mention the copy.');
    expect(text).toContain('Not finalised');
    expect(text).not.toContain('opt-');

    fixture.componentInstance.updateDraft(humanId, { points: '5', comment: 'Covers the copy.' });
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector(`[data-testid="save-score-${humanId}"]`) as HTMLButtonElement;
    button.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(saved?.questionId).toBe(humanId);
    expect(saved?.pointsAwarded).toBe(5);
    expect(fixture.nativeElement.textContent).toContain('6 / 6');
  });

  it('uses page status when review cannot be loaded', async () => {
    const api = TestBed.inject(AttemptsApi);
    api.getReview = () => throwError(() => new ApiError('Review is not available.', 403, null, 'Forbidden'));
    await render();
    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
    expect(alert.textContent).toContain('Review is not available.');
  });
});
