import { expect, test } from '@playwright/test';
import {
  assignmentId,
  candidateAttempt,
  expectNoRawIds,
  inProgressPreview,
  installCandidateApi,
  screenshotPath,
  welcomePreview,
} from './fixtures';

test('welcome page waits for Start quiz before the timer and questions', async ({ page }) => {
  const calls = await installCandidateApi(page, welcomePreview);

  await page.goto(`/attempt?token=opaque-token`);
  await expect(page.getByRole('heading', { name: 'Welcome' })).toBeVisible();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(welcomePreview.quizTitle);
  await expect(page.getByText(welcomePreview.openingName)).toBeVisible();
  await expect(page.getByText(`${welcomePreview.durationMinutes} minutes`)).toBeVisible();
  await expect(page.getByText(String(welcomePreview.questionCount), { exact: true })).toBeVisible();
  await expect(page.getByText('You have a single attempt.')).toBeVisible();
  await expect(page.getByText("Don't close this tab during the quiz.")).toBeVisible();
  await expect(page.getByRole('timer')).toHaveCount(0);
  await expect(page.getByText(candidateAttempt.questions[0].stem)).toHaveCount(0);
  expect(calls.start).toBe(0);
  expect(calls.preview).toBe(1);
  expect(calls.current).toBe(0);

  await page.screenshot({ path: screenshotPath('after-welcome.png'), fullPage: true });

  await page.getByRole('button', { name: 'Start quiz' }).click();
  await expect(page.getByRole('timer')).toBeVisible();
  await expect(page.getByText('Question 1 of 2')).toBeVisible();
  await expect(page.getByRole('heading', { name: candidateAttempt.questions[0].stem })).toBeVisible();
  await expect(page.getByText(candidateAttempt.questions[0].stem)).toBeVisible();
  await expect(page.getByText('Select one answer.')).toBeVisible();
  await expect(page.getByText('201 Created')).toBeVisible();
  await expect(page.getByText('200 OK')).toBeVisible();

  const main = await page.locator('#attempt-main').innerText();
  await expect(page.getByTestId('question-type-badge')).toHaveText('Multiple choice (single)');
  await expect(page.getByTestId('answered-count')).toBeVisible();
  expect(main).not.toContain('Ordering');
  expect(main).not.toMatch(/\bpts\b/);
  await expectNoRawIds(main);
  expect(calls.start).toBe(1);
  expect(calls.current).toBe(0);

  await page.screenshot({ path: screenshotPath('after-attempt-questions.png'), fullPage: true });
});

test('an in-progress refresh skips welcome and keeps the server timer', async ({ page }) => {
  const calls = await installCandidateApi(page, inProgressPreview);

  await page.goto(`/attempt?token=opaque-token`);
  await expect(page.getByRole('timer')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Start quiz' })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Welcome' })).toHaveCount(0);
  await expect(page.getByText(candidateAttempt.questions[0].stem)).toBeVisible();
  expect(calls.start).toBe(0);
  expect(calls.current).toBe(1);
  expect(calls.preview).toBe(1);
});

test('slot questions use dropdowns and have no drag handles or drop zones', async ({ page }) => {
  const slotAttempt = {
    ...candidateAttempt,
    questions: [
      {
        id: 'q-shared',
        sortOrder: 0,
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
        sortOrder: 1,
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
  await installCandidateApi(page, inProgressPreview, slotAttempt);
  await page.goto(`/attempt?token=opaque-token`);

  await expect(page.getByRole('heading', { name: slotAttempt.questions[0].stem })).toBeVisible();
  await expect(page.getByTestId('question-type-badge')).toHaveText('Match each slot');
  await expect(page.getByText('Match each slot to an item from the list.')).toBeVisible();
  await expect(page.getByLabel('GET')).toBeVisible();
  await expect(page.getByLabel('POST')).toBeVisible();
  await expect(page.locator('.cdk-drop-list, .drag-chip, .drag-handle, .slot-drop')).toHaveCount(0);
  await expect(page.getByText('Drop an item here')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /^Drag / })).toHaveCount(0);

  await page.getByLabel('GET').selectOption({ label: 'Read a resource' });
  await page.getByLabel('POST').selectOption({ label: 'Create a resource' });
  await expect(page.getByLabel('GET')).toHaveValue('item-read');
  await expect(page.getByLabel('POST')).toHaveValue('item-create');

  const shared = await page.locator('#attempt-main').innerText();
  await expectNoRawIds(shared);
  await page.screenshot({ path: screenshotPath('after-slot-dropdowns.png'), fullPage: true });

  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByRole('heading', { name: slotAttempt.questions[1].stem })).toBeVisible();
  await expect(page.getByTestId('question-type-badge')).toHaveText('Match each slot');
  await expect(page.getByText('Match each slot to one of its options.')).toBeVisible();
  await expect(page.locator('.cdk-drop-list, .drag-chip, .drag-handle, .slot-drop')).toHaveCount(0);
  await expect(page.getByText('Drop an option here')).toHaveCount(0);
  await page.getByLabel('401').selectOption({ label: 'Unauthenticated' });
  await expect(page.getByLabel('401')).toHaveValue('opt-unauth');
});

