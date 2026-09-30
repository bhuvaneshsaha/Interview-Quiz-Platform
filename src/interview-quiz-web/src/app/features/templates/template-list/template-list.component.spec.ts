import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { TemplateSummaryResponse } from '../../../core/api/contracts';
import { TemplatesApi } from '../../../core/api/templates-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { savedFilterTestProviders } from '../../../shared/saved-filters/saved-filters.testing';
import { TemplateList } from './template-list.component';

const sampleTemplate: TemplateSummaryResponse = {
  id: '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001',
  title: 'Backend interview template',
  description: 'Seeded',
  expectedExperienceYears: 5,
  tags: {},
  latestVersionId: '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3101',
  latestVersionNumber: 1,
  originQuizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
  updatedAtUtc: '2026-01-01T00:00:00Z',
};

describe('TemplateList', () => {
  let fixture: ComponentFixture<TemplateList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TemplateList],
      providers: [
        provideRouter([]),
        {
          provide: TemplatesApi,
          useValue: {
            list: () => of({ items: [sampleTemplate], page: 1, pageSize: 20, totalCount: 1 }),
          },
        },
        ...savedFilterTestProviders(),
      ],
    }).compileComponents();
    TestBed.inject(PermissionService).set([PermissionCodes.TemplatesRead]);
    fixture = TestBed.createComponent(TemplateList);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('lists templates and links to detail', () => {
    expect(fixture.nativeElement.textContent).toContain('Backend interview template');
    expect(
      fixture.nativeElement.querySelector(`a[href="/templates/${sampleTemplate.id}"]`),
    ).toBeTruthy();
  });
});
