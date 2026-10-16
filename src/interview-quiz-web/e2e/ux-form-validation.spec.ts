import { expect, test, type Page, type Route } from '@playwright/test';

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

async function signIn(page: Page, permissions: string[], returnUrl: string): Promise<void> {
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
    if (method === 'GET' && pathname.endsWith('/api/roles')) {
      await json(route, []);
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/permissions')) {
      await json(route, []);
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/openings')) {
      await json(route, {
        items: [{ id: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001', title: 'Senior backend engineer' }],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      });
      return;
    }
    if (method === 'POST' && pathname.endsWith('/api/ai-drafts')) {
      await json(route, {
        id: 'draft-1',
        openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
        title: 'Draft quiz',
        expectedExperienceYears: 3,
        resumeText: null,
        rulesText: null,
        templateId: null,
        templateVersionId: null,
        model: 'fake',
        status: 'generated',
        acceptedQuizId: null,
        createdAtUtc: '2026-10-16T08:00:00.000Z',
        questions: [
          {
            id: 'q1',
            sortOrder: 0,
            type: 'trueFalse',
            stem: 'A sample question.',
            scoringMode: 'auto',
            creditMode: null,
            points: 1,
            body: { correct: true },
          },
        ],
      });
      return;
    }
    await json(route, { message: 'unmocked ' + pathname }, 404);
  });

  await page.goto('/login?returnUrl=' + encodeURIComponent(returnUrl));
  await page.getByLabel('Email').fill('staff.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Admin!1');
  await page.getByRole('button', { name: 'Sign in' }).click();
}

test('create opening names the missing fields', async ({ page }) => {
  await signIn(page, ['openings.write', 'openings.read'], '/openings/new');
  await expect(page.getByRole('heading', { name: 'Create opening' })).toBeVisible();
  await page.getByRole('button', { name: 'Save opening' }).click();
  await expect(page.getByText('Title is required.')).toBeVisible();
  await expect(page.getByText('Choose an owner.')).toBeVisible();
  await expect(page.getByText('Start date is required.')).toBeVisible();
});

test('create user and create role name the missing fields', async ({ page }) => {
  await signIn(page, ['users.manage', 'roles.manage'], '/users/new');
  await expect(page.getByRole('heading', { name: 'Create user' })).toBeVisible();
  await page.getByRole('button', { name: 'Create user' }).click();
  await expect(page.getByText('Enter a valid email address.')).toBeVisible();
  await expect(page.getByText('Password is required.')).toBeVisible();

  await page.getByLabel('Temporary password').fill('short');
  await page.getByRole('button', { name: 'Create user' }).click();
  await expect(page.getByText('Use at least 8 characters.')).toBeVisible();

  await page.goto('/roles/new');
  await page.waitForURL('**/roles/new');
  await expect(page.getByRole('heading', { name: 'Create role' })).toBeVisible();
  await page.getByRole('button', { name: 'Save role' }).click();
  await expect(page.getByText('Name is required.')).toBeVisible();
});

test('an AI draft without an opening says so, and a sample draft hides the model name', async ({ page }) => {
  await signIn(page, ['ai.draft.use', 'quizzes.write', 'openings.read'], '/ai-drafts/new');
  await expect(page.getByRole('heading', { name: 'AI draft' })).toBeVisible();
  await page.getByTestId('ai-draft-generate').click();
  await expect(page.getByTestId('ai-draft-opening-error')).toHaveText('Select an opening.');

  await expect(page.getByTestId('ai-draft-opening').locator('option')).toHaveCount(2);
  await page.getByTestId('ai-draft-opening').selectOption('3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001');
  await page.getByTestId('ai-draft-generate').click();
  await expect(page.getByTestId('ai-draft-model')).toContainText('This is a sample draft.');
  await expect(page.getByTestId('ai-draft-model')).not.toContainText('fake');
});
