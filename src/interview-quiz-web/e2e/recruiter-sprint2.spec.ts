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

const bankQuestion = {
  id: '6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4001',
  title: 'Bank true or false',
  tags: {},
  expectedExperienceYears: 1,
  type: 'trueFalse',
  stem: 'A copied bank question appears on the new quiz.',
  scoringMode: 'auto',
  creditMode: null,
  points: 1,
  body: { correct: true },
  archivedAtUtc: null,
  rowVersion: 1,
  createdAtUtc: '2026-10-01T00:00:00Z',
  updatedAtUtc: '2026-10-01T00:00:00Z',
};

async function installStaffApi(page: Page): Promise<{
  attemptWrites: number;
  quizWrites: number;
  previewReads: number;
  quizCriteria: string[];
  lastAssignment?: { showScoreToCandidate?: boolean };
  lastQuiz?: { title?: string; shuffleQuestions?: boolean; shuffleOptions?: boolean };
}> {
  const calls: {
    attemptWrites: number;
    quizWrites: number;
    previewReads: number;
    quizCriteria: string[];
    lastAssignment?: { showScoreToCandidate?: boolean };
    lastQuiz?: { title?: string; shuffleQuestions?: boolean; shuffleOptions?: boolean };
  } = { attemptWrites: 0, quizWrites: 0, previewReads: 0, quizCriteria: [] };
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
          'assignments.write',
          'questions.read',
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
      calls.quizCriteria.push(url.searchParams.get('criteria') ?? '');
      await json(route, { items: [{ ...quiz, questions: quiz.questions }], page: 1, pageSize: 20, totalCount: 1 });
      return;
    }
    if (method === 'GET' && pathname === '/api/questions') {
      await json(route, { items: [bankQuestion], page: 1, pageSize: 20, totalCount: 1 });
      return;
    }
    if (method === 'POST' && pathname === '/api/assignments') {
      const body = request.postDataJSON() as { showScoreToCandidate?: boolean };
      calls.lastAssignment = body;
      await json(route, {
        id: '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001',
        openingId,
        quizId,
        snapshotId: '8f2b6c65-1a7c-4b4f-ad66-5e3b7f1f6001',
        snapshotTitle: quiz.title,
        snapshotQuestionCount: 1,
        snapshotQuizRowVersion: 1,
        candidateEmail: 'candidate.dev@example.com',
        mode: 'async',
        overallDurationMinutes: 30,
        attemptLimit: 1,
        showScoreToCandidate: Boolean(body.showScoreToCandidate),
        status: 'notStarted',
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
        createdByUserId: recruiterId,
        inviteUrl: 'http://localhost:4200/attempt?token=opaque',
      });
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

async function dragChipToSlot(page: Page, text: string, slotLabel: string): Promise<void> {
  const chip = page.locator('.drag-bank .drag-chip', { hasText: text });
  const slot = page.getByLabel(slotLabel);
  await chip.scrollIntoViewIfNeeded();
  const source = await chip.boundingBox();
  const target = await slot.boundingBox();
  if (!source || !target) {
    throw new Error(`Could not drag ${text} onto ${slotLabel}.`);
  }
  const startX = source.x + source.width / 2;
  const startY = source.y + source.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + 8, startY + 8, { steps: 4 });
  await page.mouse.move(target.x + target.width / 2, target.y + target.height / 2, { steps: 24 });
  await page.mouse.up();
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
  await expect(page.locator('select')).toHaveCount(0);
  await expect(page.locator('.drag-chip')).toHaveCount(2);
  await dragChipToSlot(page, 'Read a resource', 'GET');
  await expect(page.getByLabel('GET')).toContainText('Read a resource');

  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByText('Match each slot to one of its options.')).toBeVisible();
  await expect(page.locator('#attempt-main .drag-chip, #attempt-main .slot-drop')).toHaveCount(0);
  await page.getByLabel('401').selectOption({ label: 'Unauthenticated' });
  await expect(page.getByLabel('401')).toHaveValue('opt-unauth');
  await page.screenshot({ path: screenshotPath('after-preview-slot-modes.png'), fullPage: true });
});

test('exit preview returns to the quiz editor', async ({ page }) => {
  await installStaffApi(page);
  await signIn(page, `/quizzes/${quizId}`);
  await page.getByRole('button', { name: 'Preview as candidate' }).click();
  await expect(page).toHaveURL(new RegExp(`/quizzes/${quizId}/preview`));
  await page.getByTestId('exit-preview').click();
  await expect(page).toHaveURL(new RegExp(`/quizzes/${quizId}$`));

  await page.getByRole('button', { name: 'Preview as candidate' }).click();
  await expect(page.getByTestId('exit-preview')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page).toHaveURL(new RegExp(`/quizzes/${quizId}$`));
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

test('clearing a quiz keyword removes that criterion', async ({ page }) => {
  const calls = await installStaffApi(page);
  await signIn(page, '/quizzes');
  const keyword = page.locator('#quiz-keyword');
  await keyword.fill('backend');
  await page.getByRole('button', { name: 'Filter', exact: true }).click();
  await expect.poll(() => calls.quizCriteria.at(-1) ?? '').toContain('backend');

  await keyword.fill('');
  await expect.poll(() => calls.quizCriteria.at(-1) ?? '').not.toContain('backend');

  await keyword.fill('backend');
  await page.getByRole('button', { name: 'Filter', exact: true }).click();
  await page.getByRole('button', { name: 'Clear keyword' }).click();
  await expect.poll(() => calls.quizCriteria.at(-1) ?? '').not.toContain('backend');
});

test('create quiz can include a question from the bank', async ({ page }) => {
  await installStaffApi(page);
  await signIn(page, '/quizzes/new');
  await expect(page.getByTestId('include-from-bank')).toBeVisible();
  await page.getByRole('checkbox', { name: /Bank true or false/ }).check();
  await page.getByTestId('include-from-bank-submit').click();
  await expect(page.getByText('From question bank')).toBeVisible();
  await expect(page.getByLabel('Stem')).toHaveValue('A copied bank question appears on the new quiz.');
});

test('show score to candidate stays off unless the recruiter turns it on', async ({ page }) => {
  const calls = await installStaffApi(page);
  await signIn(page, '/assignments/new');
  await expect(page.getByTestId('show-score')).not.toBeChecked();
  await fillAssignment(page);
  await page.getByRole('button', { name: 'Create assignment' }).click();
  await expect.poll(() => calls.lastAssignment?.showScoreToCandidate).toBe(false);

  await page.goto('/assignments/new');
  await page.getByTestId('show-score').check();
  await fillAssignment(page);
  await page.getByRole('button', { name: 'Create assignment' }).click();
  await expect.poll(() => calls.lastAssignment?.showScoreToCandidate).toBe(true);
});

async function fillAssignment(page: Page): Promise<void> {
  await page.getByLabel('Opening', { exact: true }).selectOption({ label: opening.title });
  const quizSelect = page.getByLabel('Quiz', { exact: true });
  await expect(quizSelect.locator('option')).toHaveCount(2);
  await quizSelect.selectOption({ label: quiz.title });
  await page.getByLabel('Candidate email').fill(candidateEmail);
}
