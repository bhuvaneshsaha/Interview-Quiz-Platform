import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { HasPermission } from '../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../core/permissions/permission-codes';
import { PermissionService } from '../../core/permissions/permission.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink, HasPermission],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class Home {
  private readonly auth = inject(AuthService);
  private readonly permissions = inject(PermissionService);
  readonly user = this.auth.currentUser;
  readonly codes = PermissionCodes;

  readonly hasSliceOneScreen = computed(() =>
    this.permissions.hasAny([
      PermissionCodes.OpeningsRead,
      PermissionCodes.OpeningsFieldsManage,
      PermissionCodes.RolesManage,
      PermissionCodes.UsersManage,
      PermissionCodes.QuizzesRead,
      PermissionCodes.QuizzesWrite,
      PermissionCodes.TemplatesRead,
    ]),
  );
}
