import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PagedResult, UserResponse } from '../../../core/api/contracts';
import { AccessApi } from '../../../core/api/access-api.service';
import { UserList } from './user-list.component';

const staff: UserResponse = {
  id: 'staff-1',
  email: 'author.dev@example.com',
  isDisabled: false,
  roleIds: ['role-1'],
  isCandidate: false,
  roleNames: ['Template author'],
};

const candidate: UserResponse = {
  id: 'candidate-1',
  email: 'candidate.audit@example.com',
  isDisabled: false,
  roleIds: [],
  isCandidate: true,
  roleNames: [],
};

describe('UserList', () => {
  let fixture: ComponentFixture<UserList>;
  let includeCandidates: boolean | undefined;

  beforeEach(async () => {
    includeCandidates = undefined;
    const api = {
      listUsers: (page: number, pageSize: number, include = false): ReturnType<AccessApi['listUsers']> => {
        includeCandidates = include;
        const items = include ? [staff, candidate] : [staff];
        const result: PagedResult<UserResponse> = {
          items,
          page,
          pageSize,
          totalCount: items.length,
        };
        return of(result);
      },
    };

    await TestBed.configureTestingModule({
      imports: [UserList],
      providers: [provideRouter([]), { provide: AccessApi, useValue: api }],
    }).compileComponents();

    fixture = TestBed.createComponent(UserList);
    fixture.detectChanges();
  });

  it('hides candidates until asked and names employee roles', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('author.dev@example.com');
    expect(text).toContain('Template author');
    expect(text).not.toContain('candidate.audit@example.com');
    expect(includeCandidates).toBe(false);

    const box = fixture.nativeElement.querySelector('#show-candidates') as HTMLInputElement;
    box.checked = true;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const shown = fixture.nativeElement.textContent as string;
    expect(shown).toContain('candidate.audit@example.com');
    expect(shown).toContain('Candidate');
    expect(includeCandidates).toBe(true);
  });
});
