import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { QuizResponse } from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { QuizzesApi } from '../../../core/api/quizzes-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { QuizForm } from './quiz-form.component';

const sampleQuiz: QuizResponse = {
  id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
  openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  title: 'Backend interview — working copy',
  description: 'Sample authoring quiz',
  expectedExperienceYears: 5,
  tags: { Role: 'Backend' },
  questions: [
    {
      id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2103',
      sortOrder: 0,
      type: 'trueFalse',
      stem: 'A quiz belongs to exactly one opening.',
      scoringMode: 'auto',
      creditMode: null,
      points: 1,
      body: { correct: true },
    },
  ],
  rowVersion: 1,
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-02T00:00:00Z',
};

describe('QuizForm', () => {
  let fixture: ComponentFixture<QuizForm>;
  let permissions: PermissionService;

  async function setup(codes: string[], quizId: string | null): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [QuizForm],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap(quizId ? { id: quizId } : {}) },
          },
        },
        {
          provide: QuizzesApi,
          useValue: {
            get: () => of(sampleQuiz),
            create: () => of(sampleQuiz),
            update: () => of({ ...sampleQuiz, rowVersion: 2 }),
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
    permissions.set(codes);
    fixture = TestBed.createComponent(QuizForm);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('disables the editor and hides save without quizzes.write', async () => {
    await setup([PermissionCodes.QuizzesRead], sampleQuiz.id);
    expect(fixture.componentInstance.canWrite).toBe(false);
    expect(fixture.componentInstance.form.disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('[data-testid="save-quiz"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('#title')).toBeTruthy();
  });

  it('keeps the editor enabled and shows save when quizzes.write is granted', async () => {
    await setup([PermissionCodes.QuizzesRead, PermissionCodes.QuizzesWrite], sampleQuiz.id);
    expect(fixture.componentInstance.canWrite).toBe(true);
    expect(fixture.componentInstance.form.disabled).toBe(false);
    const save = fixture.nativeElement.querySelector('[data-testid="save-quiz"]');
    expect(save).toBeTruthy();
    expect(save?.getAttribute('disabled')).toBeNull();
  });
});
