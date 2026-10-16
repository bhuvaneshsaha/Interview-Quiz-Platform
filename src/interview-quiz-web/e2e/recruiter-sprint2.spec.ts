import { expect, test, type Page, type Route } from '@playwright/test';
import { screenshotPath } from './fixtures';

const openingId = '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001';
const quizId = '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001';
const recruiterId = '11111111-1111-1111-1111-111111111111';
const authorId = '22222222-2222-2222-2222-222222222222';

const candidateEmail = 'candidate.dev@example.com';

const users = [
  { id: recruiterId, name: 'Recruiter Dev', email: 'recruiter.dev@example.com', employee: true },
  { id: authorId, name: 'Author Dev', email: 'author.dev@example.com', employee: true },
  {
    id: '33333333-3333-3333-3333-333333333333',
    name: 'Candidate Dev',
    email: candidateEmail,
    employee: false,
  },
];

const opening = {
  id: openingId,
  title: 'Senior backend engineer',
  jobDescription: 'Build APIs.',
  owner: recruiterId,
  ownerName: 'Recruiter Dev',
  ownerEmail: 'recruiter.dev@example.com',
  ownerResolved: true,
  startDate: '2026-11-01',
  expectedCloseDate: null,
  headcount: 1,
  expectedExperienceYears: 5,
  handlers: [authorId],
  handlerPeople: [{ userId: authorId, name: 'Author Dev', email: 'author.dev@example.com', resolved: true }],
  tags: {},
  rowVersion: 1,
  createdAtUtc: '2026-10-01T00:00:00Z',
  updatedAtUtc: '2026-10-01T00:00:00Z',
};

const quiz = {
  id: quizId,
  openingId,
  title: 'Backend interview — working copy',
  description: 'Seeded quiz',
  expectedExperienceYears: 5,
  tags: {},
  questions: [
    {
      id: 'q-single',
      sortOrder: 0,
      type: 'multipleChoiceSingle',
      stem: 'Which HTTP status means a resource was created?',
      scoringMode: 'auto',
      creditMode: null,
      points: 1,
      sourceQuestionId: null,
      body: {
        options: [
          { id: 'opt-200', text: '200 OK', isCorrect: false },
          { id: 'opt-201', text: '201 Created', isCorrect: true },
        ],
      },
    },
  ],
  originTemplateId: null,
  sourceTemplateVersionId: null,
  rowVersion: 4,
  createdAtUtc: '2026-10-01T00:00:00Z',
  updatedAtUtc: '2026-10-01T00:00:00Z',
  assignmentCount: 2,
};

const preview = {
  title: quiz.title,
  openingName: opening.title,
  questionCount: 3,
  suggestedDurationMinutes: 10,
  rowVersion: quiz.rowVersion,
  questions: [
    {
      id: 'q-single',
      sortOrder: 0,
      type: 'multipleChoiceSingle',
      stem: quiz.questions[0].stem,
      body: {
        options: [
          { id: 'opt-200', text: '200 OK' },
          { id: 'opt-201', text: '201 Created' },
        ],
      },
      sourceQuestionId: null,
    },
    {
      id: 'q-shared',
      sortOrder: 1,
      type: 'dragDropSharedBank',
      stem: 'Match each HTTP method to what it typically does.',
      body: {
        slots: [
          { id: 'slot-get', label: 'GET' },
          { id: 'slot-post', label: 'POST' },
        ],
        bank: [
          { id: 'item-read', text: 'Read a resource' },
          { id: 'item-create', text: 'Create a resource' },
        ],
      },
      sourceQuestionId: null,
    },
    {
      id: 'q-per-slot',
      sortOrder: 2,
      type: 'dragDropPerSlot',
      stem: 'Match each status code.',
      body: {
        slots: [
          {
            id: 'slot-401',
            label: '401',
            options: [
              { id: 'opt-unauth', text: 'Unauthenticated' },
              { id: 'opt-forbid', text: 'Forbidden' },
            ],
          },
        ],
      },
      sourceQuestionId: null,
    },
  ],
};

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

