import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AttemptResultResponse } from '../../../core/api/contracts';
import { AttemptsApi } from '../../../core/api/attempts-api.service';
import { ApiError } from '../../../core/http/api-error';
import { AttemptDetail } from './attempt-detail.component';

const assignmentId = '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001';
const attemptId = '9a3c7d76-2b8d-4c5e-be77-6f4c8a2a7001';

const detail: AttemptResultResponse = {
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
  items: [
    {
      questionId: 'q-auto',
      sortOrder: 0,
      type: 'shortText',
      scoringMode: 'auto',
      points: 1,
      status: 'scored',
      pointsAwarded: 1,
      stem: 'A quiz belongs to exactly one opening.',
      candidateAnswer: { text: 'Yes', isCorrect: true },
    },
    {
      questionId: 'q-human',
      sortOrder: 1,
      type: 'longText',
      scoringMode: 'humanOnly',
      points: 5,
      status: 'unsettled',
      pointsAwarded: null,
      stem: 'Describe how a snapshot freezes the quiz.',
      candidateAnswer: { text: 'The assign step copies the quiz.' },
    },
  ],
};

describe('AttemptDetail', () => {
  let fixture: ComponentFixture<AttemptDetail>;
  let result: AttemptResultResponse | null;
  let fail: boolean;

  beforeEach(async () => {
    result = detail;
    fail = false;
    await TestBed.configureTestingModule({
      imports: [AttemptDetail],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ assignmentId, attemptId }),
            },
          },
        },
        {
          provide: AttemptsApi,
          useValue: {
            get: () => (fail ? throwError(() => new ApiError('Attempt not found.', 404, null, 'Not found')) : of(result)),
          },
        },
      ],
    }).compileComponents();
  });

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(AttemptDetail);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows stem, answer, and points without answer keys', async () => {
    await render();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('A quiz belongs to exactly one opening.');
    expect(text).toContain('Yes');
    expect(text).toContain('1 / 1 points');
    expect(text).toContain('Describe how a snapshot freezes the quiz.');
    expect(text).toContain('The assign step copies the quiz.');
    expect(text).toContain('Awaiting human review');
    expect(text).not.toContain('isCorrect');
    expect(text).not.toContain('failed');
    expect(text).not.toContain('0 / 5');
    expect(fixture.nativeElement.querySelector('a[href="/assignments/' + assignmentId + '"]')).not.toBeNull();
  });

  it('uses page status when the attempt has no items', async () => {
    result = { ...detail, items: [] };
    await render();
    expect(fixture.nativeElement.querySelector('.status-empty')?.textContent).toContain(
      'No question results for this attempt.',
    );
  });

  it('uses page status for a load error', async () => {
    fail = true;
    await render();
    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
    expect(alert.textContent).toContain('Attempt not found.');
  });
});
