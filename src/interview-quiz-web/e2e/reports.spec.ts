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

async function signIn(page: Page, returnUrl: string): Promise<void> {
  await page.goto(`/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  await page.getByLabel('Email').fill('recruiter.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Recruiter!1');
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
  await expect(page.getByText('Do not mix cohorts across quiz versions or snapshots.')).toBeVisible();
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

test('reports stay hidden without reports.read', async ({ page }) => {
  await install(page, ['assignments.read', 'attempts.read'], []);
  await signIn(page, '/');
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Reporting' })).toHaveCount(0);
  await page.goto('/reports');
  await expect(page).toHaveURL(/\/$/);
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
});
