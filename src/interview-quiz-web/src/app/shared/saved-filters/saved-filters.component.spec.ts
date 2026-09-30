import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AccessApi } from '../../core/api/access-api.service';
import { FilterResponse } from '../../core/api/contracts';
import { FiltersApi } from '../../core/api/filters-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/permissions/permission-codes';
import { PermissionService } from '../../core/permissions/permission.service';
import { SavedFilters } from './saved-filters.component';

const ownedFilter: FilterResponse = {
  id: '7e1f5b54-0f6a-4a3e-1b55-4d2f6e0e5001',
  name: 'Backend roles',
  target: 'quizzes',
  criteria: { keyword: 'backend', experienceMinYears: 3 },
  ownerUserId: 'user-1',
  shareMode: 'private',
  sharedWithUserIds: [],
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
};

describe('SavedFilters', () => {
  let fixture: ComponentFixture<SavedFilters>;
  let permissions: PermissionService;

  async function setup(codes: string[]): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [SavedFilters],
      providers: [
        {
          provide: FiltersApi,
          useValue: {
            list: () => of({ items: [ownedFilter], page: 1, pageSize: 50, totalCount: 1 }),
            create: () => of(ownedFilter),
            delete: () => of(undefined),
            share: () => of({ ...ownedFilter, shareMode: 'publicInsideCompany' as const }),
            unshare: () => of({ ...ownedFilter, shareMode: 'private' as const }),
          },
        },
        {
          provide: AccessApi,
          useValue: {
            listUsers: () => of({ items: [], page: 1, pageSize: 100, totalCount: 0 }),
          },
        },
        {
          provide: AuthService,
          useValue: {
            currentUser: signal({ id: 'user-1', email: 'author.dev@example.com', permissions: [] }),
          },
        },
      ],
    }).compileComponents();
    permissions = TestBed.inject(PermissionService);
    permissions.set(codes);
    fixture = TestBed.createComponent(SavedFilters);
    fixture.componentRef.setInput('target', 'quizzes');
    fixture.componentRef.setInput('criteria', { keyword: 'kestrel' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('hides save without filters.write and still offers apply', async () => {
    await setup([PermissionCodes.QuizzesRead]);
    expect(fixture.nativeElement.querySelector('[data-testid="save-saved-filter"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="apply-saved-filter"]')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Backend roles');
  });

  it('shows save when filters.write is granted', async () => {
    await setup([PermissionCodes.FiltersWrite]);
    expect(fixture.nativeElement.querySelector('[data-testid="save-saved-filter"]')).toBeTruthy();
  });
});
