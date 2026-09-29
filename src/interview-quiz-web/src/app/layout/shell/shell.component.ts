import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { HasPermission } from '../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../core/permissions/permission-codes';
import { OfflineBanner } from '../../core/pwa/offline-banner/offline-banner.component';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, HasPermission, OfflineBanner],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
})
export class Shell {
  private readonly auth = inject(AuthService);
  readonly user = this.auth.currentUser;
  readonly codes = PermissionCodes;

  logout(): void {
    this.auth.logout();
  }
}
