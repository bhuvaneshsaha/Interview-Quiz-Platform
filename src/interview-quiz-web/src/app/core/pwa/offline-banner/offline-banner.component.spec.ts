import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OnlineStatus } from '../online-status.service';
import { OfflineBanner } from './offline-banner.component';

describe('OfflineBanner', () => {
  let fixture: ComponentFixture<OfflineBanner>;
  const online = signal(true);

  beforeEach(async () => {
    online.set(true);
    await TestBed.configureTestingModule({
      imports: [OfflineBanner],
      providers: [{ provide: OnlineStatus, useValue: { online } }],
    }).compileComponents();
  });

  it('keeps authoring copy in the shell', () => {
    online.set(false);
    fixture = TestBed.createComponent(OfflineBanner);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(
      'Openings and authoring need a network connection',
    );
    expect(fixture.nativeElement.textContent).not.toContain('submitting this quiz');
  });

  it('uses attempt copy on the candidate quiz', () => {
    online.set(false);
    fixture = TestBed.createComponent(OfflineBanner);
    fixture.componentRef.setInput('context', 'attempt');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(
      'Saving answers and submitting this quiz need a network connection',
    );
  });

  it('hides the banner while online', () => {
    fixture = TestBed.createComponent(OfflineBanner);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });
});
