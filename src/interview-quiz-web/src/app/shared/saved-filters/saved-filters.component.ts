import { isDevMode } from '@angular/core';
import { Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { AccessApi } from '../../core/api/access-api.service';
import {
  AssignableUser,
  FilterResponse,
  FilterShareMode,
  FilterTarget,
  ListCriteria,
} from '../../core/api/contracts';
import { FiltersApi } from '../../core/api/filters-api.service';
import { UsersApi } from '../../core/api/users-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { HasPermission } from '../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../core/permissions/permission-codes';
import { PermissionService } from '../../core/permissions/permission.service';
import { PageStatus } from '../page-status/page-status.component';
import { compactCriteria, criteriaFromUnknown } from './saved-filter.criteria';

const FILTER_NAME_MAX = 200;
const SHARE_MODE_LABELS: Record<FilterShareMode, string> = {
  private: 'Private',
  publicInsideCompany: 'Everyone at the company',
  specificUsers: 'Specific people',
};

@Component({
  selector: 'app-saved-filters',
  imports: [ReactiveFormsModule, HasPermission],
  templateUrl: './saved-filters.component.html',
  styleUrl: './saved-filters.component.css',
})
export class SavedFilters implements OnInit {
  private readonly filtersApi = inject(FiltersApi);
  private readonly accessApi = inject(AccessApi);
  private readonly usersApi = inject(UsersApi);
  private readonly auth = inject(AuthService);
  private readonly permissions = inject(PermissionService);

  readonly target = input.required<FilterTarget>();
  readonly criteria = input<ListCriteria>({});
  readonly apply = output<ListCriteria>();

  readonly codes = PermissionCodes;
  readonly nameMax = FILTER_NAME_MAX;
  readonly shareLabels = SHARE_MODE_LABELS;
  readonly canManageUsers = this.permissions.hasPermission(PermissionCodes.UsersManage);
  readonly canPickPeople =
    this.permissions.hasPermission(PermissionCodes.OpeningsRead) || this.canManageUsers;

  readonly selectedId = new FormControl('', { nonNullable: true });
  readonly newName = new FormControl('', { nonNullable: true });
  readonly shareMode = new FormControl<'publicInsideCompany' | 'specificUsers'>(
    'publicInsideCompany',
    { nonNullable: true },
  );
  readonly shareUserIds = new FormControl('', { nonNullable: true });

  readonly filters = signal<FilterResponse[]>([]);
  readonly people = signal<AssignableUser[]>([]);
  readonly selectedPeople = signal<AssignableUser[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly status = signal<string | null>(null);
  readonly confirmingDelete = signal(false);
  readonly saving = signal(false);
  readonly selectedIdValue = signal('');
  readonly shareModeValue = signal<'publicInsideCompany' | 'specificUsers'>('publicInsideCompany');
  readonly showDevCorrelation = isDevMode();

  readonly selected = computed(
    () => this.filters().find((filter) => filter.id === this.selectedIdValue()) ?? null,
  );
  readonly selectedOwned = computed(() => {
    const filter = this.selected();
    const userId = this.auth.currentUser()?.id;
    return Boolean(filter && userId && filter.ownerUserId === userId);
  });

  ngOnInit(): void {
    this.reload();
    if (this.permissions.hasPermission(PermissionCodes.OpeningsRead)) {
      this.usersApi.searchAssignable('', 100).subscribe({
        next: (people) => {
          this.people.set(people);
          this.syncSelectedPeople();
        },
        error: () => this.people.set([]),
      });
    } else if (this.canManageUsers) {
      this.accessApi.listUsers(1, 100).subscribe({
        next: (result) => {
          this.people.set(result.items.map((user) => ({ id: user.id, name: user.email, email: user.email })));
          this.syncSelectedPeople();
        },
        error: () => this.people.set([]),
      });
    }
  }

  applySelected(): void {
    this.clearMessages();
    const filter = this.selected();
    if (!filter) {
      this.error.set('Select a saved filter to apply.');
      return;
    }
    this.apply.emit(criteriaFromUnknown(this.target(), filter.criteria));
    this.status.set(`Applied “${filter.name}”.`);
  }

  saveCurrent(): void {
    this.clearMessages();
    if (!this.permissions.hasPermission(PermissionCodes.FiltersWrite)) {
      return;
    }
    const name = this.newName.value.trim();
    if (!name) {
      this.error.set('Name is required to save these filters.');
      return;
    }
    if (name.length > FILTER_NAME_MAX) {
      this.error.set(`Name cannot exceed ${FILTER_NAME_MAX} characters.`);
      return;
    }
    this.saving.set(true);
    const body = {
      name,
      target: this.target(),
      criteria: compactCriteria(this.criteria()) as ListCriteria,
    };
    this.filtersApi.create(body).subscribe({
      next: (created) => {
        this.newName.setValue('');
        this.status.set(`Saved “${created.name}”.`);
        this.saving.set(false);
        this.reload(created.id);
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  shareSelected(): void {
    this.clearMessages();
    const filter = this.selected();
    if (!filter || !this.selectedOwned()) {
      this.error.set('Only a filter you own can be shared.');
      return;
    }
    const mode = this.shareMode.value;
    if (mode === 'specificUsers' && !this.canPickPeople) {
      this.syncSelectedPeople();
    }
    const userIds = mode === 'specificUsers' ? this.selectedPeople().map((person) => person.id) : undefined;
    if (mode === 'specificUsers' && (!userIds || userIds.length === 0)) {
      this.error.set('Choose at least one person.');
      return;
    }
    this.saving.set(true);
    this.filtersApi.share(filter.id, { shareMode: mode, userIds }).subscribe({
      next: (updated) => {
        this.status.set(
          updated.shareMode === 'publicInsideCompany'
            ? `“${updated.name}” is public inside the company.`
            : `“${updated.name}” is shared with specific people.`,
        );
        this.saving.set(false);
        this.reload(updated.id);
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  unshareSelected(): void {
    this.clearMessages();
    const filter = this.selected();
    if (!filter || !this.selectedOwned()) {
      this.error.set('Only a filter you own can be unshared.');
      return;
    }
    this.saving.set(true);
    this.filtersApi.unshare(filter.id).subscribe({
      next: (updated) => {
        this.status.set(`“${updated.name}” is private.`);
        this.saving.set(false);
        this.reload(updated.id);
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  requestDelete(): void {
    this.clearMessages();
    if (!this.selectedOwned()) {
      this.error.set('Only a filter you own can be deleted.');
      return;
    }
    this.confirmingDelete.set(true);
  }

  confirmDelete(): void {
    const filter = this.selected();
    if (!filter || !this.selectedOwned()) {
      this.confirmingDelete.set(false);
      return;
    }
    this.saving.set(true);
    this.filtersApi.delete(filter.id).subscribe({
      next: () => {
        this.status.set(`Deleted “${filter.name}”.`);
        this.selectedId.setValue('');
        this.selectedIdValue.set('');
        this.confirmingDelete.set(false);
        this.saving.set(false);
        this.reload();
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  cancelDelete(): void {
    this.confirmingDelete.set(false);
  }

  onSelectionChange(): void {
    this.selectedIdValue.set(this.selectedId.value);
    this.confirmingDelete.set(false);
    const filter = this.selected();
    if (!filter) {
      return;
    }
    if (filter.shareMode === 'specificUsers') {
      this.shareMode.setValue('specificUsers');
      this.shareModeValue.set('specificUsers');
      this.shareUserIds.setValue(filter.sharedWithUserIds.join(', '));
      this.syncSelectedPeople();
    } else {
      this.shareMode.setValue('publicInsideCompany');
      this.shareModeValue.set('publicInsideCompany');
      this.shareUserIds.setValue('');
      this.selectedPeople.set([]);
    }
  }

  addPerson(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLSelectElement) || !target.value) {
      return;
    }
    const person = this.people().find((item) => item.id === target.value);
    if (person && !this.selectedPeople().some((item) => item.id === person.id)) {
      this.selectedPeople.update((items) => [...items, person]);
    }
    target.value = '';
  }

  removePerson(id: string): void {
    this.selectedPeople.update((items) => items.filter((item) => item.id !== id));
  }

  onShareModeChange(): void {
    this.shareModeValue.set(this.shareMode.value);
  }

  shareLabel(mode: FilterShareMode): string {
    return SHARE_MODE_LABELS[mode] ?? mode;
  }

  private syncSelectedPeople(): void {
    const ids = this.parseUserIds(this.shareUserIds.value);
    const known = this.people();
    this.selectedPeople.set(
      ids.map((id) => known.find((person) => person.id === id) ?? { id, name: id, email: id }),
    );
  }

  private reload(selectId?: string): void {
    this.loading.set(true);
    this.filtersApi.list(this.target(), 1, 50).subscribe({
      next: (result) => {
        this.filters.set(result.items);
        if (selectId) {
          this.selectedId.setValue(selectId);
        }
        this.onSelectionChange();
        this.loading.set(false);
      },
      error: (err: unknown) => this.fail(err, true),
    });
  }

  private parseUserIds(raw: string): string[] {
    return raw
      .split(/[\s,;]+/)
      .map((id) => id.trim())
      .filter((id) => id.length > 0);
  }

  private fail(err: unknown, fromList = false): void {
    const mapped = PageStatus.fromError(err);
    this.error.set(mapped.message);
    this.correlationId.set(mapped.correlationId);
    this.saving.set(false);
    if (fromList) {
      this.loading.set(false);
    }
  }

  private clearMessages(): void {
    this.error.set(null);
    this.correlationId.set(null);
    this.status.set(null);
  }
}
