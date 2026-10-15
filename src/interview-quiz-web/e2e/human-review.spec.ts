import { expect, test, type Page, type Route } from '@playwright/test';
import { assignmentId, attemptId, installRecruiterApi } from './fixtures';

const humanId = 'q-human';

const unsettled = {
  id: attemptId,
  assignmentId,
  openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  candidateEmail: 'candidate.dev@example.com',
  status: 'submitted',
  resultStatus: 'incomplete',
  startedAtUtc: '2026-10-15T08:00:00.000Z',
  submittedAtUtc: '2026-10-15T08:20:00.000Z',
  autoPointsAwarded: 1,
  autoPointsAvailable: 1,
  totalPointsAvailable: 6,
  finalPointsAwarded: null,
  quizRowVersion: 4,
  items: [
    {
      questionId: 'q-auto',
      sortOrder: 0,
      type: 'trueFalse',
      scoringMode: 'auto',
      points: 1,
      status: 'scored',
      pointsAwarded: 1,
      stem: 'A quiz belongs to exactly one opening.',
      candidateAnswer: { value: true },
      body: {},
      answerKey: { correct: true },
      reviewable: false,
    },
    {
      questionId: humanId,
      sortOrder: 1,
      type: 'longText',
      scoringMode: 'humanOnly',
      points: 5,
      status: 'unsettled',
      pointsAwarded: null,
      stem: 'Describe how a snapshot freezes the quiz.',
      candidateAnswer: { text: 'The assign step copies the quiz.' },
      body: {},
      answerKey: { guidance: 'Mention the frozen copy.' },
      reviewable: true,
      reviewedByUserId: null,
      reviewedAtUtc: null,
      reviewComment: null,
    },
  ],
};

function completed(pointsAwarded: number, comment: string | null) {
  return {
    ...unsettled,
    resultStatus: 'complete',
    finalPointsAwarded: 1 + pointsAwarded,
    items: unsettled.items.map((item) =>
      item.questionId === humanId
        ? {
            ...item,
            status: 'scored',
            pointsAwarded,
            reviewComment: comment,
            reviewedByUserId: 'reviewer-1',
          }
        : item,
    ),
  };
}

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

async function installReviewer(page: Page, posts: { pointsAwarded: number; comment: string | null }[]): Promise<void> {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();
    if (method === 'POST' && pathname.endsWith('/api/auth/login')) {
      await json(route, {
        accessToken: 'reviewer-access-token',
        refreshToken: 'reviewer-refresh-token',
        accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
        refreshTokenExpiresAt: '2099-01-02T00:00:00.000Z',
        tokenType: 'Bearer',
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/me')) {
      await json(route, {
        id: 'reviewer-1',
        email: 'reviewer.dev@example.com',
        permissions: ['attempts.review', 'attempts.read', 'assignments.read'],
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/attempts/${attemptId}/review`) {
      await json(route, unsettled);
      return;
    }
    if (method === 'POST' && pathname === `/api/attempts/${attemptId}/items/${humanId}/review`) {
      const body = request.postDataJSON() as { pointsAwarded: number; comment: string | null };
      posts.push(body);
      await json(route, completed(body.pointsAwarded, body.comment));
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}

async function signIn(page: Page, email: string, returnUrl: string): Promise<void> {
  await page.goto(`/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill('Dev.Reviewer!1');
  await page.getByRole('button', { name: 'Sign in' }).click();
}

test('reviewer scores an unsettled item and sees the final score', async ({ page }) => {
  const posts: { pointsAwarded: number; comment: string | null }[] = [];
  await installReviewer(page, posts);
  const reviewUrl = `/assignments/${assignmentId}/attempts/${attemptId}/review`;
  await signIn(page, 'reviewer.dev@example.com', reviewUrl);

  await expect(page.getByRole('heading', { name: 'Review attempt' })).toBeVisible();
  await expect(page.getByTestId(`suggest-score-${humanId}`)).toHaveCount(0);
  await expect(page.getByText('Describe how a snapshot freezes the quiz.')).toBeVisible();
  await expect(page.getByText('No keyed answer. Guidance: Mention the frozen copy.')).toBeVisible();
  await expect(page.getByTestId('final-score')).toContainText('Not finalised');

  await page.getByTestId(`review-points-${humanId}`).fill('4');
  await page.getByLabel('Comment (optional)').fill('Covers the copy.');
  await page.getByTestId(`save-score-${humanId}`).click();

  await expect.poll(() => posts.length).toBe(1);
  expect(posts[0]).toEqual({ pointsAwarded: 4, comment: 'Covers the copy.' });
  await expect(page.getByTestId('final-score')).toContainText('5 / 6');
  await expect(page.getByText('Complete')).toBeVisible();
});

test('recruiter results omit the review action and answer keys', async ({ page }) => {
  await installRecruiterApi(page);
  const detail = `/assignments/${assignmentId}/attempts/${attemptId}`;
  await page.goto(`/login?returnUrl=${encodeURIComponent(detail)}`);
  await page.getByLabel('Email').fill('recruiter.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Recruiter!1');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Attempt' })).toBeVisible();
  await expect(page.getByTestId('open-review')).toHaveCount(0);
  await expect(page.getByText('answerKey')).toHaveCount(0);
});
