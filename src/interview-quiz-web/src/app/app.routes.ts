import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';
import { PermissionCodes } from './core/permissions/permission-codes';
import { permissionGuard } from './core/permissions/permission.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login/login.component').then((m) => m.Login),
    canActivate: [guestGuard],
    title: 'Sign in',
  },
  {
    path: 'attempt',
    loadComponent: () =>
      import('./features/attempt/attempt/attempt.component').then((m) => m.Attempt),
    title: 'Quiz attempt',
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell/shell.component').then((m) => m.Shell),
    canActivate: [authGuard],
    children: [
      {
        path: '',
        loadComponent: () => import('./layout/home/home.component').then((m) => m.Home),
        title: 'Home',
      },
      {
        path: 'openings',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.OpeningsRead },
        loadComponent: () =>
          import('./features/openings/opening-list/opening-list.component').then(
            (m) => m.OpeningList,
          ),
        title: 'Openings',
      },
      {
        path: 'openings/new',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.OpeningsWrite },
        loadComponent: () =>
          import('./features/openings/opening-form/opening-form.component').then(
            (m) => m.OpeningForm,
          ),
        title: 'Create opening',
      },
      {
        path: 'openings/:id',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.OpeningsRead },
        loadComponent: () =>
          import('./features/openings/opening-form/opening-form.component').then(
            (m) => m.OpeningForm,
          ),
        title: 'Opening',
      },
      {
        path: 'quizzes',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.QuizzesRead },
        loadComponent: () =>
          import('./features/quizzes/quiz-list/quiz-list.component').then((m) => m.QuizList),
        title: 'Quizzes',
      },
      {
        path: 'quizzes/new',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.QuizzesWrite },
        loadComponent: () =>
          import('./features/quizzes/quiz-form/quiz-form.component').then((m) => m.QuizForm),
        title: 'Create quiz',
      },
      {
        path: 'quizzes/:id',
        canActivate: [permissionGuard],
        data: { permission: [PermissionCodes.QuizzesRead, PermissionCodes.QuizzesWrite] },
        loadComponent: () =>
          import('./features/quizzes/quiz-form/quiz-form.component').then((m) => m.QuizForm),
        title: 'Quiz',
      },
      {
        path: 'templates',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.TemplatesRead },
        loadComponent: () =>
          import('./features/templates/template-list/template-list.component').then(
            (m) => m.TemplateList,
          ),
        title: 'Templates',
      },
      {
        path: 'templates/:id',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.TemplatesRead },
        loadComponent: () =>
          import('./features/templates/template-detail/template-detail.component').then(
            (m) => m.TemplateDetail,
          ),
        title: 'Template',
      },
      {
        path: 'questions',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.QuestionsRead },
        loadComponent: () =>
          import('./features/questions/question-list/question-list.component').then(
            (m) => m.QuestionList,
          ),
        title: 'Question bank',
      },
      {
        path: 'questions/new',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.QuestionsWrite },
        loadComponent: () =>
          import('./features/questions/question-form/question-form.component').then(
            (m) => m.QuestionForm,
          ),
        title: 'Create bank question',
      },
      {
        path: 'questions/:id',
        canActivate: [permissionGuard],
        data: { permission: [PermissionCodes.QuestionsRead, PermissionCodes.QuestionsWrite] },
        loadComponent: () =>
          import('./features/questions/question-form/question-form.component').then(
            (m) => m.QuestionForm,
          ),
        title: 'Bank question',
      },
      {
        path: 'assignments',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.AssignmentsRead },
        loadComponent: () =>
          import('./features/assignments/assignment-list/assignment-list.component').then(
            (m) => m.AssignmentList,
          ),
        title: 'Assignments',
      },
      {
        path: 'assignments/new',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.AssignmentsWrite },
        loadComponent: () =>
          import('./features/assignments/assignment-form/assignment-form.component').then(
            (m) => m.AssignmentForm,
          ),
        title: 'Create assignment',
      },
      {
        path: 'assignments/:assignmentId/attempts/:attemptId',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.AttemptsRead },
        loadComponent: () =>
          import('./features/assignments/attempt-detail/attempt-detail.component').then(
            (m) => m.AttemptDetail,
          ),
        title: 'Attempt',
      },
      {
        path: 'assignments/:id',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.AssignmentsRead },
        loadComponent: () =>
          import('./features/assignments/assignment-detail/assignment-detail.component').then(
            (m) => m.AssignmentDetail,
          ),
        title: 'Assignment',
      },
      {
        path: 'opening-fields',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.OpeningsFieldsManage },
        loadComponent: () =>
          import('./features/openings/field-definitions/field-definitions.component').then(
            (m) => m.FieldDefinitions,
          ),
        title: 'Opening field defaults',
      },
      {
        path: 'roles',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.RolesManage },
        loadComponent: () =>
          import('./features/admin/role-list/role-list.component').then((m) => m.RoleList),
        title: 'Roles',
      },
      {
        path: 'roles/new',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.RolesManage },
        loadComponent: () =>
          import('./features/admin/role-editor/role-editor.component').then((m) => m.RoleEditor),
        title: 'Create role',
      },
      {
        path: 'roles/:id',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.RolesManage },
        loadComponent: () =>
          import('./features/admin/role-editor/role-editor.component').then((m) => m.RoleEditor),
        title: 'Edit role',
      },
      {
        path: 'users',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.UsersManage },
        loadComponent: () =>
          import('./features/admin/user-list/user-list.component').then((m) => m.UserList),
        title: 'Users',
      },
      {
        path: 'users/new',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.UsersManage },
        loadComponent: () =>
          import('./features/admin/user-form/user-form.component').then((m) => m.UserForm),
        title: 'Create user',
      },
      {
        path: 'users/:id',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.UsersManage },
        loadComponent: () =>
          import('./features/admin/user-form/user-form.component').then((m) => m.UserForm),
        title: 'User',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
