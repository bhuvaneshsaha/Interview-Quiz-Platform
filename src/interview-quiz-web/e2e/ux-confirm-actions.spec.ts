import { expect, test, type Page, type Route } from '@playwright/test';

const assignmentId = '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001';
const questionId = '6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4001';
const userId = '015d57be-e8e1-4ed9-8aa3-dd5d2115cc51';
const roleId = 'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2001';

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

async function signIn(page: Page, permissions: string[], returnUrl: string, handle: (route: Route, pathname: string, method: string) => Promise<boolean>): Promise<void> {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();
    if (method === 'POST' && (pathname.endsWith('/api/auth/login') || pathname.endsWith('/api/auth/refresh'))) {
      await json(route, {
        accessToken: 'access',
        refreshToken: 'refresh',
        accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
        refreshTokenExpiresAt: '2099-01-02T00:00:00.000Z',
        tokenType: 'Bearer',
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/me')) {
      await json(route, { id: 'user-1', email: 'staff.dev@example.com', permissions });
      return;
    }
    if (await handle(route, pathname, method)) {
      return;
    }
    await json(route, { message: 'unmocked ' + method + ' ' + pathname }, 404);
  });
  await page.goto('/login?returnUrl=' + encodeURIComponent(returnUrl));
  await page.getByLabel('Email').fill('staff.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Recruiter!1');
  await page.getByRole('button', { name: 'Sign in' }).click();
}

test('an active invite is labeled re-issue and is not replaced until confirmed', async ({ page }) => {
  let invites = 0;
  await signIn(page, ['assignments.read', 'assignments.write', 'attempts.read'], `/assignments/${assignmentId}`, async (route, pathname, method) => {
    if (method === 'GET' && pathname === `/api/assignments/${assignmentId}`) {
      await json(route, {
        id: assignmentId,
        openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
        quizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
        snapshotId: '8f2b6c65-1a7c-4b4f-ad66-5e3b7f1f6001',
        snapshotTitle: 'Backend interview',
        snapshotQuestionCount: 8,
        snapshotQuizRowVersion: 1,
        candidateEmail: 'candidate.dev@example.com',
        mode: 'async',
        overallDurationMinutes: 30,
        attemptLimit: 1,
        status: 'notStarted',
        inviteState: 'active',
        inviteEmailStatus: 'sent',
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
      });
      return true;
    }
    if (method === 'GET' && pathname === `/api/assignments/${assignmentId}/attempts`) {
      await json(route, { items: [], page: 1, pageSize: 20, totalCount: 0 });
      return true;
    }
    if (method === 'POST' && pathname === `/api/assignments/${assignmentId}/invite`) {
      invites += 1;
      await json(route, {
        assignmentId,
        inviteUrl: 'http://localhost:4200/attempt?token=new',
        inviteState: 'active',
      });
      return true;
    }
    return false;
  });

  await expect(page.getByRole('button', { name: 'Re-issue invite link' })).toBeVisible();
  await page.getByRole('button', { name: 'Re-issue invite link' }).click();
  await expect(page.getByRole('alertdialog')).toContainText('will stop working');
  expect(invites).toBe(0);
  await page.getByTestId('confirm-rotate').click();
  await expect(page.getByTestId('invite-url')).toHaveValue(/token=new/);
  expect(invites).toBe(1);
});

test('archive, disable, and delete role wait for confirmation', async ({ page }) => {
  let archives = 0;
  let disables = 0;
  let deletes = 0;
  const question = {
    id: questionId,
    title: 'HTTP 201',
    tags: {},
    expectedExperienceYears: 1,
    type: 'trueFalse',
    stem: 'Created means 201.',
    scoringMode: 'auto',
    creditMode: null,
    points: 1,
    body: { correct: true },
    archivedAtUtc: null as string | null,
    rowVersion: 1,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z',
  };
  await signIn(
    page,
    ['questions.write', 'questions.read', 'users.manage', 'roles.manage'],
    `/questions/${questionId}`,
    async (route, pathname, method) => {
      if (method === 'GET' && pathname === `/api/questions/${questionId}`) {
        await json(route, question);
        return true;
      }
      if (method === 'POST' && pathname === `/api/questions/${questionId}/archive`) {
        archives += 1;
        await json(route, { ...question, archivedAtUtc: '2026-10-16T00:00:00Z', rowVersion: 2 });
        return true;
      }
      if (method === 'GET' && pathname === `/api/users/${userId}`) {
        await json(route, { id: userId, email: 'author.dev@example.com', isDisabled: false, roleIds: [roleId] });
        return true;
      }
      if (method === 'GET' && pathname === '/api/roles' && !pathname.includes(roleId)) {
        await json(route, []);
        return true;
      }
      if (method === 'PUT' && pathname === `/api/users/${userId}`) {
        disables += 1;
        await json(route, { id: userId, email: 'author.dev@example.com', isDisabled: true, roleIds: [roleId] });
        return true;
      }
      if (method === 'GET' && pathname === '/api/permissions') {
        await json(route, []);
        return true;
      }
      if (method === 'GET' && pathname === `/api/roles/${roleId}`) {
        await json(route, {
          id: roleId,
          name: 'Dev Recruiter',
          description: 'Sample',
          permissionCodes: [],
          userIds: [],
          isSystem: false,
        });
        return true;
      }
      if (method === 'DELETE' && pathname === `/api/roles/${roleId}`) {
        deletes += 1;
        await json(route, null, 204);
        return true;
      }
      return false;
    },
  );

  await expect(page.getByTestId('archive-question')).toBeVisible();
  await page.getByTestId('archive-question').click();
  await expect(page.getByRole('alertdialog')).toContainText('Archive this question?');
  expect(archives).toBe(0);
  await page.getByRole('button', { name: 'Cancel' }).click();
  expect(archives).toBe(0);
  await page.getByTestId('archive-question').click();
  await page.getByTestId('confirm-archive').click();
  await expect(page.getByText('This question is archived.')).toBeVisible();
  expect(archives).toBe(1);

  await page.goto(`/users/${userId}`);
  await page.getByRole('button', { name: 'Disable user' }).click();
  await expect(page.getByRole('alertdialog')).toContainText('Disable this user?');
  expect(disables).toBe(0);
  await page.getByTestId('confirm-disable').click();
  await expect(page.getByText('Disabled')).toBeVisible();
  expect(disables).toBe(1);

  await page.goto(`/roles/${roleId}`);
  await page.getByRole('button', { name: 'Delete role' }).click();
  await expect(page.getByRole('alertdialog')).toContainText('Delete this role?');
  expect(deletes).toBe(0);
  await page.getByTestId('confirm-delete-role').click();
  await expect(page.getByRole('heading', { name: 'Roles' })).toBeVisible();
  expect(deletes).toBe(1);
});
