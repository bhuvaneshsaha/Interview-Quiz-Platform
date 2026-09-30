import { signal } from '@angular/core';
import { applicationConfig, Meta, StoryObj } from '@storybook/angular-vite';
import { of } from 'rxjs';
import { AccessApi } from '../../core/api/access-api.service';
import { FilterResponse } from '../../core/api/contracts';
import { FiltersApi } from '../../core/api/filters-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/permissions/permission-codes';
import { PermissionService } from '../../core/permissions/permission.service';
import { SavedFilters } from './saved-filters.component';

const sampleFilter: FilterResponse = {
  id: '7e1f5b54-0f6a-4a3e-1b55-4d2f6e0e5001',
  name: 'Backend roles',
  target: 'quizzes',
  criteria: { keyword: 'backend' },
  ownerUserId: 'user-1',
  shareMode: 'private',
  sharedWithUserIds: [],
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
};

function withSavedFilters(codes: readonly string[]) {
  return applicationConfig({
    providers: [
      {
        provide: PermissionService,
        useFactory: () => {
          const permissions = new PermissionService();
          permissions.set(codes);
          return permissions;
        },
      },
      {
        provide: FiltersApi,
        useValue: {
          list: () => of({ items: [sampleFilter], page: 1, pageSize: 50, totalCount: 1 }),
          create: () => of(sampleFilter),
          delete: () => of(undefined),
          share: () => of({ ...sampleFilter, shareMode: 'publicInsideCompany' }),
          unshare: () => of(sampleFilter),
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
          currentUser: signal({ id: 'user-1', email: 'author.dev@example.com', permissions: codes }),
        },
      },
    ],
  });
}

const meta: Meta<SavedFilters> = {
  title: 'Shared/SavedFilters',
  component: SavedFilters,
  tags: ['autodocs'],
  args: {
    target: 'quizzes',
    criteria: { keyword: 'backend', experienceMinYears: 3 },
  },
};

export default meta;
type Story = StoryObj<SavedFilters>;

export const ApplyOnly: Story = {
  decorators: [withSavedFilters([PermissionCodes.QuizzesRead, PermissionCodes.TemplatesRead])],
};

export const WriteAndShare: Story = {
  decorators: [
    withSavedFilters([
      PermissionCodes.FiltersWrite,
      PermissionCodes.FiltersShare,
      PermissionCodes.QuizzesRead,
    ]),
  ],
};
