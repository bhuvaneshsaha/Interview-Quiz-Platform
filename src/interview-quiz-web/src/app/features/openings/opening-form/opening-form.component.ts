import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CreateOpeningRequest, OpeningResponse, UpdateOpeningRequest } from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';

@Component({
  selector: 'app-opening-form',
  imports: [ReactiveFormsModule, RouterLink, HasPermission, PageStatus],
  templateUrl: './opening-form.component.html',
  styleUrl: './opening-form.component.css',
})
export class OpeningForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(OpeningsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly permissions = inject(PermissionService);

  readonly codes = PermissionCodes;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly isNew = signal(true);
  readonly canWrite = this.permissions.hasPermission(PermissionCodes.OpeningsWrite);

  private openingId: string | null = null;
  private rowVersion = 0;

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    jobDescription: ['', [Validators.maxLength(20000)]],
    owner: ['', [Validators.required, Validators.maxLength(200)]],
    startDate: ['', Validators.required],
    expectedCloseDate: [''],
    headcount: [1, [Validators.required, Validators.min(1), Validators.max(10000)]],
    expectedExperienceYears: [0, [Validators.required, Validators.min(0), Validators.max(80)]],
    handlersText: [''],
    tags: this.fb.array<ReturnType<OpeningForm['createTagGroup']>>([]),
  });

  get tags(): FormArray {
    return this.form.controls.tags;
  }

  tagGroup(index: number): FormGroup {
    return this.tags.at(index) as FormGroup;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || id === 'new') {
      this.isNew.set(true);
      this.loading.set(false);
      if (!this.canWrite) {
        this.error.set('You do not have permission to create openings.');
      }
      return;
    }
    this.isNew.set(false);
    this.openingId = id;
    this.api.get(id).subscribe({
      next: (opening) => {
        this.patch(opening);
        this.loading.set(false);
        if (!this.canWrite) {
          this.form.disable();
        }
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.loading.set(false);
      },
    });
  }

  addTag(key = '', value = ''): void {
    this.tags.push(this.createTagGroup(key, value));
  }

  removeTag(index: number): void {
    this.tags.removeAt(index);
  }

  submit(): void {
    this.error.set(null);
    if (!this.canWrite) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      document.querySelector<HTMLElement>('#opening-form .ng-invalid')?.focus();
      return;
    }
    this.saving.set(true);
    const body = this.toRequest();
    const request$ =
      this.isNew() || !this.openingId
        ? this.api.create(body)
        : this.api.update(this.openingId, {
            ...body,
            id: this.openingId,
            rowVersion: this.rowVersion,
          } satisfies UpdateOpeningRequest);

    request$.subscribe({
      next: () => void this.router.navigateByUrl('/openings'),
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.saving.set(false);
      },
    });
  }

  private createTagGroup(key: string, value: string) {
    return this.fb.nonNullable.group({
      key: [key, Validators.maxLength(100)],
      value: [value, Validators.maxLength(400)],
    });
  }

  private patch(opening: OpeningResponse): void {
    this.rowVersion = opening.rowVersion;
    this.form.patchValue({
      title: opening.title,
      jobDescription: opening.jobDescription,
      owner: opening.owner,
      startDate: opening.startDate,
      expectedCloseDate: opening.expectedCloseDate ?? '',
      headcount: opening.headcount,
      expectedExperienceYears: opening.expectedExperienceYears,
      handlersText: opening.handlers.join('\n'),
    });
    this.tags.clear();
    for (const [key, value] of Object.entries(opening.tags)) {
      this.addTag(key, value);
    }
  }

  private toRequest(): CreateOpeningRequest {
    const value = this.form.getRawValue();
    const handlers = value.handlersText
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);
    const tags: Record<string, string> = {};
    for (const row of value.tags) {
      const key = row.key.trim();
      if (key) {
        tags[key] = row.value.trim();
      }
    }
    return {
      title: value.title.trim(),
      jobDescription: value.jobDescription.trim(),
      owner: value.owner.trim(),
      startDate: value.startDate,
      expectedCloseDate: value.expectedCloseDate ? value.expectedCloseDate : null,
      headcount: Number(value.headcount),
      expectedExperienceYears: Number(value.expectedExperienceYears),
      handlers,
      tags,
    };
  }
}
