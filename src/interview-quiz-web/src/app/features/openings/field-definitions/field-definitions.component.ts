import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { OpeningFieldDefinitionDto } from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';

@Component({
  selector: 'app-field-definitions',
  imports: [ReactiveFormsModule, PageStatus],
  templateUrl: './field-definitions.component.html',
  styleUrl: './field-definitions.component.css',
})
export class FieldDefinitions implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(OpeningsApi);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly saved = signal(false);

  readonly form = this.fb.nonNullable.group({
    items: this.fb.array<ReturnType<FieldDefinitions['createItem']>>([]),
  });

  get items(): FormArray {
    return this.form.controls.items;
  }

  ngOnInit(): void {
    this.api.listFieldDefinitions().subscribe({
      next: (defs) => {
        this.items.clear();
        for (const def of defs) {
          this.items.push(this.createItem(def.key, def.displayName, def.sortOrder));
        }
        this.loading.set(false);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.loading.set(false);
      },
    });
  }

  add(): void {
    this.items.push(this.createItem('', '', this.items.length));
  }

  remove(index: number): void {
    this.items.removeAt(index);
  }

  submit(): void {
    this.error.set(null);
    this.saved.set(false);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const items: OpeningFieldDefinitionDto[] = this.form.getRawValue().items.map((row, index) => ({
      key: row.key.trim(),
      displayName: row.displayName.trim(),
      sortOrder: Number.isFinite(row.sortOrder) ? row.sortOrder : index,
    }));
    this.api.replaceFieldDefinitions({ items }).subscribe({
      next: (saved) => {
        this.items.clear();
        for (const def of saved) {
          this.items.push(this.createItem(def.key, def.displayName, def.sortOrder));
        }
        this.saving.set(false);
        this.saved.set(true);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.saving.set(false);
      },
    });
  }

  private createItem(key: string, displayName: string, sortOrder: number) {
    return this.fb.nonNullable.group({
      key: [key, [Validators.required, Validators.maxLength(100)]],
      displayName: [displayName, [Validators.required, Validators.maxLength(200)]],
      sortOrder: [sortOrder, [Validators.min(0)]],
    });
  }
}
