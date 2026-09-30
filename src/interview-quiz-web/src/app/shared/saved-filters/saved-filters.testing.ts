import { signal } from '@angular/core';
import { of } from 'rxjs';
import { AccessApi } from '../../core/api/access-api.service';
import { FilterResponse } from '../../core/api/contracts';
import { FiltersApi } from '../../core/api/filters-api.service';
import { AuthService } from '../../core/auth/auth.service';

const emptyFilters = {
  items: [] as FilterResponse[],
  page: 1,
  pageSize: 50,
  totalCount: 0,
};

export function savedFilterTestProviders(userId = 'user-1') {
  return [
    {
      provide: FiltersApi,
      useValue: {
        list: () => of(emptyFilters),
        create: () => of({
          id: '7e1f5b54-0f6a-4a3e-1b55-4d2f6e0e5001',
          name: 'Saved',
          target: 'quizzes',
          criteria: {},
          ownerUserId: userId,
          shareMode: 'private',
          sharedWithUserIds: [],
          createdAtUtc: '2026-01-01T00:00:00Z',
          updatedAtUtc: '2026-01-01T00:00:00Z',
        } satisfies FilterResponse),
        delete: () => of(undefined),
        share: () => of({} as FilterResponse),
        unshare: () => of({} as FilterResponse),
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
        currentUser: signal({ id: userId, email: 'dev@example.com', permissions: [] }),
      },
    },
  ];
}
