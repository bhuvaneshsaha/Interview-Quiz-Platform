import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import {
  AssignmentResponse,
  OpeningResponse,
  QuizResponse,
} from '../../../core/api/contracts';
import { AssignmentsApi } from '../../../core/api/assignments-api.service';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { QuizzesApi } from '../../../core/api/quizzes-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { AssignmentForm } from './assignment-form.component';

const opening: OpeningResponse = {
  id: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  title: 'Backend engineer',
  jobDescription: '',
  owner: 'recruiter.dev@example.com',
  startDate: '2026-01-01',
  expectedCloseDate: null,
  headcount: 1,
  expectedExperienceYears: 5,
  handlers: [],
  tags: {},
  rowVersion: 1,
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
};

const quiz: QuizResponse = {
  id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
  openingId: opening.id,
  title: 'Backend interview — working copy',
  description: '',
  expectedExperienceYears: 5,
  tags: {},
  questions: [],
  originTemplateId: null,
  sourceTemplateVersionId: null,
  rowVersion: 1,
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
};

const createdBase: AssignmentResponse = {
  id: '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001',
  openingId: opening.id,
  quizId: quiz.id,
  snapshotId: '8f2b6c65-1a7c-4b4f-ad66-5e3b7f1f6001',
  snapshotTitle: quiz.title,
  snapshotQuestionCount: 3,
  candidateEmail: 'candidate.dev@example.com',
  mode: 'async',
  overallDurationMinutes: 30,
  attemptLimit: 1,
  status: 'notStarted',
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
  createdByUserId: 'user-1',
};

describe('AssignmentForm', () => {
  let fixture: ComponentFixture<AssignmentForm>;
  let permissions: PermissionService;
  let createResponse: AssignmentResponse;

  beforeEach(async () => {
    createResponse = {
      ...createdBase,
      mode: 'async',
      inviteUrl: 'http://localhost:4200/attempt?token=opaque',
    };
    await TestBed.configureTestingModule({
      imports: [AssignmentForm],
      providers: [
        provideRouter([]),
        {
          provide: AssignmentsApi,
          useValue: {
            create: () => of(createResponse),
          },
        },
        {
          provide: OpeningsApi,
          useValue: {
            list: () => of({ items: [opening], page: 1, pageSize: 100, totalCount: 1 }),
          },
        },
        {
          provide: QuizzesApi,
          useValue: {
            list: () => of({ items: [quiz], page: 1, pageSize: 100, totalCount: 1 }),
          },
        },
      ],
    }).compileComponents();
    permissions = TestBed.inject(PermissionService);
    permissions.set([
      PermissionCodes.AssignmentsWrite,
      PermissionCodes.OpeningsRead,
      PermissionCodes.QuizzesRead,
    ]);
  });

  async function fillAndSubmit(mode: 'async' | 'live'): Promise<void> {
    fixture = TestBed.createComponent(AssignmentForm);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.form.patchValue({
      openingId: opening.id,
      quizId: quiz.id,
      candidateEmail: 'candidate.dev@example.com',
      mode,
      overallDurationMinutes: 30,
      attemptLimit: 1,
    });
    fixture.detectChanges();
    fixture.componentInstance.submit();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows the invite copy control after async create', async () => {
    await fillAndSubmit('async');
    const invite = fixture.nativeElement.querySelector('[data-testid="invite-url"]') as HTMLInputElement;
    expect(invite).not.toBeNull();
    expect(invite.value).toContain('/attempt?token=opaque');
    expect(fixture.nativeElement.textContent).toContain('until the candidate submits');
    expect(fixture.nativeElement.textContent).toContain('sign-in session expires');
  });

  it('hides invite copy for live create', async () => {
    createResponse = {
      ...createdBase,
      mode: 'live',
      overallDurationMinutes: null,
      inviteUrl: null,
    };
    await fillAndSubmit('live');
    expect(fixture.nativeElement.querySelector('[data-testid="invite-url"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('slice 6');
  });

  it('keeps Live mode disabled until slice 6', async () => {
    fixture = TestBed.createComponent(AssignmentForm);
    fixture.detectChanges();
    await fixture.whenStable();
    const live = fixture.nativeElement.querySelector('input[value="live"]') as HTMLInputElement;
    expect(live.disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Coming soon');
  });

  it('describes invalid fields the same way as login', async () => {
    fixture = TestBed.createComponent(AssignmentForm);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.submit();
    fixture.detectChanges();

    const email = fixture.nativeElement.querySelector('#candidateEmail') as HTMLInputElement;
    expect(email.getAttribute('aria-invalid')).toBe('true');
    expect(email.getAttribute('aria-describedby')).toBe('candidate-email-error');
    expect(fixture.nativeElement.querySelector('#candidate-email-error')).not.toBeNull();

    const opening = fixture.nativeElement.querySelector('#openingId') as HTMLSelectElement;
    expect(opening.getAttribute('aria-invalid')).toBe('true');
    expect(opening.getAttribute('aria-describedby')).toBe('opening-error');
  });

  it('shows an alert when the invite link cannot be copied', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: () => Promise.reject(new Error('denied')) },
    });
    await fillAndSubmit('async');
    const copy = [...fixture.nativeElement.querySelectorAll('button')].find((button: HTMLButtonElement) =>
      button.textContent?.includes('Copy invite link'),
    ) as HTMLButtonElement;
    copy.click();
    await fixture.whenStable();
    fixture.detectChanges();
    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
    expect(alert.textContent).toContain('Could not copy the invite link');
  });
});
