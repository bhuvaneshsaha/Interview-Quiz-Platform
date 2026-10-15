import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AssignmentResponse, LiveProgressResponse } from '../../../core/api/contracts';
import { AssignmentsApi } from '../../../core/api/assignments-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { LiveSession } from './live-session.component';

const assignmentId = '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001';

const assignment: AssignmentResponse = {
  id: assignmentId,
  openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  quizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
  snapshotId: '8f2b6c65-1a7c-4b4f-ad66-5e3b7f1f6001',
  snapshotTitle: 'Backend interview',
  snapshotQuestionCount: 8,
  snapshotQuizRowVersion: 1,
  candidateEmail: 'candidate.dev@example.com',
  mode: 'live',
  overallDurationMinutes: 30,
  attemptLimit: 1,
  status: 'notStarted',
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
  createdByUserId: 'user-1',
};

const lobby: LiveProgressResponse = {
  assignmentId,
  state: 'lobby',
  candidateConnected: true,
  lastActivityAtUtc: '2026-10-15T08:00:00Z',
  currentQuestionIndex: null,
  questionCount: 8,
  answeredCount: 0,
  flaggedCount: 0,
  remainingSeconds: null,
  totalPausedSeconds: 0,
};

describe('LiveSession', () => {
  let fixture: ComponentFixture<LiveSession>;

  async function render(permissions: string[]): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [LiveSession],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: assignmentId }) } },
        },
        {
          provide: AssignmentsApi,
          useValue: {
            get: () => of(assignment),
            liveProgress: () => of(lobby),
            startLive: () => of({ ...lobby, state: 'running', remainingSeconds: 1800 }),
            pauseLive: () => of(lobby),
            resumeLive: () => of(lobby),
            endLive: () => of(lobby),
          },
        },
      ],
    }).compileComponents();
    TestBed.inject(PermissionService).set(permissions);
    fixture = TestBed.createComponent(LiveSession);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => {
    fixture?.destroy();
    TestBed.inject(PermissionService).clear();
  });

  it('shows progress and hides host controls without sessions.live.run', async () => {
    await render([PermissionCodes.AttemptsRead]);
    const text = fixture.nativeElement.textContent as string;
    expect(fixture.nativeElement.querySelector('[data-testid="live-state"]').textContent).toContain('Lobby');
    expect(fixture.nativeElement.querySelector('[data-testid="live-connected"]').textContent).toContain('Yes');
    expect(fixture.nativeElement.querySelector('[data-testid="live-answered"]').textContent).toContain('0');
    expect(fixture.nativeElement.querySelector('[data-testid="live-flagged"]').textContent).toContain('0');
    expect(text).not.toContain('candidate.dev@example.com');
    expect(fixture.nativeElement.querySelector('[data-testid="live-start"]')).toBeNull();
  });

  it('enables Start when the candidate is in the lobby', async () => {
    await render([PermissionCodes.SessionsLiveRun, PermissionCodes.AssignmentsRead]);
    const start = fixture.nativeElement.querySelector('[data-testid="live-start"]') as HTMLButtonElement;
    expect(start.disabled).toBe(false);
    expect((fixture.nativeElement.querySelector('[data-testid="live-pause"]') as HTMLButtonElement).disabled).toBe(
      true,
    );
  });
});
