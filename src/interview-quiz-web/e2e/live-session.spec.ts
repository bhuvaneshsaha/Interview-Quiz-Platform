import { expect, test, type Page, type Route } from '@playwright/test';
import { assignmentId, candidateAttempt, screenshotPath } from './fixtures';

type Phase = 'lobby' | 'running' | 'paused' | 'ended' | 'timeout';

test('host and candidate run a live session', async ({ browser }) => {
  const session = createSession();
  const host = await browser.newPage();
  const candidate = await browser.newPage();
  await installHost(host, session);
  await installCandidate(candidate, session);

  await candidate.goto(`/attempt?token=live-token`);
  await expect(candidate.getByTestId('live-lobby')).toBeVisible();
  await expect(candidate.getByText('Waiting for the interviewer to start.')).toBeVisible();
  await expect(candidate.getByRole('button', { name: 'Start quiz' })).toHaveCount(0);
  await candidate.screenshot({ path: screenshotPath('live-lobby.png'), fullPage: true });

  await host.goto(`/login?returnUrl=${encodeURIComponent(`/assignments/${assignmentId}/live`)}`);
  await host.getByLabel('Email').fill('recruiter.dev@example.com');
  await host.getByLabel('Password').fill('Dev.Recruiter!1');
  await host.getByRole('button', { name: 'Sign in' }).click();
  await expect(host.getByTestId('live-state')).toHaveText('Lobby');
  await expect(host.getByTestId('live-connected')).toContainText('Yes');
  await expect(host.getByText('candidate.dev@example.com')).toHaveCount(0);

  await host.getByTestId('live-start').click();
  await expect(host.getByTestId('live-state')).toHaveText('Running');
  await expect(candidate.getByRole('heading', { name: candidateAttempt.questions[0].stem })).toBeVisible({
    timeout: 8_000,
  });
  await expect(candidate.getByRole('button', { name: 'Start quiz' })).toHaveCount(0);
  await candidate.screenshot({ path: screenshotPath('live-running.png'), fullPage: true });

  await candidate.getByLabel('201 Created').check();
  await expect(host.getByTestId('live-answered')).toHaveText('1', { timeout: 8_000 });

  await host.getByTestId('live-pause').click();
  await expect(candidate.getByTestId('live-pause-banner')).toHaveText('Paused by interviewer', { timeout: 4_000 });
  const frozen = await candidate.getByRole('timer').innerText();
  await candidate.waitForTimeout(1200);
  await expect(candidate.getByRole('timer')).toHaveText(frozen);
  await candidate.screenshot({ path: screenshotPath('live-paused.png'), fullPage: true });

  await host.getByTestId('live-resume').click();
  await expect(candidate.getByTestId('live-pause-banner')).toHaveCount(0, { timeout: 4_000 });

  session.failCurrentOnce = true;
  const consumesBefore = session.consumeCount;
  await candidate.reload();
  await expect.poll(() => session.consumeCount, { timeout: 8_000 }).toBeGreaterThan(consumesBefore);
  await expect(candidate.getByRole('heading', { name: candidateAttempt.questions[0].stem })).toBeVisible();
  await expect(candidate.getByText('Reopen your link.')).toHaveCount(0);

  await host.getByTestId('live-end').click();
  await expect(host.getByRole('alertdialog')).toContainText('1 answered');
  await expect(host.getByRole('alertdialog')).toContainText('0 flagged');
  await host.getByTestId('confirm-end').click();
  await expect(candidate.getByText('The interviewer ended the session.')).toBeVisible({ timeout: 8_000 });
  await candidate.screenshot({ path: screenshotPath('live-ended.png'), fullPage: true });

  await host.close();
  await candidate.close();
});

test('auto-end and reconnecting recover without a reload', async ({ browser }) => {
  const session = createSession();
  session.phase = 'running';
  const host = await browser.newPage();
  const candidate = await browser.newPage();
  await installHost(host, session);
  await installCandidate(candidate, session);

  await host.goto(`/login?returnUrl=${encodeURIComponent(`/assignments/${assignmentId}/live`)}`);
  await host.getByLabel('Email').fill('recruiter.dev@example.com');
  await host.getByLabel('Password').fill('Dev.Recruiter!1');
  await host.getByRole('button', { name: 'Sign in' }).click();
  await expect(host.getByTestId('live-state')).toHaveText('Running');

  await candidate.goto(`/attempt?token=live-token`);
  await expect(candidate.getByRole('heading', { name: candidateAttempt.questions[0].stem })).toBeVisible();

  session.progressFailuresLeft = 2;
  await expect(host.getByTestId('live-reconnecting')).toBeVisible({ timeout: 12_000 });
  await expect(host.getByTestId('live-reconnecting')).toHaveCount(0, { timeout: 12_000 });
  await expect(host).toHaveURL(new RegExp(`/assignments/${assignmentId}/live`));

  session.phase = 'timeout';
  await expect(candidate.getByRole('heading', { name: 'Time ran out' })).toBeVisible({ timeout: 8_000 });
  await expect(host.getByTestId('live-state')).toHaveText('Submitted', { timeout: 8_000 });

  await host.close();
  await candidate.close();
});

