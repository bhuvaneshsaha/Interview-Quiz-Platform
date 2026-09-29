import {
  Directive,
  EmbeddedViewRef,
  TemplateRef,
  ViewContainerRef,
  effect,
  inject,
  input,
} from '@angular/core';
import { PermissionService } from './permission.service';

@Directive({
  selector: '[hasPermission]',
})
export class HasPermission {
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly permissions = inject(PermissionService);
  private viewRef: EmbeddedViewRef<unknown> | null = null;

  readonly hasPermission = input.required<string | readonly string[]>();

  constructor() {
    effect(() => {
      const required = this.hasPermission();
      const allowed =
        typeof required === 'string'
          ? this.permissions.hasPermission(required)
          : this.permissions.hasAny(required);
      if (allowed && !this.viewRef) {
        this.viewRef = this.viewContainer.createEmbeddedView(this.templateRef);
      } else if (!allowed && this.viewRef) {
        this.viewContainer.clear();
        this.viewRef = null;
      }
    });
  }
}
