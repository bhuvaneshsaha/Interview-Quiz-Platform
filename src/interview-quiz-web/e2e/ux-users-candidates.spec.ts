import { expect, test, type Page, type Route } from '@playwright/test';

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

async function install(page: Page): Promise<boolean[]> {
  const includeCandidates: boolean[] = [];
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
      await json(route, {
        id: 'admin-1',
        email: 'admin.dev@example.com',
        permissions: ['users.manage', 'roles.manage'],
      });
      return;
    }
    if (method === 'GET' && pathname === '/api/users') {
      const include = new URL(request.url()).searchParams.get('includeCandidates') === 'true';
      includeCandidates.push(include);
      const staff = {
        id: 'staff-1',
        email: 'author.dev@example.com',
        isDisabled: false,
        roleIds: ['role-1'],
        isCandidate: false,
        roleNames: ['Template author'],
      };
      const candidate = {
        id: 'candidate-1',
        email: 'candidate.audit@example.com',
        isDisabled: false,
        roleIds: [],
        isCandidate: true,
        roleNames: [],
      };
      await json(route, {
        items: include ? [staff, candidate] : [staff],
        page: 1,
        pageSize: 20,
        totalCount: include ? 2 : 1,
      });
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
  return includeCandidates;
}

test('the users list hides candidates until Show candidates is checked', async ({ page }) => {
  const includeCandidates = await install(page);
  await page.goto('/login?returnUrl=%2Fusers');
  await page.getByLabel('Email').fill('admin.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Admin!1');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Users' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'author.dev@example.com' })).toBeVisible();
  await expect(page.getByText('Template author')).toBeVisible();
  await expect(page.getByRole('link', { name: 'candidate.audit@example.com' })).toHaveCount(0);
  expect(includeCandidates.some((value) => value)).toBe(false);

  await page.getByLabel('Show candidates').check();
  await expect(page.getByRole('link', { name: 'candidate.audit@example.com' })).toBeVisible();
  await expect(page.getByText('Candidate', { exact: true })).toBeVisible();
  expect(includeCandidates.some((value) => value)).toBe(true);
});
