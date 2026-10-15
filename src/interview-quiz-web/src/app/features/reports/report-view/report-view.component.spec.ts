import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { FunnelReportResponse, ReportFilterCriteria } from '../../../core/api/contracts';
import { ReportsApi } from '../../../core/api/reports-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { savedFilterTestProviders } from '../../../shared/saved-filters/saved-filters.testing';
import { ReportView } from './report-view.component';

const funnel: FunnelReportResponse = {
  defaultWindowApplied: true,
  page: 1,
  pageSize: 20,
  totalCount: 1,
  totals: {
    openingTitle: '',
    quizTitle: '',
    assigned: 3,
    appeared: 2,
    submitted: 2,
    completed: 1,
    pendingReview: 0,
    appearedRate: 2 / 3,
    submittedRate: 2 / 3,
    completedRate: 1 / 3,
  },
  items: [
    {
      openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
      openingTitle: 'Backend opening',
      quizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
      quizTitle: 'Backend quiz',
      assigned: 3,
      appeared: 2,
      submitted: 2,
      completed: 1,
      pendingReview: 0,
      appearedRate: 2 / 3,
      submittedRate: 2 / 3,
      completedRate: 1 / 3,
    },
  ],
};

describe('ReportView', () => {
  let fixture: ComponentFixture<ReportView>;
  let calls: ReportFilterCriteria[];

  beforeEach(async () => {
    calls = [];
    await TestBed.configureTestingModule({
      imports: [ReportView],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ reportKey: 'funnel' })) },
        },
        {
          provide: ReportsApi,
          useValue: {
            load: (_route: string, criteria: ReportFilterCriteria) => {
              calls.push(criteria);
              return of(funnel);
            },
            exportCsv: () => of(new Blob(['ok'])),
          },
        },
        ...savedFilterTestProviders(),
      ],
    }).compileComponents();
  });

  async function render(): Promise<void> {
    const permissions = TestBed.inject(PermissionService);
    permissions.set([PermissionCodes.ReportsRead, PermissionCodes.AssignmentsRead]);
    fixture = TestBed.createComponent(ReportView);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the funnel row and chart', async () => {
    await render();
    const table = fixture.nativeElement.querySelector('[data-testid="report-table"]') as HTMLElement;
    expect(table.textContent).toContain('Backend opening');
    expect(table.textContent).toContain('Backend quiz');
    const chart = fixture.nativeElement.querySelector('[data-testid="report-chart"]') as SVGElement;
    expect(chart.getAttribute('aria-label')).toContain('Assigned 3');
    expect(fixture.nativeElement.textContent).toContain('Showing assignments created in the last 90 days.');
    expect(fixture.nativeElement.querySelector('a[href="/assignments?openingId=3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001"]')).not.toBeNull();
  });

  it('sends the added chip as report criteria', async () => {
    await render();
    const input = fixture.nativeElement.querySelector('input[type="text"]') as HTMLInputElement;
    input.value = '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="add-filter"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(calls.at(-1)).toEqual({
      filters: [{ field: 'openingId', operator: 'is', value: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001' }],
    });
    expect(fixture.nativeElement.textContent).toContain('openingId is 4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001');
  });
});
