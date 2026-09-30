import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { TemplateResponse, TemplateVersionResponse } from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { TemplatesApi } from '../../../core/api/templates-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { TemplateDetail } from './template-detail.component';

const sampleTemplate: TemplateResponse = {
  id: '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001',
  title: 'Backend interview template',
  description: 'Seeded template',
  expectedExperienceYears: 5,
  tags: { Role: 'Backend' },
  latestVersionId: '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3101',
  latestVersionNumber: 1,
  originQuizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
};

const sampleVersion: TemplateVersionResponse = {
  id: sampleTemplate.latestVersionId,
  templateId: sampleTemplate.id,
  versionNumber: 1,
  title: sampleTemplate.title,
  publishedFromQuizId: sampleTemplate.originQuizId,
  publishedAtUtc: '2026-01-01T00:00:00Z',
  questionCount: 1,
  description: sampleTemplate.description,
  expectedExperienceYears: 5,
  tags: sampleTemplate.tags,
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
};

describe('TemplateDetail', () => {
  let fixture: ComponentFixture<TemplateDetail>;
  let permissions: PermissionService;

  async function setup(codes: string[]): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [TemplateDetail],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ id: sampleTemplate.id }) },
          },
        },
        {
          provide: TemplatesApi,
          useValue: {
            get: () => of(sampleTemplate),
            listVersions: () =>
              of({ items: [sampleVersion], page: 1, pageSize: 20, totalCount: 1 }),
            getVersion: () => of(sampleVersion),
            clone: () =>
              of({
                id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
                openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
                title: 'Cloned',
                description: '',
                expectedExperienceYears: 5,
                tags: {},
                questions: [],
                originTemplateId: sampleTemplate.id,
                sourceTemplateVersionId: sampleVersion.id,
                rowVersion: 0,
                createdAtUtc: '2026-01-01T00:00:00Z',
                updatedAtUtc: '2026-01-01T00:00:00Z',
              }),
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
    fixture = TestBed.createComponent(TemplateDetail);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('hides clone without quizzes.write (templates.read only)', async () => {
    await setup([PermissionCodes.TemplatesRead]);
    expect(fixture.nativeElement.querySelector('[data-testid="clone-template"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Backend interview template');
  });

  it('shows clone when both quizzes.write and templates.read are granted', async () => {
    await setup([PermissionCodes.TemplatesRead, PermissionCodes.QuizzesWrite]);
    const clone = fixture.nativeElement.querySelector('[data-testid="clone-template"]');
    expect(clone).toBeTruthy();
    expect(clone?.textContent).toContain('Clone as quiz');
  });
});
