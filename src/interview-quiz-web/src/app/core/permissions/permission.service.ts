import { computed, Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PermissionService {
  private readonly codes = signal<ReadonlySet<string>>(new Set());

  readonly permissions = computed(() => [...this.codes()]);

  set(codes: readonly string[]): void {
    this.codes.set(new Set(codes));
  }

  clear(): void {
    this.codes.set(new Set());
  }

  hasPermission(code: string): boolean {
    return this.codes().has(code);
  }

  hasAny(codes: readonly string[]): boolean {
    const current = this.codes();
    return codes.some((code) => current.has(code));
  }
}