interface LiveMock {
  phase: Phase;
  answered: number;
  flagged: number;
  index: number;
  remaining: number;
  progressFailuresLeft: number;
  failCurrentOnce: boolean;
  consumeCount: number;
}

function createSession(): LiveMock {
  return {
    phase: 'lobby',
    answered: 0,
    flagged: 0,
    index: 0,
    remaining: 1800,
    progressFailuresLeft: 0,
    failCurrentOnce: false,
    consumeCount: 0,
  };
}

function progressBody(session: LiveMock) {
  const ended = session.phase === 'ended' || session.phase === 'timeout';
  return {
    assignmentId,
    attemptId: session.phase === 'lobby' ? null : candidateAttempt.id,
    state: ended ? 'submitted' : session.phase,
    candidateConnected: true,
    lastActivityAtUtc: '2026-10-15T08:00:00.000Z',
    currentQuestionIndex: session.phase === 'lobby' ? null : session.index,
    questionCount: 2,
    answeredCount: session.answered,
    flaggedCount: session.flagged,
    remainingSeconds: ended ? null : session.remaining,
    totalPausedSeconds: session.phase === 'paused' ? 30 : 0,
    endReason: session.phase === 'ended' ? 'host' : session.phase === 'timeout' ? 'timeout' : null,
  };
}

function candidateBody(session: LiveMock) {
  const ended = session.phase === 'ended' || session.phase === 'timeout';
  return {
    ...candidateAttempt,
    status: ended ? 'submitted' : 'inProgress',
    remainingSeconds: ended ? 0 : session.remaining,
    liveState: ended ? 'ended' : session.phase === 'paused' ? 'paused' : 'running',
    paused: session.phase === 'paused',
    liveEndReason: session.phase === 'ended' ? 'host' : session.phase === 'timeout' ? 'timeout' : null,
    flaggedQuestionIds: session.flagged ? [candidateAttempt.questions[0].id] : [],
    answers: session.answered ? [{ questionId: 'q-single', value: { optionId: 'opt-201' } }] : [],
  };
}

async function installHost(page: Page, session: LiveMock): Promise<void> {
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
        permissions: ['sessions.live.run', 'assignments.read', 'assignments.write', 'attempts.read'],
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/assignments/${assignmentId}`) {
      await json(route, {
        id: assignmentId,
        snapshotTitle: 'Backend interview',
        snapshotQuestionCount: 2,
        mode: 'live',
        status: 'notStarted',
        candidateEmail: 'candidate.dev@example.com',
        overallDurationMinutes: 30,
      });
      return;
    }
    if (pathname.endsWith('/live/progress') && method === 'GET') {
      if (session.progressFailuresLeft > 0) {
        session.progressFailuresLeft -= 1;
        await json(route, { title: 'Unavailable' }, 503);
        return;
      }
      await json(route, progressBody(session));
      return;
    }
    if (pathname.endsWith('/live/start') && method === 'POST') {
      session.phase = 'running';
      await json(route, progressBody(session));
      return;
    }
    if (pathname.endsWith('/live/pause') && method === 'POST') {
      session.phase = 'paused';
      await json(route, progressBody(session));
      return;
    }
    if (pathname.endsWith('/live/resume') && method === 'POST') {
      session.phase = 'running';
      await json(route, progressBody(session));
      return;
    }
    if (pathname.endsWith('/live/end') && method === 'POST') {
      session.phase = 'ended';
      await json(route, progressBody(session));
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}

async function installCandidate(page: Page, session: LiveMock): Promise<void> {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();
    if (method === 'POST' && pathname.endsWith('/api/auth/magic-link/consume')) {
      session.consumeCount += 1;
      await json(route, {
        accessToken: `candidate-access-${session.consumeCount}`,
        accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
        tokenType: 'Bearer',
        assignmentId,
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/attempts/preview')) {
      const started = session.phase !== 'lobby';
      await json(route, {
        attemptStatus: started ? (session.phase === 'ended' || session.phase === 'timeout' ? 'submitted' : 'inProgress') : 'notStarted',
        quizTitle: 'Backend interview',
        openingName: 'Senior backend engineer',
        durationMinutes: 30,
        questionCount: 2,
        liveState: session.phase === 'lobby' ? 'waiting' : session.phase === 'paused' ? 'paused' : 'running',
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/attempts/current')) {
      if (session.failCurrentOnce) {
        session.failCurrentOnce = false;
        await json(route, { title: 'Unauthorized' }, 401);
        return;
      }
      await json(route, candidateBody(session));
      return;
    }
    if (method === 'PUT' && pathname.endsWith('/answers')) {
      session.answered = 1;
      await json(route, candidateBody(session));
      return;
    }
    if (method === 'PUT' && pathname.endsWith('/flags')) {
      await json(route, candidateBody(session));
      return;
    }
    if (method === 'PUT' && pathname.endsWith('/position')) {
      await json(route, {}, 204);
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: status === 204 ? '' : JSON.stringify(body),
  });
}
