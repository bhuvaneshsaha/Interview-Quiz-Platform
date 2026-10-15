import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { AssignableUser, OpeningPerson } from '../../core/api/contracts';
import { UsersApi } from '../../core/api/users-api.service';

@Component({
  selector: 'app-user-picker',
  templateUrl: './user-picker.component.html',
  styleUrl: './user-picker.component.css',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: UserPicker,
      multi: true,
    },
  ],
})
export class UserPicker implements ControlValueAccessor {
  private readonly usersApi = inject(UsersApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly queries = new Subject<string>();

  readonly multiple = input(false);
  readonly inputId = input('user-picker');
  readonly labelledBy = input<string | null>(null);
  readonly known = input<OpeningPerson[]>([]);

  readonly disabled = signal(false);
  readonly query = signal('');
  readonly open = signal(false);
  readonly results = signal<AssignableUser[]>([]);
  readonly selected = signal<AssignableUser[]>([]);
  readonly searching = signal(false);

  private onChange: (value: string | string[]) => void = () => {};
  private onTouched: () => void = () => {};

  constructor() {
    effect(() => {
      this.remember(this.known());
    });
    this.queries
      .pipe(
        debounceTime(200),
        distinctUntilChanged(),
        switchMap((term) => {
          this.searching.set(true);
          return this.usersApi.searchAssignable(term).pipe(catchError(() => of([] as AssignableUser[])));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((users) => {
        this.results.set(users);
        this.searching.set(false);
        this.open.set(true);
      });
  }

  writeValue(value: string | string[] | null): void {
    const ids = (Array.isArray(value) ? value : value ? [value] : [])
      .map((id) => id.trim())
      .filter((id) => id.length > 0);
    const kept = this.selected().filter((user) => ids.includes(user.id));
    const missing = ids.filter((id) => !kept.some((user) => user.id === id));
    this.selected.set(kept);
    for (const id of missing) {
      this.usersApi.searchAssignable(id, 5).subscribe({
        next: (users) => {
          const match = users.find((user) => user.id === id);
          this.selected.update((current) => {
            if (current.some((user) => user.id === id)) {
              return current;
            }
            return [
              ...current,
              match ?? { id, name: id, email: '' },
            ];
          });
        },
        error: () => {
          this.selected.update((current) =>
            current.some((user) => user.id === id) ? current : [...current, { id, name: id, email: '' }],
          );
        },
      });
    }
  }

  registerOnChange(fn: (value: string | string[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  private remember(people: OpeningPerson[]): void {
    this.selected.update((current) => {
      const next = [...current];
      for (const person of people) {
        if (!person.userId || next.some((user) => user.id === person.userId)) {
          continue;
        }
        if (!person.resolved) {
          continue;
        }
        next.push({
          id: person.userId,
          name: person.name,
          email: person.email,
        });
      }
      return next;
    });
  }

  onQuery(value: string): void {
    this.query.set(value);
    this.queries.next(value.trim());
  }

  choose(user: AssignableUser): void {
    if (this.disabled()) {
      return;
    }
    if (this.multiple()) {
      if (!this.selected().some((item) => item.id === user.id)) {
        this.selected.update((current) => [...current, user]);
      }
    } else {
      this.selected.set([user]);
    }
    this.query.set('');
    this.open.set(false);
    this.emit();
  }

  remove(id: string): void {
    if (this.disabled()) {
      return;
    }
    this.selected.update((current) => current.filter((user) => user.id !== id));
    this.emit();
  }

  blur(): void {
    this.onTouched();
    setTimeout(() => this.open.set(false), 150);
  }

  private emit(): void {
    const ids = this.selected().map((user) => user.id);
    this.onChange(this.multiple() ? ids : (ids[0] ?? ''));
  }
}
