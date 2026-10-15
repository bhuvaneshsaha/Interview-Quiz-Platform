import { expect, test } from '@playwright/test';
import { candidateAttempt, inProgressPreview, screenshotPath } from './fixtures';

const assignmentId = candidateAttempt.assignmentId;

test('revoke ends an in-progress attempt and blocks further answers', async ({ page }) => {
  let currentReads = 0;
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();
    if (method === 'POST' && pathname.endsWith('/api/auth/magic-link/consume')) {
      await json(route, {
        accessToken: 'candidate-access-token',
        accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
        tokenType: 'Bearer',
        assignmentId,
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/attempts/preview')) {
      await json(route, inProgressPreview);
      return;
    }
    if (method === 'GET' && pathname.endsWith('/attempts/current')) {
      currentReads += 1;
      if (currentReads === 1) {
        await json(route, candidateAttempt);
        return;
      }
      await json(
        route,
        { title: 'Bad Request', status: 400, detail: 'This invite has been revoked.' },
        400,
      );
      return;
    }
    if (method === 'PUT' && pathname.endsWith('/answers')) {
      await json(
        route,
        { title: 'Bad Request', status: 400, detail: 'This invite has been revoked.' },
        400,
      );
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });

  await page.goto('/attempt?token=opaque-token');
  await expect(page.getByRole('heading', { name: candidateAttempt.questions[0].stem })).toBeVisible();
  await expect(page.getByRole('timer')).toBeVisible();
  await page.screenshot({ path: screenshotPath('revoke-mid-attempt-before.png'), fullPage: true });

  await expect(page.getByRole('heading', { name: 'This invite is no longer active' })).toBeVisible();
  await expect(page.getByText('this quiz has ended')).toBeVisible();
  await expect(page.getByText('You cannot keep answering.')).toBeVisible();
  await expect(page.getByRole('heading', { name: candidateAttempt.questions[0].stem })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Submit quiz' })).toHaveCount(0);
  await expect(page.getByRole('timer')).toHaveCount(0);
  await page.screenshot({ path: screenshotPath('revoke-mid-attempt-ended.png'), fullPage: true });
});

async function json(
  route: import('@playwright/test').Route,
  body: unknown,
  status = 200,
): Promise<void> {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}
