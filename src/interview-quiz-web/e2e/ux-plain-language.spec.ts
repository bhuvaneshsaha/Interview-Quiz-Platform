import { expect, test, type Page, type Route } from '@playwright/test';
import { assignmentId } from './fixtures';

const staffPermissions = [
  'questions.read',
  'questions.write',
  'assignments.read',
  'assignments.write',
  'attempts.read',
  'sessions.live.run',
  'reports.read',
  'openings.fields.manage',
  'roles.manage',
];

test('sign-in tells candidates to use their email link', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  await expect(page.getByText('Staff sign in here with email and password.')).toBeVisible();
  await expect(page.getByText('Candidates open the link in their email.')).toBeVisible();
  await expect(page.locator('code')).toHaveCount(0);
  await expect(page.getByText('/attempt')).toHaveCount(0);
});

test('home uses the same names as the menu and explains an empty account', async ({ page }) => {
  await install(page, []);
  await signIn(page, '/');
  await expect(
    page.getByText('Your account does not include any of these areas yet. Ask an administrator if you need access.'),
  ).toBeVisible();
  await expect(page.getByText(/slice 1/)).toHaveCount(0);
});

test('question editor uses everyday labels', async ({ page }) => {
  await install(page, staffPermissions);
  await signIn(page, '/');
  await expect(page.getByRole('link', { name: 'Opening fields' })).toHaveCount(2);
  await expect(page.getByRole('link', { name: 'Roles', exact: true })).toHaveCount(2);
  await expect(page.getByRole('link', { name: 'Field defaults' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: /permission groups/ })).toHaveCount(0);

  await page.getByRole('link', { name: 'Question bank' }).first().click();
  await page.getByRole('link', { name: 'Create question' }).click();
  await expect(page.getByLabel('Question text')).toBeVisible();
  await expect(page.getByLabel('Stem')).toHaveCount(0);

  await page.getByLabel('Type').selectOption({ label: 'Match from one list' });
  await expect(page.getByRole('group', { name: 'Choices' })).toBeVisible();
  await expect(page.getByLabel('Choice 1')).toBeVisible();
  await expect(page.getByText('Wrong answer')).toBeVisible();
  await expect(page.getByRole('option', { name: 'Scored automatically' })).toBeAttached();
  await expect(page.getByRole('option', { name: 'AI suggests a score' })).toBeAttached();
  await expect(page.getByRole('option', { name: 'A person scores this' })).toBeAttached();

  await page.getByLabel('Type').selectOption({ label: 'Multiple choice (multi)' });
  await expect(page.getByLabel('Credit')).toBeVisible();
  await expect(page.getByRole('option', { name: 'Partial credit' })).toBeAttached();

  await page.getByLabel('Type').selectOption({ label: 'Ordering' });
  await expect(page.getByLabel('Correct place (0 is the first)')).toHaveCount(2);
});

test('assignment and live session avoid snapshot and email jargon', async ({ page }) => {
  await install(page, staffPermissions);
  await signIn(page, `/assignments/${assignmentId}`);
  await expect(page.getByText('Version 4. This candidate keeps this version if the quiz is edited later.')).toBeVisible();
  await expect(page.getByText('The new link is not emailed.')).toBeVisible();
  await expect(page.getByText(/not comparable/)).toHaveCount(0);
  await expect(page.getByText('Snapshot')).toHaveCount(0);

  await page.getByRole('link', { name: 'Open live session' }).click();
  await expect(page.getByTestId('live-connected')).toHaveText('Not connected yet');
  await expect(page.getByText('not seen yet')).toHaveCount(0);
});

test('reporting hub speaks in plain language', async ({ page }) => {
  await install(page, staffPermissions);
  await signIn(page, '/reports');
  await expect(page.getByText('These reports cover the whole company for anyone who can open Reporting.')).toBeVisible();
  await expect(page.getByText(/reports\.read/)).toHaveCount(0);
  await page.getByRole('link', { name: /Funnel/ }).click();
  await expect(page.getByText("Don't combine results from different quiz versions.")).toBeVisible();
  await expect(page.getByText('How many people were assigned, started, submitted, finished, and still need a review.')).toBeVisible();
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
    const pathname = new URL(request.url()).pathname;
    const method = request.method();
    if (method === 'POST' && pathname.endsWith('/api/auth/login')) {
      await json(route, tokens);
      return;
    }
    if (method === 'POST' && pathname.endsWith('/api/auth/refresh')) {
      await json(route, tokens);
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/me')) {
      await json(route, {
        id: 'staff-1',
        email: 'recruiter.dev@example.com',
        permissions,
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/assignments/${assignmentId}`) {
      await json(route, assignment);
      return;
    }
    if (method === 'GET' && pathname === `/api/assignments/${assignmentId}/attempts`) {
      await json(route, { items: [], page: 1, pageSize: 20, totalCount: 0 });
      return;
    }
    if (method === 'GET' && pathname === `/api/assignments/${assignmentId}/live/progress`) {
      await json(route, {
        assignmentId,
        attemptId: null,
        state: 'lobby',
        candidateConnected: false,
        lastActivityAtUtc: null,
        currentQuestionIndex: null,
        questionCount: 2,
        answeredCount: 0,
        flaggedCount: 0,
        remainingSeconds: null,
        totalPausedSeconds: 0,
        endReason: null,
      });
      return;
    }
    if (method === 'GET' && pathname === '/api/reports/funnel') {
      await json(route, {
        defaultWindowApplied: false,
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totals: {
          openingTitle: '',
          quizTitle: '',
          assigned: 0,
          appeared: 0,
          submitted: 0,
          completed: 0,
          pendingReview: 0,
          appearedRate: 0,
          submittedRate: 0,
          completedRate: 0,
        },
        items: [],
      });
      return;
    }
    if (method === 'GET' && pathname === '/api/questions') {
      await json(route, { items: [], page: 1, pageSize: 20, totalCount: 0 });
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

const assignment = {
  id: assignmentId,
  openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  quizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
  snapshotId: '8f2b6c65-1a7c-4b4f-ad66-5e3b7f1f6001',
  snapshotTitle: 'Backend interview',
  snapshotQuestionCount: 3,
  snapshotQuizRowVersion: 4,
  candidateEmail: 'candidate.dev@example.com',
  mode: 'live',
  overallDurationMinutes: 30,
  attemptLimit: 1,
  status: 'notStarted',
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
  createdByUserId: 'user-1',
};

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}
