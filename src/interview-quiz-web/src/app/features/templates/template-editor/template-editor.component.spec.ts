import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { SaveTemplateContentRequest, TemplateResponse, TemplateVersionResponse } from '../../../core/api/contracts';
import { TemplatesApi } from '../../../core/api/templates-api.service';
import { TemplateEditor } from './template-editor.component';

const template: TemplateResponse = {
  id: '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001',
  title: 'Backend interview template',
  description: 'Seeded template',
  expectedExperienceYears: 5,
  tags: { Role: 'Backend' },
  latestVersionId: '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3101',
  latestVersionNumber: 2,
  originQuizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
  rowVersion: 3,
};

const version: TemplateVersionResponse = {
  id: template.latestVersionId,
  templateId: template.id,
  versionNumber: 2,
  title: template.title,
  publishedFromQuizId: template.originQuizId,
  publishedAtUtc: '2026-01-02T00:00:00Z',
  publishedByUserId: 'author-1',
  publishedByDisplayName: 'Ada Author',
  changeNote: 'Rename only',
  questionCount: 2,
  description: template.description,
  expectedExperienceYears: 5,
  tags: template.tags,
  questions: [
    {
      id: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
      sortOrder: 0,
      type: 'dragDropSharedBank',
      stem: 'Match each method.',
      scoringMode: 'auto',
      creditMode: null,
      points: 2,
      sourceQuestionId: 'bbbbbbbb-bbbb-4ccc-8ddd-eeeeeeeeeeee',
      body: {
        slots: [{ id: 'slot-get', label: 'GET', correctItemId: 'item-read' }],
        bank: [
          { id: 'item-read', text: 'Read a resource', isDistractor: false },
          { id: 'item-extra', text: 'Unused', isDistractor: true },
        ],
      },
    },
    {
      id: 'cccccccc-bbbb-4ccc-8ddd-eeeeeeeeeeee',
      sortOrder: 1,
      type: 'dragDropPerSlot',
      stem: 'Match each status.',
      scoringMode: 'auto',
      creditMode: null,
      points: 2,
      sourceQuestionId: null,
      body: {
        slots: [
          {
            id: 'slot-401',
            label: '401',
            options: [
              { id: 'opt-unauth', text: 'Unauthorized', isCorrect: true },
              { id: 'opt-ok', text: 'OK', isCorrect: false },
            ],
          },
        ],
      },
    },
  ],
};

@Component({ template: '' })
class TemplateDetailStub {}

describe('TemplateEditor', () => {
  let fixture: ComponentFixture<TemplateEditor>;
  let saved: SaveTemplateContentRequest | null;

  beforeEach(async () => {
    saved = null;
    await TestBed.configureTestingModule({
      imports: [TemplateEditor],
      providers: [
        provideRouter([{ path: 'templates/:id', component: TemplateDetailStub }]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: template.id }) } },
        },
        {
          provide: TemplatesApi,
          useValue: {
            get: () => of(template),
            getVersion: () => of(version),
            saveContent: (_id: string, body: SaveTemplateContentRequest) => {
              saved = body;
              return of({ ...template, latestVersionNumber: 3, rowVersion: 4 });
            },
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(TemplateEditor);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('edits shared-bank and per-slot questions the way the quiz editor does', () => {
    const shared = fixture.nativeElement.querySelector('[data-testid="shared-slot-label"]') as HTMLInputElement;
    const perSlot = fixture.nativeElement.querySelector('[data-testid="per-slot-label"]') as HTMLInputElement;
    expect(shared.value).toBe('GET');
    expect(perSlot.value).toBe('401');
    expect(fixture.nativeElement.textContent).toContain('Add slot');
    expect(fixture.nativeElement.textContent).toContain('Add bank item');
    expect(fixture.nativeElement.textContent).toContain('Correct bank item');

    shared.value = 'GET method';
    shared.dispatchEvent(new Event('input'));
    perSlot.value = 'Unauthorized';
    perSlot.dispatchEvent(new Event('input'));
    const note = fixture.nativeElement.querySelector('#edit-note') as HTMLInputElement;
    note.value = 'Adjust the slots';
    note.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(saved).toBeTruthy();
    expect(saved!.changeNote).toBe('Adjust the slots');
    expect(saved!.questions).toHaveLength(2);
    const sharedBody = saved!.questions[0].body as {
      slots: Array<{ label: string; correctItemId: string }>;
      bank: Array<{ text: string }>;
    };
    expect(sharedBody.slots[0].label).toBe('GET method');
    expect(sharedBody.slots[0].correctItemId).toBe('item-read');
    expect(sharedBody.bank.map((item) => item.text)).toEqual(['Read a resource', 'Unused']);
    expect(saved!.questions[0].sourceQuestionId).toBe('bbbbbbbb-bbbb-4ccc-8ddd-eeeeeeeeeeee');
    const perBody = saved!.questions[1].body as { slots: Array<{ label: string; options: Array<{ text: string }> }> };
    expect(perBody.slots[0].label).toBe('Unauthorized');
    expect(perBody.slots[0].options.map((option) => option.text)).toEqual(['Unauthorized', 'OK']);
  });
});
