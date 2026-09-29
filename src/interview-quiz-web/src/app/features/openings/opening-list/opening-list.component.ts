import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { OpeningResponse } from '../../../core/api/contracts';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PageStatus } from '../../../shared/page-status/page-status.component';

@Component({
  selector: 'app-opening-list',
  imports: [RouterLink, ReactiveFormsModule, HasPermission, PageStatus],
  templateUrl: './opening-list.component.html',
  styleUrl: './opening-list.component.css',
})
export class OpeningList implements OnInit {
  private readonly api = inject(OpeningsApi);
  readonly codes = PermissionCodes;
  readonly ownerFilter = new FormControl('', { nonNullable: true });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly openings = signal<OpeningResponse[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set(null);
    const owner = this.ownerFilter.value.trim() || undefined;
    this.api.list(page, this.pageSize, owner).subscribe({
      next: (result) => {
        this.openings.set(result.items);
        this.page.set(result.page);
        this.totalCount.set(result.totalCount);
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

  get lastPage(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }
}
