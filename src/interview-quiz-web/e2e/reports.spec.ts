import { expect, test, type Page, type Route } from '@playwright/test';

const openingId = '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001';
const quizId = '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001';

const funnel = {
  defaultWindowApplied: true,
  appliedCreatedFromUtc: '2026-01-01T00:00:00.000Z',
  appliedCreatedToUtc: null,
  page: 1,
  pageSize: 20,
  totalCount: 1,
  totals: {
    openingTitle: '',
    quizTitle: '',
    assigned: 3,
    appeared: 2,
    submitted: 2,
    completed: 1,
    pendingReview: 0,
    appearedRate: 0.6667,
    submittedRate: 0.6667,
    completedRate: 0.3333,
  },
  items: [
    {
      openingId,
      openingTitle: 'Backend opening',
      quizId,
      quizTitle: 'Backend quiz',
      assigned: 3,
      appeared: 2,
      submitted: 2,
      completed: 1,
      pendingReview: 0,
      appearedRate: 0.6667,
      submittedRate: 0.6667,
      completedRate: 0.3333,
    },
  ],
};

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

async function install(page: Page, permissions: string[], criteriaCalls: string[]): Promise<void> {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const pathname = url.pathname;
    const method = request.method();
    if (method === 'POST' && pathname.endsWith('/api/auth/login')) {
      await json(route, {
        accessToken: 'staff-access-token',
        refreshToken: 'staff-refresh-token',
        accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
        refreshTokenExpiresAt: '2099-01-02T00:00:00.000Z',
        tokenType: 'Bearer',
      });
      return;
    }
    if (method === 'POST' && pathname.endsWith('/api/auth/refresh')) {
      await json(route, {
        accessToken: 'staff-access-token',
        refreshToken: 'staff-refresh-token',
        accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
        refreshTokenExpiresAt: '2099-01-02T00:00:00.000Z',
        tokenType: 'Bearer',
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/me')) {
      await json(route, {
        id: 'recruiter-1',
        email: 'recruiter.dev@example.com',
        permissions,
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/filters')) {
      await json(route, { items: [], page: 1, pageSize: 50, totalCount: 0 });
      return;
    }
    if (method === 'GET' && pathname === '/api/reports/funnel') {
      criteriaCalls.push(url.searchParams.get('criteria') ?? '');
      await json(route, funnel);
      return;
    }
    if (method === 'GET' && pathname === '/api/reports/funnel/export') {
      await route.fulfill({
        status: 200,
        contentType: 'text/csv; charset=utf-8',
        headers: { 'content-disposition': 'attachment; filename="funnel.csv"' },
        body: 'openingId,openingTitle\n',
      });
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}

async function signIn(
  page: Page,
  returnUrl: string,
  email = 'recruiter.dev@example.com',
  password = 'Dev.Recruiter!1',
): Promise<void> {
  await page.goto(`/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
}

test('reporting hub, funnel chart, filter, and csv export', async ({ page }) => {
  const criteriaCalls: string[] = [];
  await install(page, ['reports.read', 'filters.write', 'assignments.read', 'attempts.read'], criteriaCalls);
  await signIn(page, '/reports');
  await expect(page.getByRole('link', { name: 'Reporting' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Reporting' })).toBeVisible();
  await expect(page.getByTestId('report-card')).toHaveCount(6);

  await page.getByRole('link', { name: /Funnel/ }).click();
  await expect(page.getByRole('heading', { name: 'Funnel' })).toBeVisible();
  await expect(page.getByTestId('report-table')).toContainText('Backend opening');
  await expect(page.getByRole('img', { name: /Assigned 3/ })).toBeVisible();
  await expect(page.getByText('Showing assignments created in the last 90 days.')).toBeVisible();
  await expect(page.getByText("Don't combine results from different quiz versions.")).toBeVisible();
  await expect(page.getByRole('link', { name: 'Backend opening' })).toHaveAttribute(
    'href',
    `/assignments?openingId=${openingId}`,
  );

  await page.getByLabel('Value').fill(quizId);
  await page.getByTestId('add-filter').click();
  await expect.poll(() => criteriaCalls.some((criteria) => criteria.includes(quizId))).toBe(true);
  await expect(page.getByRole('button', { name: 'Remove filter openingId is' })).toBeVisible();

  const downloadPromise = page.waitForEvent('download');
  await page.getByTestId('export-csv').click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe('funnel.csv');
});

test('super admin sees the Reporting nav link', async ({ page }) => {
  await install(
    page,
    ['reports.read', 'users.manage', 'roles.manage', 'openings.read', 'assignments.read'],
    [],
  );
  await signIn(page, '/');
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Primary' }).getByRole('link', { name: 'Reporting' })).toBeVisible();
});

test('a signed-in reload of /reports stays on the page', async ({ page }) => {
  await install(page, ['reports.read'], []);
  await signIn(page, '/reports');
  await expect(page.getByRole('heading', { name: 'Reporting' })).toBeVisible();
  await page.reload();
  await expect(page).toHaveURL(/\/reports$/);
  await expect(page.getByRole('heading', { name: 'Reporting' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Reporting' })).toBeVisible();
});

test('admin.dev opens /reports', async ({ page }) => {
  await install(
    page,
    [
      'reports.read',
      'users.manage',
      'roles.manage',
      'openings.read',
      'quizzes.read',
      'templates.read',
    ],
    [],
  );
  const login = page.waitForRequest(
    (request) => request.method() === 'POST' && request.url().includes('/api/auth/login'),
  );
  await signIn(page, '/reports', 'admin.dev@example.com', 'Dev.Admin!1');
  expect((await login).postDataJSON()).toMatchObject({
    email: 'admin.dev@example.com',
    password: 'Dev.Admin!1',
  });
  await expect(page).toHaveURL(/\/reports$/);
  await expect(page.getByRole('heading', { name: 'Reporting' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Primary' }).getByRole('link', { name: 'Reporting' })).toBeVisible();
});

test('a candidate does not see Reporting and cannot open /reports', async ({ page }) => {
  await install(page, ['candidate.attempt.participate'], []);
  await signIn(page, '/');
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Primary' }).getByRole('link', { name: 'Reporting' })).toHaveCount(0);
  await expect(page.locator('#main').getByRole('link', { name: 'Reporting' })).toHaveCount(0);
  await page.goto('/reports');
  await expect(page).toHaveURL(/\/\?denied=1$/);
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.getByText("You don't have access to that page.")).toBeVisible();
});

test('the role editor lists View reports under Evaluation', async ({ page }) => {
  await install(page, ['roles.manage', 'reports.read'], []);
  await page.route('**/api/permissions', async (route) => {
    await json(route, [
      {
        code: 'reports.read',
        displayName: 'View reports',
        module: 'evaluation',
        includeInEmployeeRoleEditor: true,
      },
      {
        code: 'attempts.read',
        displayName: 'View attempts and results',
        module: 'evaluation',
        includeInEmployeeRoleEditor: true,
      },
      {
        code: 'candidate.attempt.participate',
        displayName: 'Take and submit own attempt',
        module: 'candidate',
        includeInEmployeeRoleEditor: false,
      },
    ]);
  });
  await signIn(page, '/roles/new', 'admin.dev@example.com', 'Dev.Admin!1');
  await expect(page.getByRole('heading', { name: 'Create role' })).toBeVisible();
  const group = page.getByRole('region', { name: 'Evaluation' }).or(page.locator('section', { has: page.getByRole('heading', { name: 'Evaluation' }) }));
  await expect(page.getByRole('heading', { name: 'Evaluation' })).toBeVisible();
  const reports = page.getByRole('checkbox', { name: /View reports/ });
  await expect(reports).toBeVisible();
  await expect(reports).toBeEnabled();
  await expect(page.getByText('reports.read', { exact: true })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: /Take and submit own attempt/ })).toHaveCount(0);
  await reports.check();
  await expect(reports).toBeChecked();
  await reports.uncheck();
  await expect(reports).not.toBeChecked();
  await expect(group.getByRole('checkbox', { name: /View reports/ })).toBeVisible();
});

test('the role editor shows System and Built-in descriptions', async ({ page }) => {
  const superAdminId = 'f8c1a0b2-6d4e-4a11-9c3e-1b7a5d0e0001';
  const adminRoleId = 'aaaaaaaa-0a01-4c9a-9e11-0f8c2b6a2004';
  const superAdminDescription =
    'System role. Holds every employee permission except candidate attempt access. Cannot be renamed, deleted, or reduced.';
  const adminDescription =
    'Built-in role. Runs day-to-day administration and starts with every employee permission. Permissions can be edited. It cannot assign or revoke Super Admin, and it cannot be deleted.';
  await install(page, ['roles.manage'], []);
  await page.route('**/api/permissions', async (route) => {
    await json(route, [
      {
        code: 'reports.read',
        displayName: 'View reports',
        module: 'evaluation',
        includeInEmployeeRoleEditor: true,
      },
      {
        code: 'roles.manage',
        displayName: 'Manage permission groups',
        module: 'access',
        includeInEmployeeRoleEditor: true,
      },
    ]);
  });
  await page.route('**/api/roles/**', async (route) => {
    const id = new URL(route.request().url()).pathname.split('/').pop();
    const system = id === superAdminId;
    await json(route, {
      id: system ? superAdminId : adminRoleId,
      name: system ? 'Super Admin' : 'Admin',
      description: system ? superAdminDescription : adminDescription,
      permissionCodes: ['reports.read', 'roles.manage'],
      userIds: [],
      isSystem: system,
      isBuiltIn: !system,
    });
  });

  await signIn(page, `/roles/${superAdminId}`, 'admin.dev@example.com', 'Dev.Admin!1');
  await expect(page.getByRole('heading', { name: 'Edit role' })).toBeVisible();
  await expect(page.locator('.system-badge')).toHaveText('System');
  await expect(page.locator('.role-explanation')).toHaveText(superAdminDescription);
  await expect(page.getByLabel('Name')).toBeDisabled();
  await expect(page.getByRole('checkbox', { name: /View reports/ })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Save role' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Delete role' })).toHaveCount(0);

  await page.goto(`/roles/${adminRoleId}`);
  await expect(page.getByRole('heading', { name: 'Edit role' })).toBeVisible();
  await expect(page.locator('.builtin-badge')).toHaveText('Built-in');
  await expect(page.locator('.role-explanation')).toHaveText(adminDescription);
  await expect(page.getByLabel('Name')).toBeDisabled();
  await expect(page.getByLabel('Description')).toBeDisabled();
  const reports = page.getByRole('checkbox', { name: /View reports/ });
  await expect(reports).toBeEnabled();
  await reports.check();
  await expect(reports).toBeChecked();
  await expect(page.getByRole('button', { name: 'Save role' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Delete role' })).toHaveCount(0);
});

test('reports stay hidden without reports.read', async ({ page }) => {
  await install(page, ['assignments.read', 'attempts.read'], []);
  await signIn(page, '/');
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Reporting' })).toHaveCount(0);
  await page.goto('/reports');
  await expect(page).toHaveURL(/\/\?denied=1$/);
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.getByText("You don't have access to that page.")).toBeVisible();
});
