import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { BankQuestionResponse } from '../../../core/api/contracts';
import { QuestionsApi } from '../../../core/api/questions-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { QuestionForm } from './question-form.component';

const sampleQuestion: BankQuestionResponse = {
  id: '6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4002',
  title: 'True/false bank item',
  tags: { Topic: 'Catalog' },
  expectedExperienceYears: 2,
  type: 'trueFalse',
  stem: 'A quiz belongs to exactly one opening.',
  scoringMode: 'auto',
  creditMode: null,
  points: 1,
  body: { correct: true },
  archivedAtUtc: null,
  rowVersion: 1,
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-02T00:00:00Z',
};

describe('QuestionForm', () => {
  let fixture: ComponentFixture<QuestionForm>;
  let permissions: PermissionService;

  async function setup(codes: string[], questionId: string | null, archived = false): Promise<void> {
    TestBed.resetTestingModule();
    const question: BankQuestionResponse = {
      ...sampleQuestion,
      archivedAtUtc: archived ? '2026-02-01T00:00:00Z' : null,
    };
    await TestBed.configureTestingModule({
      imports: [QuestionForm],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap(questionId ? { id: questionId } : {}) },
          },
        },
        {
          provide: QuestionsApi,
          useValue: {
            get: () => of(question),
            create: () => of(question),
            update: () => of({ ...question, rowVersion: 2 }),
            archive: () => of({ ...question, archivedAtUtc: '2026-02-01T00:00:00Z' }),
            unarchive: () => of({ ...question, archivedAtUtc: null }),
          },
        },
      ],
    }).compileComponents();
    permissions = TestBed.inject(PermissionService);
    permissions.set(codes);
    fixture = TestBed.createComponent(QuestionForm);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('disables the editor and hides save without questions.write', async () => {
    await setup([PermissionCodes.QuestionsRead], sampleQuestion.id);
    expect(fixture.componentInstance.canWrite).toBe(false);
    expect(fixture.componentInstance.form.disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('[data-testid="save-question"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="archive-question"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('#title')).toBeTruthy();
  });

  it('keeps the editor enabled and shows save when questions.write is granted', async () => {
    await setup([PermissionCodes.QuestionsRead, PermissionCodes.QuestionsWrite], sampleQuestion.id);
    expect(fixture.componentInstance.canWrite).toBe(true);
    expect(fixture.componentInstance.form.disabled).toBe(false);
    const save = fixture.nativeElement.querySelector('[data-testid="save-question"]');
    expect(save).toBeTruthy();
    expect(save?.getAttribute('disabled')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="archive-question"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="unarchive-question"]')).toBeNull();
  });

  it('hides archive and unarchive on create-new even with questions.write', async () => {
    await setup([PermissionCodes.QuestionsRead, PermissionCodes.QuestionsWrite], null);
    expect(fixture.nativeElement.querySelector('[data-testid="archive-question"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="unarchive-question"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="save-question"]')).toBeTruthy();
  });

  it('shows Unarchive when the bank item is archived and questions.write is granted', async () => {
    await setup(
      [PermissionCodes.QuestionsRead, PermissionCodes.QuestionsWrite],
      sampleQuestion.id,
      true,
    );
    expect(fixture.nativeElement.querySelector('[data-testid="archive-question"]')).toBeNull();
    const unarchive = fixture.nativeElement.querySelector('[data-testid="unarchive-question"]');
    expect(unarchive).toBeTruthy();
    expect(unarchive?.textContent).toContain('Unarchive');
  });
});