async function installStaffApi(page: Page): Promise<{
  attemptWrites: number;
  quizWrites: number;
  previewReads: number;
  lastQuiz?: { title?: string; shuffleQuestions?: boolean; shuffleOptions?: boolean };
}> {
  const calls: {
    attemptWrites: number;
    quizWrites: number;
    previewReads: number;
    lastQuiz?: { title?: string; shuffleQuestions?: boolean; shuffleOptions?: boolean };
  } = { attemptWrites: 0, quizWrites: 0, previewReads: 0 };
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const pathname = url.pathname;
    const method = request.method();

    if (
      (method === 'POST' && pathname.endsWith('/api/auth/login')) ||
      (method === 'POST' && pathname.endsWith('/api/auth/refresh'))
    ) {
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
        id: recruiterId,
        email: 'recruiter.dev@example.com',
        permissions: [
          'quizzes.read',
          'quizzes.write',
          'openings.read',
          'openings.write',
          'assignments.read',
          'templates.read',
        ],
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/filters')) {
      await json(route, { items: [], page: 1, pageSize: 50, totalCount: 0 });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/users/assignable')) {
      const term = (url.searchParams.get('q') ?? '').toLowerCase();
      const matches = users.filter(
        (user) =>
          user.employee &&
          (!term ||
            user.email.includes(term) ||
            user.name.toLowerCase().includes(term) ||
            user.id === term),
      );
      await json(route, matches);
      return;
    }
    if (method === 'GET' && pathname === '/api/openings') {
      await json(route, { items: [opening], page: 1, pageSize: 20, totalCount: 1 });
      return;
    }
    if (method === 'GET' && pathname === `/api/openings/${openingId}`) {
      await json(route, opening);
      return;
    }
    if (method === 'GET' && pathname === '/api/quizzes') {
      await json(route, { items: [{ ...quiz, questions: quiz.questions }], page: 1, pageSize: 20, totalCount: 1 });
      return;
    }
    if (method === 'GET' && pathname === `/api/quizzes/${quizId}`) {
      await json(route, quiz);
      return;
    }
    if (method === 'PUT' && pathname === `/api/quizzes/${quizId}`) {
      calls.quizWrites += 1;
      const body = request.postDataJSON() as {
        title?: string;
        shuffleQuestions?: boolean;
        shuffleOptions?: boolean;
      };
      calls.lastQuiz = body;
      await json(route, { ...quiz, title: body.title ?? quiz.title, rowVersion: quiz.rowVersion + 1 });
      return;
    }
    if (method === 'GET' && pathname === `/api/quizzes/${quizId}/candidate-preview`) {
      calls.previewReads += 1;
      await json(route, preview);
      return;
    }
    if (method === 'POST' && pathname === '/api/quizzes/candidate-preview') {
      calls.previewReads += 1;
      const body = request.postDataJSON() as { title?: string };
      await json(route, { ...preview, title: body.title ?? preview.title });
      return;
    }
    if (method === 'POST' && pathname.includes('/candidate-preview/score')) {
      await json(route, {
        autoPointsAwarded: 0,
        autoPointsAvailable: 1,
        items: [{ questionId: 'q-single', status: 'scored', pointsAwarded: 0, points: 1 }],
      });
      return;
    }
    if (pathname.includes('/attempts') || pathname.includes('/assignments') && method !== 'GET') {
      calls.attemptWrites += 1;
    }
    if (method === 'GET' && pathname.startsWith('/api/assignments')) {
      await json(route, { items: [], page: 1, pageSize: 20, totalCount: 0 });
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
  return calls;
}

async function signIn(page: Page, returnUrl: string): Promise<void> {
  await page.goto(`/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  await page.getByLabel('Email').fill('recruiter.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Recruiter!1');
  await page.getByRole('button', { name: 'Sign in' }).click();
}

test('editing an assigned quiz warns and asks before save', async ({ page }) => {
  const calls = await installStaffApi(page);
  await signIn(page, '/quizzes');

  await expect(page.getByRole('heading', { name: 'Quizzes' })).toBeVisible();
  await page.getByRole('link', { name: 'Edit' }).click();
  await expect(page.getByText('2 assignments use earlier snapshots. Changes apply only to new assignments.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'View assignments' })).toBeVisible();
  await page.screenshot({ path: screenshotPath('after-quiz-edit-warning.png'), fullPage: true });

  await page.getByLabel('Title').fill('Backend interview — revised');
  await page.getByLabel('Shuffle questions').check();
  await page.getByTestId('save-quiz').click();
  await expect(page.getByRole('alertdialog')).toContainText('Save changes to an assigned quiz?');
  await expect(page.getByRole('alertdialog')).toContainText('Snapshots already assigned stay frozen.');
  expect(calls.quizWrites).toBe(0);
  await page.getByTestId('confirm-quiz-edit').click();
  await expect(page.getByLabel('Title')).toHaveValue('Backend interview — revised');
  await expect(page.getByLabel('Shuffle questions')).toBeChecked();
  await expect(page.getByLabel('Shuffle multiple-choice options')).not.toBeChecked();
  expect(calls.quizWrites).toBe(1);
  expect(calls.lastQuiz?.shuffleQuestions).toBe(true);
  expect(calls.lastQuiz?.shuffleOptions).toBe(false);
  expect(calls.attemptWrites).toBe(0);
});

test('candidate preview records nothing', async ({ page }) => {
  const calls = await installStaffApi(page);
  await signIn(page, `/quizzes/${quizId}`);

  await page.getByRole('button', { name: 'Preview as candidate' }).click();
  await expect(page).toHaveURL(new RegExp(`/quizzes/${quizId}/preview`));
  await expect(page.getByText('Preview, not recorded')).toBeVisible();
  await expect(page.locator('app-shell')).toHaveCount(0);
  await page.getByRole('button', { name: 'Start quiz' }).click();
  await expect(page.getByRole('timer')).toBeVisible();
  await page.getByRole('button', { name: 'Pause timer' }).click();
  await expect(page.getByRole('button', { name: 'Resume timer' })).toBeVisible();
  await page.getByLabel('201 Created').check();
  await page.getByRole('button', { name: 'Check answers' }).click();
  await expect(page.getByText('Auto-scored 0 of 1')).toBeVisible();
  await expect(page.getByText('isCorrect')).toHaveCount(0);
  expect(calls.previewReads).toBeGreaterThan(0);
  expect(calls.attemptWrites).toBe(0);
  expect(calls.quizWrites).toBe(0);
  await page.screenshot({ path: screenshotPath('after-candidate-preview.png'), fullPage: true });
});

test('candidate preview answers slot questions from dropdowns', async ({ page }) => {
  await installStaffApi(page);
  await signIn(page, `/quizzes/${quizId}`);

  await page.getByRole('button', { name: 'Preview as candidate' }).click();
  await page.getByRole('button', { name: 'Start quiz' }).click();
  await page.getByRole('button', { name: 'Next' }).click();

  await expect(page.getByText('Candidate preview')).toBeVisible();
  await expect(page.getByText('Match each slot to an item from the list.')).toBeVisible();
  await expect(page.getByLabel('GET')).toBeVisible();
  await expect(page.locator('.cdk-drop-list, .drag-chip, .drag-handle, .slot-drop')).toHaveCount(0);
  await expect(page.getByText('Drop an item here')).toHaveCount(0);
  await page.getByLabel('GET').selectOption({ label: 'Read a resource' });
  await expect(page.getByLabel('GET')).toHaveValue('item-read');

  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByText('Match each slot to one of its options.')).toBeVisible();
  await expect(page.locator('.cdk-drop-list, .drag-chip, .drag-handle, .slot-drop')).toHaveCount(0);
  await page.getByLabel('401').selectOption({ label: 'Unauthenticated' });
  await expect(page.getByLabel('401')).toHaveValue('opt-unauth');
  await page.screenshot({ path: screenshotPath('after-preview-slot-dropdowns.png'), fullPage: true });
});

test('unsaved quiz edits preview from the current form', async ({ page }) => {
  const calls = await installStaffApi(page);
  await signIn(page, `/quizzes/${quizId}`);
  await page.getByLabel('Title').fill('Unsaved preview title');
  await page.getByRole('button', { name: 'Preview as candidate' }).click();
  await expect(page).toHaveURL(/\/quiz-preview$/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Unsaved preview title');
  await expect(page.getByText('Preview, not recorded')).toBeVisible();
  expect(calls.quizWrites).toBe(0);
  expect(calls.attemptWrites).toBe(0);
});

test('opening owner and handler pickers omit candidate emails', async ({ page }) => {
  await installStaffApi(page);
  await signIn(page, '/openings/new');
  await expect(page.getByRole('heading', { name: 'Create opening' })).toBeVisible();

  const owner = page.getByLabel('Owner');
  await owner.click();
  const ownerList = page.locator('#owner-list');
  await expect(ownerList.getByRole('option', { name: /Recruiter Dev/ })).toBeVisible();
  await expect(ownerList).not.toContainText(candidateEmail);

  await owner.fill(candidateEmail);
  await expect(ownerList.getByText('No matching users.')).toBeVisible();
  await expect(ownerList).not.toContainText(candidateEmail);

  const handlers = page.getByLabel('Handlers');
  await handlers.click();
  const handlerList = page.locator('#handlers-list');
  await handlers.fill(candidateEmail);
  await expect(handlerList.getByText('No matching users.')).toBeVisible();
  await expect(handlerList).not.toContainText(candidateEmail);
});

test('opening owner and handlers are user pickers', async ({ page }) => {
  await installStaffApi(page);
  await signIn(page, '/openings');
  await expect(page.getByRole('cell', { name: 'Recruiter Dev' })).toBeVisible();

  await page.getByRole('link', { name: 'Create opening' }).click();
  const owner = page.getByLabel('Owner');
  await owner.click();
  await owner.pressSequentially('recruiter', { delay: 30 });
  await page.getByRole('option', { name: /Recruiter Dev/ }).click();
  await expect(page.getByRole('button', { name: 'Remove Recruiter Dev' })).toBeVisible();

  const handlers = page.getByLabel('Handlers');
  await handlers.click();
  await handlers.pressSequentially('author', { delay: 30 });
  await page.getByRole('option', { name: /Author Dev/ }).click();
  await expect(page.getByRole('button', { name: 'Remove Author Dev' })).toBeVisible();
  await page.screenshot({ path: screenshotPath('after-opening-pickers.png'), fullPage: true });

  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/openings');
  await expect(page.getByRole('link', { name: 'Senior backend engineer' })).toBeVisible();
  const overflows = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
  );
  expect(overflows).toBe(false);
});