test('ordering can be dragged and reordered from the keyboard', async ({ page }) => {
  await installCandidateApi(page, inProgressPreview);
  await page.goto(`/attempt?token=opaque-token`);
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByRole('heading', { name: candidateAttempt.questions[1].stem })).toBeVisible();

  const texts = page.locator('.order-text');
  await expect(texts).toHaveText(['Authentication', 'Authorization', 'Endpoint']);
  await expect(page.getByText('Drag the items into order, or use Move up and Move down.')).toBeVisible();

  const handle = page.getByRole('button', { name: 'Drag Authentication' });
  await expect(handle).toBeVisible();
  await expect(handle).toHaveCSS('touch-action', 'none');

  await dragOrderingItem(page, 0, 1);
  await expect(texts).toHaveText(['Authorization', 'Authentication', 'Endpoint']);

  await page.getByRole('button', { name: 'Move Authentication up' }).click();
  await expect(texts).toHaveText(['Authentication', 'Authorization', 'Endpoint']);
  await page.getByRole('button', { name: 'Move Authentication down' }).click();
  await expect(texts).toHaveText(['Authorization', 'Authentication', 'Endpoint']);

  const main = await page.locator('#attempt-main').innerText();
  await expectNoRawIds(main);
  expect(main).not.toContain('pts');

  await page.screenshot({ path: screenshotPath('after-attempt-ordering.png'), fullPage: true });
});

test('one question at a time can be reviewed, jumped, and submitted with counts', async ({ page }) => {
  await installCandidateApi(page, inProgressPreview);
  await page.goto(`/attempt?token=opaque-token`);

  await expect(page.getByRole('heading', { name: candidateAttempt.questions[0].stem })).toBeVisible();
  await expect(page.getByText(candidateAttempt.questions[1].stem)).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Previous' })).toBeDisabled();
  await expect(page.getByText('Answered 1 of 2')).toBeVisible();

  await page.getByRole('button', { name: 'Mark for review', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Marked for review', exact: true })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  await page.reload();
  await expect(page.getByRole('button', { name: 'Marked for review', exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Question 2, answered' }).click();
  await expect(page.getByRole('heading', { name: candidateAttempt.questions[1].stem })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Next' })).toBeDisabled();
  await expect(page.getByText(candidateAttempt.questions[0].stem)).toHaveCount(0);

  await page.getByRole('button', { name: 'Submit quiz' }).click();
  const dialog = page.getByRole('alertdialog');
  await expect(dialog).toContainText("You won't be able to change answers");
  await expect(dialog).toContainText('1 unanswered');
  await expect(dialog).toContainText('1 marked for review');
  await page.screenshot({ path: screenshotPath('after-question-nav.png'), fullPage: true });
  await page.getByRole('button', { name: 'Cancel' }).click();
});

test('attempt navigation fits a phone width', async ({ page }) => {
  await installCandidateApi(page, inProgressPreview);
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto(`/attempt?token=opaque-token`);

  await expect(page.getByRole('button', { name: 'Questions' })).toBeVisible();
  await page.getByRole('button', { name: 'Questions' }).click();
  await expect(page.getByRole('button', { name: 'Question 2, answered' })).toBeVisible();
  await page.getByRole('button', { name: 'Submit quiz' }).scrollIntoViewIfNeeded();
  await expect(page.getByRole('button', { name: 'Submit quiz' })).toBeVisible();
  await expect(page.getByRole('timer')).toBeVisible();

  const overflows = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
  );
  expect(overflows).toBe(false);
  await page.screenshot({ path: screenshotPath('after-mobile-attempt.png'), fullPage: true });
});

test('candidate routes stay outside the employee shell', async ({ page }) => {
  await installCandidateApi(page, welcomePreview);
  await page.goto(`/attempt?token=opaque-token`);
  await expect(page.getByRole('button', { name: 'Start quiz' })).toBeVisible();
  await expect(page.locator('app-shell')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Sign out' })).toHaveCount(0);
  expect(page.url()).toContain('/attempt');
  expect(page.url()).toContain('token=');
  expect(page.url()).not.toContain(`/assignments/${assignmentId}`);
});

async function dragOrderingItem(
  page: import('@playwright/test').Page,
  from: number,
  to: number,
): Promise<void> {
  const rows = page.locator('.order-item');
  const handle = rows.nth(from).locator('.drag-handle');
  await rows.nth(to).scrollIntoViewIfNeeded();
  const source = await handle.boundingBox();
  const target = await rows.nth(to).boundingBox();
  if (!source || !target) {
    throw new Error('Ordering rows are not visible.');
  }
  const startX = source.x + source.width / 2;
  const startY = source.y + source.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX, startY + 12, { steps: 4 });
  await page.mouse.move(target.x + target.width / 2, target.y + target.height / 2, { steps: 20 });
  await page.mouse.up();
}
