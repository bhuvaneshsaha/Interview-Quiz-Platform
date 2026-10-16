import { expect, test, type Page, type Route } from '@playwright/test';
import { assignmentId } from './fixtures';

const openingId = '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001';
const quizId = '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001';
const templateId = '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001';
const versionId = '6d0f4a43-9e50-4f2d-8b44-3c1f5e9d4001';
const personId = '015d57be-e8e1-4ed9-8aa3-dd5d2115cc51';

test('a forbidden page says so on Home', async ({ page }) => {
  await install(page, ['questions.read']);
  await signIn(page, '/assignments');
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.getByText("You don't have access to that page.")).toBeVisible();
});

test('empty question and template lists say what to do next', async ({ page }) => {
  await install(page, ['questions.read', 'templates.read', 'filters.write']);
  await signIn(page, '/questions');
  await page.getByLabel('Keyword').fill('missing');
  await page.getByRole('button', { name: 'Filter' }).click();
  await expect(page.getByText('No questions match these filters.')).toBeVisible();
  await page.getByRole('button', { name: 'Clear filters' }).click();
  await expect(page.getByRole('link', { name: 'What is a quiz?' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Clear filters' })).toHaveCount(0);

  await page.getByRole('link', { name: 'Templates' }).click();
  await expect(page.getByText('A template is published from a quiz.')).toBeVisible();
  await expect(page.getByText('Save these filters')).toBeVisible();
  await expect(page.getByText('criteria', { exact: true })).toHaveCount(0);
});

test('assignment filters, opening counts, and attempt links stay readable', async ({ page }) => {
  await install(page, [
    'openings.read',
    'quizzes.read',
    'assignments.read',
    'assignments.write',
    'attempts.read',
    'attempts.review',
    'templates.read',
    'filters.write',
    'filters.share',
  ]);
  await signIn(page, '/assignments');
  await expect(page.getByLabel('Quiz')).toBeVisible();
  await expect(page.getByRole('option', { name: 'Backend interview' })).toBeAttached();
  await expect(page.getByLabel('Quiz id')).toHaveCount(0);

  await page.goto(`/openings/${openingId}`);
  await expect(page.getByText('Assigned 2 · Started 1 · Submitted 1')).toHaveCount(2);
  await expect(page.getByRole('link', { name: 'View assignments' }).first()).toBeVisible();
  await expect(page.getByRole('link', { name: /Appeared/ })).toHaveCount(0);

  await page.goto(`/assignments/${assignmentId}`);
  const detail = page.locator('[data-label="Detail"]');
  await expect(detail.getByRole('link', { name: 'View', exact: true })).toBeVisible();
  await expect(detail.getByRole('link', { name: 'Review', exact: true })).toBeVisible();
  await expect(detail).toContainText('View·Review');

  await page.goto(`/quizzes/${quizId}`);
  await expect(page.getByRole('link', { name: 'View template' })).toBeVisible();
  await expect(page.getByText(templateId)).toHaveCount(0);

  await page.goto(`/templates/${templateId}`);
  await expect(page.getByRole('link', { name: 'View original quiz' })).toBeVisible();
  await expect(page.getByText(quizId)).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Showing questions' })).toBeVisible();
  const actions = page.locator('.version-actions');
  await expect(actions.getByRole('link', { name: 'Preview as candidate' })).toBeVisible();
  const gap = await actions.evaluate((element) => getComputedStyle(element).gap);
  expect(gap).not.toBe('0px');
});

test('saved filters let a recruiter pick a person', async ({ page }) => {
  await install(page, ['templates.read', 'filters.write', 'filters.share', 'openings.read']);
  await signIn(page, '/templates');
  await page.getByLabel('Visible filters').selectOption({ label: 'Backend roles (Private)' });
  await page.getByLabel('Who can use this').selectOption({ label: 'Specific people' });
  await expect(page.getByLabel('Add a person')).toBeVisible();
  await expect(page.getByLabel('User ids')).toHaveCount(0);
  await page.getByLabel('Add a person').selectOption({ label: 'author.dev@example.com' });
  await expect(page.getByRole('listitem').filter({ hasText: 'author.dev@example.com' })).toBeVisible();
});

async function signIn(page: Page, returnUrl: string): Promise<void> {
  await page.goto(`/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  await page.getByLabel('Email').fill('recruiter.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Recruiter!1');
  await page.getByRole('button', { name: 'Sign in' }).click();
}

async function install(page: Page, permissions: string[]): Promise<void> {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const pathname = url.pathname;
    const method = request.method();
    if (method === 'POST' && (pathname.endsWith('/api/auth/login') || pathname.endsWith('/api/auth/refresh'))) {
      await json(route, tokens);
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/me')) {
      await json(route, { id: 'staff-1', email: 'recruiter.dev@example.com', permissions });
      return;
    }
    if (method === 'GET' && pathname === '/api/questions') {
      const criteria = url.searchParams.get('criteria') ?? '';
      const items = criteria.includes('missing')
        ? []
        : [
            {
              id: 'q-1',
              title: 'What is a quiz?',
              type: 'trueFalse',
              expectedExperienceYears: 0,
              stem: 'A quiz is a set of questions.',
              archivedAtUtc: null,
            },
          ];
      await json(route, { items, page: 1, pageSize: 20, totalCount: items.length });
      return;
    }
    if (method === 'GET' && pathname === '/api/templates') {
      await json(route, { items: [], page: 1, pageSize: 20, totalCount: 0 });
      return;
    }
    if (method === 'GET' && pathname === '/api/filters') {
      await json(route, {
        items: [
          {
            id: '7e1f5b54-0f6a-4a3e-1b55-4d2f6e0e5001',
            name: 'Backend roles',
            target: 'templates',
            criteria: {},
            ownerUserId: 'staff-1',
            shareMode: 'private',
            sharedWithUserIds: [],
            createdAtUtc: '2026-01-01T00:00:00Z',
            updatedAtUtc: '2026-01-01T00:00:00Z',
          },
        ],
        page: 1,
        pageSize: 50,
        totalCount: 1,
      });
      return;
    }
    if (method === 'GET' && pathname === '/api/users/assignable') {
      await json(route, [{ id: personId, name: 'Author', email: 'author.dev@example.com' }]);
      return;
    }
    if (method === 'GET' && pathname === '/api/quizzes') {
      await json(route, {
        items: [{ id: quizId, title: 'Backend interview', openingId, questions: [] }],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      });
      return;
    }
    if (method === 'GET' && pathname === '/api/assignments') {
      await json(route, { items: [], page: 1, pageSize: 20, totalCount: 0 });
      return;
    }
    if (method === 'GET' && pathname === '/api/openings') {
      await json(route, { items: [], page: 1, pageSize: 100, totalCount: 0 });
      return;
    }
    if (method === 'GET' && pathname === `/api/openings/${openingId}`) {
      await json(route, {
        id: openingId,
        title: 'Senior backend engineer',
        jobDescription: 'Build APIs.',
        owner: personId,
        startDate: '2026-01-01',
        expectedCloseDate: null,
        headcount: 1,
        expectedExperienceYears: 3,
        handlers: [],
        tags: {},
        rowVersion: 1,
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/openings/${openingId}/summary`) {
      await json(route, {
        candidates: { assigned: 2, appeared: 1, submitted: 1 },
        quizzes: [
          {
            id: quizId,
            title: 'Backend interview',
            questionCount: 3,
            updatedAtUtc: '2026-01-01T00:00:00Z',
            candidates: { assigned: 2, appeared: 1, submitted: 1 },
          },
        ],
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/assignments/${assignmentId}`) {
      await json(route, {
        id: assignmentId,
        openingId,
        quizId,
        snapshotId: '8f2b6c65-1a7c-4b4f-ad66-5e3b7f1f6001',
        snapshotTitle: 'Backend interview',
        snapshotQuestionCount: 3,
        snapshotQuizRowVersion: 1,
        candidateEmail: 'candidate.dev@example.com',
        mode: 'async',
        overallDurationMinutes: 30,
        attemptLimit: 1,
        status: 'pendingReview',
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
        createdByUserId: 'staff-1',
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/assignments/${assignmentId}/attempts`) {
      await json(route, {
        items: [
          {
            id: '9a3c7d76-2b8d-4c5e-be77-6f4c8a2a7001',
            assignmentId,
            openingId,
            candidateEmail: 'candidate.dev@example.com',
            status: 'pendingReview',
            resultStatus: 'pendingReview',
            startedAtUtc: '2026-01-02T00:00:00Z',
            submittedAtUtc: '2026-01-02T01:00:00Z',
            autoPointsAwarded: 1,
            autoPointsAvailable: 2,
            totalPointsAvailable: 2,
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/quizzes/${quizId}`) {
      await json(route, {
        id: quizId,
        openingId,
        title: 'Backend interview',
        description: '',
        expectedExperienceYears: 3,
        tags: {},
        questions: [],
        originTemplateId: templateId,
        sourceTemplateVersionId: versionId,
        rowVersion: 1,
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
        assignmentCount: 0,
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/templates/${templateId}` && !pathname.includes('/versions')) {
      await json(route, {
        id: templateId,
        title: 'Backend template',
        description: '',
        expectedExperienceYears: 3,
        tags: {},
        latestVersionId: versionId,
        latestVersionNumber: 1,
        originQuizId: quizId,
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
        rowVersion: 1,
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/templates/${templateId}/versions`) {
      await json(route, {
        items: [
          {
            id: versionId,
            templateId,
            versionNumber: 1,
            title: 'Backend template',
            publishedFromQuizId: quizId,
            publishedAtUtc: '2026-01-01T00:00:00Z',
            publishedByUserId: 'staff-1',
            publishedByDisplayName: 'Author',
            changeNote: '',
            questionCount: 1,
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/templates/${templateId}/versions/${versionId}`) {
      await json(route, {
        id: versionId,
        templateId,
        versionNumber: 1,
        title: 'Backend template',
        description: '',
        expectedExperienceYears: 3,
        tags: {},
        questions: [],
        publishedFromQuizId: quizId,
        publishedAtUtc: '2026-01-01T00:00:00Z',
        publishedByUserId: 'staff-1',
        publishedByDisplayName: 'Author',
        changeNote: '',
        questionCount: 0,
      });
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}

const tokens = {
  accessToken: 'staff-access-token',
  refreshToken: 'staff-refresh-token',
  accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
  refreshTokenExpiresAt: '2099-01-02T00:00:00.000Z',
  tokenType: 'Bearer',
};

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}
