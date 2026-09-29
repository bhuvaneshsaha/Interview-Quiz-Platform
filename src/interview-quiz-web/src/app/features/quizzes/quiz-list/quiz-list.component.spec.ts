import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { QuizResponse } from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { QuizzesApi } from '../../../core/api/quizzes-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { QuizList } from './quiz-list.component';

const sampleQuiz: QuizResponse = {
  id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
  openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  title: 'Backend interview — working copy',
  description: 'Sample',
  expectedExperienceYears: 5,
  tags: {},
  questions: [],
  rowVersion: 0,
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
};

describe('QuizList', () => {
  let fixture: ComponentFixture<QuizList>;
  let permissions: PermissionService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuizList],
      providers: [
        provideRouter([]),
        {
          provide: QuizzesApi,
          useValue: {
            list: () => of({ items: [sampleQuiz], page: 1, pageSize: 20, totalCount: 1 }),
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

  it('hides Create quiz without quizzes.write', async () => {
    permissions.set([PermissionCodes.QuizzesRead]);
    fixture = TestBed.createComponent(QuizList);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a[href="/quizzes/new"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Backend interview — working copy');
  });

  it('shows Create quiz when quizzes.write is granted', async () => {
    permissions.set([PermissionCodes.QuizzesRead, PermissionCodes.QuizzesWrite]);
    fixture = TestBed.createComponent(QuizList);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const create = fixture.nativeElement.querySelector('a[href="/quizzes/new"]');
    expect(create?.textContent).toContain('Create quiz');
  });
});
