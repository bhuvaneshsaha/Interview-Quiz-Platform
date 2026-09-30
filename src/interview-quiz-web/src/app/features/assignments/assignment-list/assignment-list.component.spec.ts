import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AssignmentSummaryResponse } from '../../../core/api/contracts';
import { AssignmentsApi } from '../../../core/api/assignments-api.service';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { AssignmentList } from './assignment-list.component';

const sample: AssignmentSummaryResponse = {
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
};

describe('AssignmentList', () => {
  let fixture: ComponentFixture<AssignmentList>;
  let permissions: PermissionService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssignmentList],
      providers: [
        provideRouter([]),
        {
          provide: AssignmentsApi,
          useValue: {
            list: () => of({ items: [sample], page: 1, pageSize: 20, totalCount: 1 }),
          },
        },
        {
          provide: OpeningsApi,
          useValue: {
            list: () => of({ items: [], page: 1, pageSize: 100, totalCount: 0 }),
          },
        },
      ],
    }).compileComponents();
    permissions = TestBed.inject(PermissionService);
  });

  it('hides Create assignment without assignments.write', async () => {
    permissions.set([PermissionCodes.AssignmentsRead]);
    fixture = TestBed.createComponent(AssignmentList);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a[href="/assignments/new"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('candidate.dev@example.com');
  });

  it('shows Create assignment when assignments.write is granted', async () => {
    permissions.set([PermissionCodes.AssignmentsRead, PermissionCodes.AssignmentsWrite]);
    fixture = TestBed.createComponent(AssignmentList);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const create = fixture.nativeElement.querySelector('a[href="/assignments/new"]');
    expect(create?.textContent).toContain('Create assignment');
  });
});
