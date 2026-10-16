import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
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
  private readonly route = inject(ActivatedRoute);
  readonly user = this.auth.currentUser;
  readonly codes = PermissionCodes;
  readonly accessDenied = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get('denied') === '1')),
    { initialValue: false },
  );

  readonly hasSliceOneScreen = computed(() =>
    this.permissions.hasAny([
      PermissionCodes.OpeningsRead,
      PermissionCodes.OpeningsFieldsManage,
      PermissionCodes.RolesManage,
      PermissionCodes.UsersManage,
      PermissionCodes.QuizzesRead,
      PermissionCodes.QuizzesWrite,
      PermissionCodes.AiDraftUse,
      PermissionCodes.TemplatesRead,
      PermissionCodes.QuestionsRead,
      PermissionCodes.AssignmentsRead,
      PermissionCodes.ReportsRead,
    ]),
  );
}
