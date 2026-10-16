import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AcceptAiDraftRequest, AiDraftResponse } from '../../../core/api/contracts';
import { AiDraftsApi } from '../../../core/api/ai-drafts-api.service';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { ApiError } from '../../../core/http/api-error';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { AiDraftForm } from './ai-draft-form.component';

describe('AiDraftForm', () => {
  const openingId = '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001';
  let fixture: ComponentFixture<AiDraftForm>;
  let accepts: AcceptAiDraftRequest[];
  let acceptError: ApiError | null;

  const generated: AiDraftResponse = {
    id: 'draft-1',
    openingId,
    title: 'Draft: Senior backend engineer',
    expectedExperienceYears: 5,
    resumeText: 'Ten years of APIs.',
    rulesText: null,
    templateId: null,
    templateVersionId: null,
    model: 'fake',
    status: 'generated',
    acceptedQuizId: null,
    createdAtUtc: '2026-10-16T08:00:00.000Z',
    questions: [
      {
        id: 'q1',
        sortOrder: 0,
        type: 'trueFalse',
        stem: 'Draft check for Senior backend engineer.',
        scoringMode: 'auto',
        creditMode: null,
        points: 1,
        body: { correct: true },
        sourceQuestionId: null,
      },
      {
        id: 'q2',
        sortOrder: 1,
        type: 'longText',
        stem: 'Draft written answer from the resume.',
        scoringMode: 'humanOnly',
        creditMode: null,
        points: 5,
        body: { guidance: 'Review before use.' },
        sourceQuestionId: null,
      },
    ],
  };

  beforeEach(async () => {
    accepts = [];
    acceptError = null;
    await TestBed.configureTestingModule({
      imports: [AiDraftForm],
      providers: [
        provideRouter([]),
        {
          provide: OpeningsApi,
          useValue: {
            list: () =>
              of({
                items: [{ id: openingId, title: 'Senior backend engineer' }],
                page: 1,
                pageSize: 100,
                totalCount: 1,
              }),
          },
        },
        {
          provide: AiDraftsApi,
          useValue: {
            create: () => of(generated),
            accept: (_id: string, body: AcceptAiDraftRequest) => {
              accepts.push(body);
              if (acceptError) {
                return throwError(() => acceptError);
              }
              return of({ draftId: 'draft-1', quizId: 'quiz-1', title: generated.title });
            },
          },
        },
      ],
    }).compileComponents();
    TestBed.inject(PermissionService).set([
      PermissionCodes.AiDraftUse,
      PermissionCodes.QuizzesWrite,
      PermissionCodes.OpeningsRead,
    ]);
    fixture = TestBed.createComponent(AiDraftForm);
    fixture.detectChanges();
  });

  it('sends an edited stem when the draft is saved as a quiz', async () => {
    fixture.componentInstance.source.controls.openingId.setValue(openingId);
    fixture.componentInstance.source.controls.resumeText.setValue('Ten years of APIs.');
    fixture.componentInstance.generate();
    fixture.detectChanges();
    await fixture.whenStable();

    const stem = fixture.nativeElement.querySelector(
      '[data-testid="ai-draft-stem-0"]',
    ) as HTMLTextAreaElement;
    expect(fixture.nativeElement.textContent).toContain('This is a sample draft.');
    expect(fixture.nativeElement.textContent).not.toContain('Model fake');
    expect(stem.value).toContain('Draft check');
    stem.value = 'Draft check edited.';
    stem.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[data-testid="ai-draft-save"]').click();
    await fixture.whenStable();

    expect(accepts).toHaveLength(1);
    expect(accepts[0].questions[0].stem).toBe('Draft check edited.');
    expect(accepts[0].questions[0].stem).not.toBe(generated.questions[0].stem);
    expect(fixture.nativeElement.textContent).toContain('Quiz saved. It is not assigned.');
  });

  it('shows the server message when the questions were not edited', async () => {
    acceptError = new ApiError('Edit the draft before saving it as a quiz.', 400, null, 'Bad Request');
    fixture.componentInstance.source.controls.openingId.setValue(openingId);
    fixture.componentInstance.generate();
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.nativeElement.querySelector('[data-testid="ai-draft-save"]').click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('[data-testid="ai-draft-error"]').textContent).toContain(
      'Edit the draft before saving it as a quiz.',
    );
    expect(fixture.nativeElement.textContent).not.toContain('Quiz saved');
  });
});
