import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AssignmentResponse } from '../../../core/api/contracts';
import { AssignmentsApi } from '../../../core/api/assignments-api.service';
import { AttemptsApi } from '../../../core/api/attempts-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { AssignmentDetail } from './assignment-detail.component';

const assignment: AssignmentResponse = {
  id: '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001',
  openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  quizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
  snapshotId: '8f2b6c65-1a7c-4b4f-ad66-5e3b7f1f6001',
  snapshotTitle: 'Backend interview — working copy',
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

describe('AssignmentDetail', () => {
  let fixture: ComponentFixture<AssignmentDetail>;
  let inviteCalls: number;

  beforeEach(async () => {
    inviteCalls = 0;
    await TestBed.configureTestingModule({
      imports: [AssignmentDetail],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: assignment.id }) } },
        },
        {
          provide: AssignmentsApi,
          useValue: {
            get: () => of(assignment),
            invite: () => {
              inviteCalls += 1;
              return of({
                assignmentId: assignment.id,
                inviteUrl: `http://localhost:4200/attempt?token=invite-${inviteCalls}`,
              });
            },
          },
        },
        {
          provide: AttemptsApi,
          useValue: {
            listByAssignment: () =>
              of({
                items: [
                  {
                    id: 'attempt-1',
                    assignmentId: assignment.id,
                    openingId: assignment.openingId,
                    candidateEmail: assignment.candidateEmail,
                    status: 'inProgress',
                    resultStatus: 'incomplete',
                    startedAtUtc: '2026-01-01T00:00:00Z',
                    submittedAtUtc: null,
                    autoPointsAwarded: 0,
                    autoPointsAvailable: 1,
                    totalPointsAvailable: 1,
                  },
                ],
                page: 1,
                pageSize: 20,
                totalCount: 1,
              }),
          },
        },
      ],
    }).compileComponents();
    TestBed.inject(PermissionService).set([
      PermissionCodes.AssignmentsRead,
      PermissionCodes.AssignmentsWrite,
      PermissionCodes.AttemptsRead,
    ]);
  });

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(AssignmentDetail);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('confirms re-issue and leaves the prior url when cancelled', async () => {
    await render();
    expect(fixture.nativeElement.textContent).toContain('Incomplete');
    expect(fixture.nativeElement.textContent).not.toContain('incomplete');
    expect(
      fixture.nativeElement.querySelector(
        `a[href="/assignments/${assignment.id}/attempts/attempt-1"]`,
      ),
    ).not.toBeNull();

    issueButton().click();
    await fixture.whenStable();
    fixture.detectChanges();

    const firstUrl = inviteInput().value;
    expect(firstUrl).toContain('token=invite-1');
    expect(fixture.nativeElement.textContent).toContain('until the candidate submits');
    expect(fixture.nativeElement.textContent).toContain('sign-in session expires');
    expect(fixture.nativeElement.querySelector('[role="alertdialog"]')).toBeNull();
    expect(inviteCalls).toBe(1);

    issueButton().click();
    fixture.detectChanges();
    const dialog = fixture.nativeElement.querySelector('[role="alertdialog"]') as HTMLElement;
    expect(dialog.textContent).toContain('will stop working');
    expect(inviteCalls).toBe(1);

    cancelButton(dialog).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alertdialog"]')).toBeNull();
    expect(inviteInput().value).toBe(firstUrl);
    expect(inviteCalls).toBe(1);

    issueButton().click();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="confirm-rotate"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(inviteCalls).toBe(2);
    expect(inviteInput().value).toContain('token=invite-2');
    expect(fixture.nativeElement.textContent).toContain('The previous link no longer works');
  });

  function issueButton(): HTMLButtonElement {
    return [...fixture.nativeElement.querySelectorAll('button')].find((button: HTMLButtonElement) =>
      /invite link/i.test(button.textContent ?? ''),
    ) as HTMLButtonElement;
  }

  function inviteInput(): HTMLInputElement {
    return fixture.nativeElement.querySelector('[data-testid="invite-url"]') as HTMLInputElement;
  }

  function cancelButton(dialog: HTMLElement): HTMLButtonElement {
    return [...dialog.querySelectorAll('button')].find((button: HTMLButtonElement) =>
      button.textContent?.includes('Cancel'),
    ) as HTMLButtonElement;
  }
});
