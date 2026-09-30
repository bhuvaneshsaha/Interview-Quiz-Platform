import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { QuizResponse } from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { QuestionsApi } from '../../../core/api/questions-api.service';
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
      sourceQuestionId: null,
    },
  ],
  originTemplateId: null,
  sourceTemplateVersionId: null,
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
            includeQuestions: () => of({ ...sampleQuiz, rowVersion: 3 }),
            publishTemplate: () =>
              of({
                templateId: '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001',
                versionId: '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3101',
                versionNumber: 1,
                createdNewTemplate: true,
              }),
          },
        },
        {
          provide: QuestionsApi,
          useValue: {
            list: () => of({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
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

  it('hides Publish as template without templates.write', async () => {
    await setup([PermissionCodes.QuizzesRead, PermissionCodes.QuizzesWrite], sampleQuiz.id);
    expect(fixture.nativeElement.querySelector('[data-testid="publish-template"]')).toBeNull();
  });

  it('hides Publish as template on create-new even with templates.write', async () => {
    await setup(
      [PermissionCodes.QuizzesRead, PermissionCodes.QuizzesWrite, PermissionCodes.TemplatesWrite],
      null,
    );
    expect(fixture.nativeElement.querySelector('[data-testid="publish-template"]')).toBeNull();
  });

  it('shows Publish as template when templates.write is granted on an existing quiz', async () => {
    await setup(
      [PermissionCodes.QuizzesRead, PermissionCodes.QuizzesWrite, PermissionCodes.TemplatesWrite],
      sampleQuiz.id,
    );
    const publish = fixture.nativeElement.querySelector('[data-testid="publish-template"]');
    expect(publish).toBeTruthy();
    expect(publish?.textContent).toContain('Publish as template');
  });

  it('hides include from bank without questions.read', async () => {
    await setup([PermissionCodes.QuizzesRead, PermissionCodes.QuizzesWrite], sampleQuiz.id);
    expect(fixture.nativeElement.querySelector('[data-testid="include-from-bank"]')).toBeNull();
  });

  it('hides include from bank on create-new even with both permissions', async () => {
    await setup(
      [PermissionCodes.QuizzesRead, PermissionCodes.QuizzesWrite, PermissionCodes.QuestionsRead],
      null,
    );
    expect(fixture.nativeElement.querySelector('[data-testid="include-from-bank"]')).toBeNull();
  });

  it('shows include from bank when quizzes.write and questions.read on an existing quiz', async () => {
    await setup(
      [PermissionCodes.QuizzesRead, PermissionCodes.QuizzesWrite, PermissionCodes.QuestionsRead],
      sampleQuiz.id,
    );
    expect(fixture.nativeElement.querySelector('[data-testid="include-from-bank"]')).toBeTruthy();
  });
});
