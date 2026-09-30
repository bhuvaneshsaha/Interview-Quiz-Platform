import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { BankQuestionResponse } from '../../../core/api/contracts';
import { QuestionsApi } from '../../../core/api/questions-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { QuestionList } from './question-list.component';

const sampleQuestion: BankQuestionResponse = {
  id: '6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4001',
  title: 'MC single bank item',
  tags: { Topic: 'HTTP' },
  expectedExperienceYears: 3,
  type: 'multipleChoiceSingle',
  stem: 'Which HTTP status means a resource was created?',
  scoringMode: 'auto',
  creditMode: null,
  points: 1,
  body: {
    options: [
      { id: 'opt-201', text: '201 Created', isCorrect: true },
      { id: 'opt-200', text: '200 OK', isCorrect: false },
    ],
  },
  archivedAtUtc: null,
  rowVersion: 1,
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-02T00:00:00Z',
};

describe('QuestionList', () => {
  let fixture: ComponentFixture<QuestionList>;
  let permissions: PermissionService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuestionList],
      providers: [
        provideRouter([]),
        {
          provide: QuestionsApi,
          useValue: {
            list: () => of({ items: [sampleQuestion], page: 1, pageSize: 20, totalCount: 1 }),
          },
        },
      ],
    }).compileComponents();
    permissions = TestBed.inject(PermissionService);
  });

  it('hides Create question without questions.write', async () => {
    permissions.set([PermissionCodes.QuestionsRead]);
    fixture = TestBed.createComponent(QuestionList);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a[href="/questions/new"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('#question-archived')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('MC single bank item');
    expect(fixture.nativeElement.querySelector('app-saved-filters')).toBeNull();
  });

  it('shows Create question when questions.write is granted', async () => {
    permissions.set([PermissionCodes.QuestionsRead, PermissionCodes.QuestionsWrite]);
    fixture = TestBed.createComponent(QuestionList);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const create = fixture.nativeElement.querySelector('a[href="/questions/new"]');
    expect(create?.textContent).toContain('Create question');
    expect(fixture.nativeElement.querySelector('#question-archived')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-saved-filters')).toBeNull();
  });
});
