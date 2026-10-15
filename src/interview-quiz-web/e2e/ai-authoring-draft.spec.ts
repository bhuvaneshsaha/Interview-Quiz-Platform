import { expect, test, type Page, type Route } from '@playwright/test';

const openingId = '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001';
const draftId = '9c1e4a22-1b2c-4d3e-8f44-5a6b7c8d9001';
const quizId = '9c1e4a22-1b2c-4d3e-8f44-5a6b7c8d9002';
const originalStem = 'Draft check for Senior backend engineer.';

function draft() {
  return {
    id: draftId,
    openingId,
    title: 'Draft: Senior backend engineer',
    expectedExperienceYears: 5,
    resumeText: 'Ten years of APIs.',
    rulesText: 'Prefer short stems.',
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
        stem: originalStem,
        scoringMode: 'auto',
        creditMode: null,
        points: 1,
        body: { correct: true },
      },
      {
        id: 'q2',
        sortOrder: 1,
        type: 'longText',
        stem: 'Draft written answer from the resume.',
        scoringMode: 'humanOnly',
        creditMode: null,
        points: 5,
        body: { guidance: 'Prefer short stems.' },
      },
    ],
  };
}

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

test('author edits an AI draft before it becomes a quiz and nothing is assigned', async ({ page }) => {
  const accepts: { questions: { stem: string }[] }[] = [];
  await install(page, ['ai.draft.use', 'quizzes.write', 'openings.read'], accepts);
  await signIn(page, '/ai-drafts/new');

  await expect(page.getByRole('heading', { name: 'AI draft' })).toBeVisible();
  await expect(page.getByTestId('ai-draft-not-assigned')).toBeHidden();
  await page.getByTestId('ai-draft-opening').selectOption({ label: 'Senior backend engineer' });
  await page.getByTestId('ai-draft-resume').fill('Ten years of APIs.');
  await page.getByTestId('ai-draft-rules').fill('Prefer short stems.');
  await page.getByTestId('ai-draft-generate').click();

  await expect(page.getByTestId('ai-draft-stem-0')).toHaveValue(originalStem);
  await expect(page.getByTestId('ai-draft-not-assigned')).toContainText('not assigned');
  await page.getByTestId('ai-draft-save').click();
  await expect(page.getByTestId('ai-draft-error')).toContainText(
    'Edit the draft before saving it as a quiz.',
  );
  expect(accepts).toHaveLength(1);
  expect(accepts[0].questions[0].stem).toBe(originalStem);

  await page.getByTestId('ai-draft-stem-0').fill(`${originalStem} Edited.`);
  await page.getByTestId('ai-draft-save').click();
  await expect.poll(() => accepts.length).toBe(2);
  expect(accepts[1].questions[0].stem).toBe(`${originalStem} Edited.`);
  expect(accepts[1].questions[0].stem).not.toBe(originalStem);
  await expect(page.getByTestId('ai-draft-saved')).toContainText('Quiz saved. It is not assigned.');
  await expect(page.getByTestId('ai-draft-not-assigned')).toBeVisible();
});

test('a user without ai.draft.use does not see the AI draft link', async ({ page }) => {
  await install(page, ['quizzes.read', 'assignments.read'], []);
  await signIn(page, '/');
  const nav = page.getByRole('navigation', { name: 'Primary' });
  await expect(nav.getByRole('link', { name: 'Quizzes' })).toBeVisible();
  await expect(nav.getByRole('link', { name: 'AI draft' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'AI draft' })).toHaveCount(0);
});

async function signIn(page: Page, returnUrl: string): Promise<void> {
  await page.goto(`/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  await page.getByLabel('Email').fill('author.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Author!1');
  await page.getByRole('button', { name: 'Sign in' }).click();
}

async function install(
  page: Page,
  permissions: string[],
  accepts: { questions: { stem: string }[] }[],
): Promise<void> {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();
    if (method === 'POST' && pathname.endsWith('/api/auth/login')) {
      await json(route, {
        accessToken: 'author-access-token',
        refreshToken: 'author-refresh-token',
        accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
        refreshTokenExpiresAt: '2099-01-02T00:00:00.000Z',
        tokenType: 'Bearer',
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/me')) {
      await json(route, {
        id: 'author-1',
        email: 'author.dev@example.com',
        permissions,
      });
      return;
    }
    if (method === 'GET' && pathname === '/api/openings') {
      await json(route, {
        items: [
          {
            id: openingId,
            title: 'Senior backend engineer',
            jobDescription: '',
            owner: 'owner',
            startDate: '2026-10-01',
            expectedCloseDate: null,
            headcount: 1,
            expectedExperienceYears: 5,
            handlers: [],
            tags: {},
            rowVersion: 1,
            createdAtUtc: '2026-10-01T00:00:00.000Z',
            updatedAtUtc: '2026-10-01T00:00:00.000Z',
          },
        ],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      });
      return;
    }
    if (method === 'POST' && pathname === '/api/ai-drafts') {
      await json(route, draft(), 201);
      return;
    }
    if (method === 'POST' && pathname === `/api/ai-drafts/${draftId}/accept`) {
      const body = request.postDataJSON() as { questions: { stem: string }[] };
      accepts.push(body);
      if (body.questions[0]?.stem === originalStem) {
        await json(route, { detail: 'Edit the draft before saving it as a quiz.' }, 400);
        return;
      }
      await json(route, { draftId, quizId, title: 'Draft: Senior backend engineer' }, 201);
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}
