import { expect, test, type Page } from '@playwright/test';
import { assignmentId, installCandidateApi, screenshotPath, welcomePreview } from './fixtures';

const openingId = '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001';

async function installAssignmentList(page: Page): Promise<void> {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();

    if (method === 'POST' && pathname.endsWith('/api/auth/login')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          accessToken: 'recruiter-access-token',
          refreshToken: 'recruiter-refresh-token',
          accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
          refreshTokenExpiresAt: '2099-01-02T00:00:00.000Z',
          tokenType: 'Bearer',
        }),
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/me')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'recruiter-1',
          email: 'recruiter.dev@example.com',
          permissions: ['assignments.read', 'assignments.write', 'openings.read'],
        }),
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/openings')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [{ id: openingId, title: 'Senior backend engineer' }],
          page: 1,
          pageSize: 100,
          totalCount: 1,
        }),
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/assignments')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: assignmentId,
              openingId,
              quizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
              snapshotId: '6d0f4a43-9e5c-4b2d-ad44-3c1b5e9d4001',
              snapshotTitle: 'Backend interview — working copy',
              snapshotQuestionCount: 2,
              candidateEmail: 'candidate.dev@example.com',
              mode: 'async',
              overallDurationMinutes: 30,
              attemptLimit: 1,
              status: 'pendingReview',
              createdAtUtc: '2026-10-15T08:00:00.000Z',
              updatedAtUtc: '2026-10-15T09:00:00.000Z',
            },
          ],
          page: 1,
          pageSize: 20,
          totalCount: 1,
        }),
      });
      return;
    }
    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ message: `Unmocked ${method} ${pathname}` }),
    });
  });
}

async function expectNoPageScroll(page: Page): Promise<void> {
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 1);
}

test('assignment list fits a 375px viewport and keeps Create assignment reachable', async ({ page }) => {
  await installAssignmentList(page);
  await page.goto('/login?returnUrl=/assignments');
  await page.getByLabel('Email').fill('recruiter.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Recruiter!1');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Assignments' })).toBeVisible();
  const create = page.getByRole('link', { name: 'Create assignment' });
  await expect(create).toBeVisible();
  await expect(page.getByRole('link', { name: 'candidate.dev@example.com' })).toBeVisible();
  await expect(page.getByText('Pending review')).toBeVisible();
  await expectNoPageScroll(page);

  const box = await create.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.x).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width).toBeLessThanOrEqual(375 + 1);

  await page.screenshot({ path: screenshotPath('mobile-assignment-list.png'), fullPage: true });
});

test('attempt questions fit a 375px viewport and keep Start quiz reachable', async ({ page }) => {
  await installCandidateApi(page, welcomePreview);
  await page.goto('/attempt?token=opaque-token');

  const start = page.getByRole('button', { name: 'Start quiz' });
  await expect(start).toBeVisible();
  await expectNoPageScroll(page);
  const welcomeBox = await start.boundingBox();
  expect(welcomeBox).not.toBeNull();
  expect(welcomeBox!.x + welcomeBox!.width).toBeLessThanOrEqual(375 + 1);

  await start.click();
  await expect(page.getByText('Question 1 of 2')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Which HTTP status means a resource was created?' })).toBeVisible();
  const next = page.getByRole('button', { name: 'Next' });
  const submit = page.getByRole('button', { name: 'Submit quiz' });
  await expect(next).toBeVisible();
  await expect(submit).toBeVisible();
  await expectNoPageScroll(page);
  for (const action of [next, submit]) {
    const box = await action.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(375 + 1);
  }

  await page.screenshot({ path: screenshotPath('mobile-attempt-questions.png'), fullPage: true });
});

async function installRecruiterLists(page: Page): Promise<void> {
  const pageOf = (items: unknown[]) => ({
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
  });
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();
    if (method === 'POST' && pathname.endsWith('/api/auth/login')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          accessToken: 'recruiter-access-token',
          refreshToken: 'recruiter-refresh-token',
          accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
          refreshTokenExpiresAt: '2099-01-02T00:00:00.000Z',
          tokenType: 'Bearer',
        }),
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/me')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'recruiter-1',
          email: 'recruiter.dev@example.com',
          permissions: [
            'openings.read',
            'openings.write',
            'quizzes.read',
            'templates.read',
            'questions.read',
            'assignments.read',
            'filters.write',
          ],
        }),
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/filters')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(pageOf([])),
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/openings')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          pageOf([
            {
              id: openingId,
              title: 'Senior backend engineer',
              owner: 'recruiter.dev@example.com',
              startDate: '2026-11-01',
              headcount: 2,
              expectedExperienceYears: 5,
            },
          ]),
        ),
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/quizzes')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          pageOf([
            {
              id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
              openingId,
              title: 'Backend interview — working copy',
              expectedExperienceYears: 5,
              questions: [],
              updatedAtUtc: '2026-10-15T08:00:00.000Z',
            },
          ]),
        ),
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/templates')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          pageOf([
            {
              id: '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001',
              title: 'Backend interview template',
              expectedExperienceYears: 5,
              latestVersionNumber: 1,
              updatedAtUtc: '2026-10-15T08:00:00.000Z',
            },
          ]),
        ),
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/questions')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          pageOf([
            {
              id: '6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4001',
              title: 'HTTP created',
              type: 'multipleChoiceSingle',
              expectedExperienceYears: 3,
              stem: 'Which status means created?',
              archivedAtUtc: null,
            },
          ]),
        ),
      });
      return;
    }
    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ message: `Unmocked ${method} ${pathname}` }),
    });
  });
}

test('recruiter list pages fit a 375px viewport', async ({ page }) => {
  await installRecruiterLists(page);
  await page.goto('/login?returnUrl=/openings');
  await page.getByLabel('Email').fill('recruiter.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Recruiter!1');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Openings' })).toBeVisible();
  const createOpening = page.getByRole('link', { name: 'Create opening' });
  await expect(createOpening).toBeVisible();
  await expectNoPageScroll(page);
  const openingBox = await createOpening.boundingBox();
  expect(openingBox).not.toBeNull();
  expect(openingBox!.x + openingBox!.width).toBeLessThanOrEqual(375 + 1);
  await page.screenshot({ path: screenshotPath('mobile-opening-list.png'), fullPage: true });

  await page.getByRole('link', { name: 'Quizzes' }).click();
  await expect(page.getByRole('heading', { name: 'Quizzes' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Backend interview — working copy' })).toBeVisible();
  await expectNoPageScroll(page);
  await page.screenshot({ path: screenshotPath('mobile-quiz-list.png'), fullPage: true });

  await page.getByRole('link', { name: 'Templates' }).click();
  await expect(page.getByRole('heading', { name: 'Templates' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Backend interview template' })).toBeVisible();
  await expectNoPageScroll(page);

  await page.getByRole('link', { name: 'Question bank' }).click();
  await expect(page.getByRole('heading', { name: 'Question bank' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'HTTP created' })).toBeVisible();
  await expectNoPageScroll(page);
});
