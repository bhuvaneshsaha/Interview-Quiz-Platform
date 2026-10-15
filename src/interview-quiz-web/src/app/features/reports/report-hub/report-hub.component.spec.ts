import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ReportHub } from './report-hub.component';

describe('ReportHub', () => {
  let fixture: ComponentFixture<ReportHub>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReportHub],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(ReportHub);
    fixture.detectChanges();
  });

  it('renders a card for each Reporting v1 report', () => {
    const links = [...fixture.nativeElement.querySelectorAll('[data-testid="report-card"]')] as HTMLAnchorElement[];
    expect(links.map((link) => link.querySelector('.card-title')?.textContent?.trim())).toEqual([
      'Funnel',
      'Scores',
      'Time taken',
      'Questions',
      'Candidates',
      'Pending review',
    ]);
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/reports/funnel',
      '/reports/scores',
      '/reports/time',
      '/reports/questions',
      '/reports/candidates',
      '/reports/pending-review',
    ]);
  });
});
