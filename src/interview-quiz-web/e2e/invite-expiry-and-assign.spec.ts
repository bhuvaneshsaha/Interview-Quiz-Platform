import { expect, test, type Page, type Route } from '@playwright/test';
import { screenshotPath } from './fixtures';

const openingId = '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001';
const templateId = '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001';
const versionId = '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3101';
const assignmentId = '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001';

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

test('expired and revoked invites show a clear page', async ({ page }) => {
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    if (route.request().method() === 'POST' && pathname.endsWith('/api/auth/magic-link/consume')) {
      const body = route.request().postDataJSON() as { token?: string };
      const expired = body.token === 'expired-token';
      await json(
        route,
        {
          title: 'Bad Request',
          status: 400,
          detail: expired ? 'This invite has expired.' : 'This invite has been revoked.',
        },
        400,
      );
      return;
    }
    await json(route, { message: 'unmocked' }, 404);
  });

  await page.goto('/attempt?token=expired-token');
  await expect(page.getByRole('heading', { name: 'This invite has expired' })).toBeVisible();
  await expect(page.getByText('extend the invite or send a new one')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Start quiz' })).toHaveCount(0);
  await page.screenshot({ path: screenshotPath('invite-expired.png'), fullPage: true });

  await page.goto('/attempt?token=revoked-token');
  await expect(page.getByRole('heading', { name: 'This invite is no longer active' })).toBeVisible();
  await expect(page.getByText('This link was revoked')).toBeVisible();
  await page.screenshot({ path: screenshotPath('invite-revoked.png'), fullPage: true });
});

test('recruiter assigns a candidate from a template', async ({ page }) => {
  let fromTemplate: Record<string, unknown> | null = null;
  await installRecruiter(page, (body) => {
    fromTemplate = body;
  });
  await page.goto('/login?returnUrl=%2Fassignments%2Fnew');
  await page.getByLabel('Email').fill('recruiter.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Recruiter!1');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Create assignment' })).toBeVisible();
  await page.getByTestId('source-template').check();
  await page.getByLabel('Opening').selectOption(openingId);
  await page.getByTestId('template-id').selectOption(templateId);
  await page.getByLabel('Template version').selectOption(versionId);
  await page.getByLabel('Candidate email').fill('candidate.dev@example.com');
  await page.screenshot({ path: screenshotPath('assign-from-template.png'), fullPage: true });
  await page.getByRole('button', { name: 'Create assignment' }).click();

  await expect(page.getByText('Assignment created')).toBeVisible();
  await expect(page.getByTestId('invite-email-status')).toContainText('sent');
  await expect(page.getByTestId('invite-url')).toHaveValue(/attempt\?token=/);
  expect(fromTemplate).toMatchObject({
    openingId,
    templateId,
    templateVersionId: versionId,
    candidateEmail: 'candidate.dev@example.com',
    mode: 'async',
  });
  await page.screenshot({ path: screenshotPath('assign-from-template-created.png'), fullPage: true });
});

async function installRecruiter(page: Page, onFromTemplate: (body: Record<string, unknown>) => void): Promise<void> {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();
    if (method === 'POST' && pathname.endsWith('/api/auth/login')) {
      await json(route, {
        accessToken: 'recruiter-access-token',
        refreshToken: 'recruiter-refresh-token',
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
        permissions: ['assignments.write', 'assignments.read', 'openings.read', 'templates.read', 'quizzes.read'],
      });
      return;
    }
    if (method === 'GET' && pathname === '/api/openings') {
      await json(route, {
        items: [{ id: openingId, title: 'Senior backend engineer' }],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      });
      return;
    }
    if (method === 'GET' && pathname === '/api/quizzes') {
      await json(route, { items: [], page: 1, pageSize: 100, totalCount: 0 });
      return;
    }
    if (method === 'GET' && pathname === '/api/templates') {
      await json(route, {
        items: [
          {
            id: templateId,
            title: 'Backend interview template',
            description: '',
            expectedExperienceYears: 5,
            tags: {},
            latestVersionId: versionId,
            latestVersionNumber: 1,
            originQuizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
            updatedAtUtc: '2026-01-01T00:00:00Z',
            rowVersion: 1,
          },
        ],
        page: 1,
        pageSize: 100,
        totalCount: 1,
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
            title: 'Backend interview template',
            publishedFromQuizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
            publishedAtUtc: '2026-01-01T00:00:00Z',
            questionCount: 3,
          },
        ],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      });
      return;
    }
    if (method === 'POST' && pathname === '/api/assignments/from-template') {
      const body = request.postDataJSON() as Record<string, unknown>;
      onFromTemplate(body);
      await json(
        route,
        {
          id: assignmentId,
          openingId,
          quizId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
          snapshotId: '8f2b6c65-1a7c-4b4f-ad66-5e3b7f1f6001',
          snapshotTitle: 'Backend interview template',
          snapshotQuestionCount: 3,
          snapshotQuizRowVersion: 1,
          candidateEmail: 'candidate.dev@example.com',
          mode: 'async',
          overallDurationMinutes: 30,
          attemptLimit: 1,
          status: 'notStarted',
          createdAtUtc: '2026-10-15T00:00:00Z',
          updatedAtUtc: '2026-10-15T00:00:00Z',
          createdByUserId: 'recruiter-1',
          inviteUrl: 'http://localhost:4200/attempt?token=from-template',
          inviteEmailStatus: 'sent',
          inviteEmailAttemptCount: 1,
          inviteState: 'active',
        },
        201,
      );
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}
