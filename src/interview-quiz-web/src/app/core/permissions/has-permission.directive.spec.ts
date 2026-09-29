import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HasPermission } from './has-permission.directive';
import { PermissionCodes } from './permission-codes';
import { PermissionService } from './permission.service';

@Component({
  template: `<p *hasPermission="code" data-testid="gated">Visible</p>`,
  imports: [HasPermission],
})
class Host {
  code: string | readonly string[] = PermissionCodes.OpeningsWrite;
}

describe('HasPermission', () => {
  let fixture: ComponentFixture<Host>;
  let permissions: PermissionService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Host],
    }).compileComponents();
    permissions = TestBed.inject(PermissionService);
    fixture = TestBed.createComponent(Host);
  });

  it('hides content without the permission', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[data-testid="gated"]')).toBeNull();
  });

  it('shows content when the permission is granted', async () => {
    permissions.set([PermissionCodes.OpeningsWrite]);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[data-testid="gated"]')?.textContent).toContain(
      'Visible',
    );
  });
});
