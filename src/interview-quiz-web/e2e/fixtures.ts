import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { expect, type Page, type Route } from '@playwright/test';

export const assignmentId = '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001';
export const attemptId = '9a3c7d76-2b8d-4c5e-be77-6f4c8a2a7001';

const screenshotDir = process.env.E2E_SCREENSHOT_DIR
  ? path.resolve(process.env.E2E_SCREENSHOT_DIR)
  : path.join(process.cwd(), 'e2e', 'screenshots');

export function screenshotPath(filename: string): string {
  mkdirSync(screenshotDir, { recursive: true });
  return path.join(screenshotDir, filename);
}

const tokens = {
  accessToken: 'candidate-access-token',
  accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
  tokenType: 'Bearer',
  assignmentId,
};

export const welcomePreview = {
  attemptStatus: 'notStarted',
  quizTitle: 'Backend interview — working copy',
  openingName: 'Senior backend engineer',
  durationMinutes: 30,
  questionCount: 8,
};

export const inProgressPreview = {
  ...welcomePreview,
  attemptStatus: 'inProgress',
};

export const candidateAttempt = {
  id: attemptId,
  assignmentId,
  status: 'inProgress',
  startedAtUtc: '2026-10-15T08:00:00.000Z',
  dueAtUtc: '2099-01-01T00:00:00.000Z',
  submittedAtUtc: null,
  remainingSeconds: 1800,
  questions: [
    {
      id: 'q-single',
      sortOrder: 0,
      type: 'multipleChoiceSingle',
      stem: 'Which HTTP status means a resource was created?',
      body: {
        options: [
          { id: 'opt-200', text: '200 OK' },
          { id: 'opt-201', text: '201 Created' },
          { id: 'opt-204', text: '204 No Content' },
        ],
      },
      sourceQuestionId: null,
    },
    {
      id: 'q-order',
      sortOrder: 1,
      type: 'ordering',
      stem: 'Order the request pipeline from first to last.',
      body: {
        items: [
          { id: 'item-authn', text: 'Authentication' },
          { id: 'item-authz', text: 'Authorization' },
          { id: 'item-endpoint', text: 'Endpoint' },
        ],
      },
      sourceQuestionId: null,
    },
  ],
  answers: [],
};

export const recruiterAttempt = {
  id: attemptId,
  assignmentId,
  openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
  candidateEmail: 'candidate.dev@example.com',
  status: 'submitted',
  resultStatus: 'complete',
  startedAtUtc: '2026-10-15T08:00:00.000Z',
  submittedAtUtc: '2026-10-15T08:20:00.000Z',
  autoPointsAwarded: 6,
  autoPointsAvailable: 6,
  totalPointsAvailable: 6,
  items: [
    {
      questionId: 'q-single',
      sortOrder: 0,
      type: 'multipleChoiceSingle',
      scoringMode: 'auto',
      points: 1,
      status: 'scored',
      pointsAwarded: 1,
      stem: 'Which HTTP status means a resource was created?',
      candidateAnswer: { optionId: 'opt-201' },
      body: {
        options: [
          { id: 'opt-200', text: '200 OK' },
          { id: 'opt-201', text: '201 Created' },
        ],
      },
    },
    {
      questionId: 'q-multi',
      sortOrder: 1,
      type: 'multipleChoiceMulti',
      scoringMode: 'auto',
      points: 2,
      status: 'scored',
      pointsAwarded: 2,
      stem: 'Which of these are ASP.NET Core hosting concerns?',
      candidateAnswer: { optionIds: ['opt-kestrel', 'opt-health'] },
      body: {
        options: [
          { id: 'opt-kestrel', text: 'Kestrel' },
          { id: 'opt-health', text: 'Health checks' },
        ],
      },
    },
    {
      questionId: 'q-shared',
      sortOrder: 2,
      type: 'dragDropSharedBank',
      scoringMode: 'auto',
      points: 3,
      status: 'scored',
      pointsAwarded: 3,
      stem: 'Match each HTTP method to what it typically does.',
      candidateAnswer: {
        slots: [
          { slotId: 'slot-get', itemId: 'item-read' },
          { slotId: 'slot-post', itemId: 'item-create' },
        ],
      },
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
    },
    {
      questionId: 'q-order',
      sortOrder: 3,
      type: 'ordering',
      scoringMode: 'auto',
      points: 3,
      status: 'scored',
      pointsAwarded: 3,
      stem: 'Order the request pipeline from first to last.',
      candidateAnswer: { itemIds: ['item-authn', 'item-authz', 'item-endpoint'] },
      body: {
        items: [
          { id: 'item-authn', text: 'Authentication' },
          { id: 'item-authz', text: 'Authorization' },
          { id: 'item-endpoint', text: 'Endpoint' },
        ],
      },
    },
  ],
};

export interface AttemptCalls {
  start: number;
  preview: number;
  current: number;
}

export async function installCandidateApi(
  page: Page,
  preview: typeof welcomePreview,
): Promise<AttemptCalls> {
  const calls: AttemptCalls = { start: 0, preview: 0, current: 0 };
  const flagged = new Set<string>();

  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();

    if (method === 'POST' && pathname.endsWith('/api/auth/magic-link/consume')) {
      await json(route, tokens);
      return;
    }
    if (method === 'GET' && pathname.endsWith('/attempts/preview')) {
      calls.preview += 1;
      await json(route, preview);
      return;
    }
    if (method === 'GET' && pathname.endsWith('/attempts/current')) {
      calls.current += 1;
      await json(route, { ...candidateAttempt, flaggedQuestionIds: [...flagged] });
      return;
    }
    if (method === 'POST' && /\/attempts$/.test(pathname)) {
      calls.start += 1;
      await json(route, candidateAttempt, 201);
      return;
    }
    if (method === 'PUT' && pathname.endsWith('/answers')) {
      await json(route, { ...candidateAttempt, flaggedQuestionIds: [...flagged] });
      return;
    }
    if (method === 'PUT' && pathname.endsWith('/flags')) {
      const body = request.postDataJSON() as { flags?: { questionId: string; flagged: boolean }[] };
      flagged.clear();
      for (const flag of body.flags ?? []) {
        if (flag.flagged) {
          flagged.add(flag.questionId);
        }
      }
      await json(route, { ...candidateAttempt, flaggedQuestionIds: [...flagged] });
      return;
    }
    if (method === 'PUT' && pathname.endsWith('/position')) {
      await json(route, {}, 204);
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });

  return calls;
}

export async function installRecruiterApi(page: Page): Promise<void> {
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
        permissions: ['attempts.read', 'assignments.read'],
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/attempts/${attemptId}`) {
      await json(route, recruiterAttempt);
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}

export async function expectNoRawIds(text: string): Promise<void> {
  expect(text).not.toMatch(/\bopt-[a-z0-9-]+\b/);
  expect(text).not.toMatch(/\bitem-[a-z0-9-]+\b/);
  expect(text).not.toMatch(/\bslot-[a-z0-9-]+\b/);
}

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}
