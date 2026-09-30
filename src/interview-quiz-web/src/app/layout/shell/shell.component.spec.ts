import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MeResponse } from '../../core/api/contracts';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/permissions/permission-codes';
import { PermissionService } from '../../core/permissions/permission.service';
import { Shell } from './shell.component';

describe('Shell', () => {
  let fixture: ComponentFixture<Shell>;
  let permissions: PermissionService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Shell],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            currentUser: signal<MeResponse | null>({
              id: 'user-1',
              email: 'author.dev@example.com',
              permissions: [],
            }),
            logout: () => undefined,
          },
        },
      ],
    }).compileComponents();
    permissions = TestBed.inject(PermissionService);
  });

  it('hides Assignments nav for author-like permissions without assignments.read', async () => {
    permissions.set([
      PermissionCodes.QuizzesRead,
      PermissionCodes.QuizzesWrite,
      PermissionCodes.TemplatesRead,
      PermissionCodes.TemplatesWrite,
      PermissionCodes.QuestionsRead,
      PermissionCodes.QuestionsWrite,
    ]);
    fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('a[href="/assignments"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/quizzes"]')).not.toBeNull();
  });

  it('shows Assignments nav when assignments.read is granted', async () => {
    permissions.set([PermissionCodes.AssignmentsRead]);
    fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('a[href="/assignments"]')?.textContent).toContain(
      'Assignments',
    );
  });
});
