import { expect, test, type Page, type Route } from '@playwright/test';
import { assignmentId, attemptId } from './fixtures';

const assistId = 'q-ai';

const unsettled = {
  id: attemptId,
  assignmentId,
  openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  candidateEmail: 'candidate.dev@example.com',
  status: 'submitted',
  resultStatus: 'incomplete',
  startedAtUtc: '2026-10-15T08:00:00.000Z',
  submittedAtUtc: '2026-10-15T08:20:00.000Z',
  autoPointsAwarded: 0,
  autoPointsAvailable: 0,
  totalPointsAvailable: 10,
  finalPointsAwarded: null,
  aiSuggestionsEnabled: true,
  quizRowVersion: 4,
  items: [
    {
      questionId: assistId,
      sortOrder: 0,
      type: 'longText',
      scoringMode: 'aiAssist',
      points: 10,
      status: 'unsettled',
      pointsAwarded: null,
      stem: 'Where should a reviewer confirm an AI suggestion?',
      candidateAnswer: { text: 'On the review screen.' },
      body: {},
      answerKey: { guidance: 'On the review screen.' },
      reviewable: true,
      reviewedByUserId: null,
      reviewedAtUtc: null,
      reviewComment: null,
      suggestedPoints: null,
      suggestionFeedback: null,
      suggestedAtUtc: null,
      suggestedByUserId: null,
      suggestionModel: null,
    },
  ],
};

const feedback = 'Non-empty answer. Suggested half of the available points.';

function withSuggestion() {
  return {
    ...unsettled,
    items: [
      {
        ...unsettled.items[0],
        suggestedPoints: 5,
        suggestionFeedback: feedback,
        suggestedAtUtc: '2026-10-15T12:00:00.000Z',
        suggestedByUserId: 'reviewer-1',
        suggestionModel: 'fake',
      },
    ],
  };
}

function confirmed(pointsAwarded: number, comment: string | null) {
  const drafted = withSuggestion();
  return {
    ...drafted,
    resultStatus: 'complete',
    finalPointsAwarded: pointsAwarded,
    items: [
      {
        ...drafted.items[0],
        status: 'scored',
        pointsAwarded,
        reviewComment: comment,
        reviewedByUserId: 'reviewer-1',
        reviewedAtUtc: '2026-10-15T12:05:00.000Z',
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

test('reviewer confirms an AI suggestion and the draft stays visible', async ({ page }) => {
  const suggestions: string[] = [];
  const scores: { pointsAwarded: number; comment: string | null }[] = [];
  await install(page, suggestions, scores);
  const reviewUrl = `/assignments/${assignmentId}/attempts/${attemptId}/review`;
  await page.goto(`/login?returnUrl=${encodeURIComponent(reviewUrl)}`);
  await page.getByLabel('Email').fill('reviewer.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Reviewer!1');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Review attempt' })).toBeVisible();
  await page.getByTestId(`suggest-score-${assistId}`).click();
  await expect.poll(() => suggestions.length).toBe(1);
  await expect(page.getByTestId('suggestion-draft')).toContainText('Suggested 5 of 10.');
  await expect(page.getByTestId('suggestion-draft')).toContainText(feedback);
  await expect(page.getByText('model fake')).toBeVisible();

  await page.getByTestId(`use-suggestion-${assistId}`).click();
  await page.getByTestId(`save-score-${assistId}`).click();

  await expect.poll(() => scores.length).toBe(1);
  expect(scores[0]).toEqual({ pointsAwarded: 5, comment: feedback });
  await expect(page.getByTestId('final-score')).toContainText('5 / 10');
  await expect(page.getByTestId('suggestion-draft')).toContainText('Suggested 5 of 10.');
  await expect(page.getByText('Complete')).toBeVisible();
});

async function install(
  page: Page,
  suggestions: string[],
  scores: { pointsAwarded: number; comment: string | null }[],
): Promise<void> {
  let current: unknown = unsettled;
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
      await json(route, current);
      return;
    }
    if (method === 'POST' && pathname === `/api/attempts/${attemptId}/items/${assistId}/suggestion`) {
      suggestions.push(pathname);
      current = withSuggestion();
      await json(route, current);
      return;
    }
    if (method === 'POST' && pathname === `/api/attempts/${attemptId}/items/${assistId}/review`) {
      const body = request.postDataJSON() as { pointsAwarded: number; comment: string | null };
      scores.push(body);
      current = confirmed(body.pointsAwarded, body.comment);
      await json(route, current);
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}
