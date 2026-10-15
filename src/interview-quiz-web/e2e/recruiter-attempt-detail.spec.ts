import { expect, test } from '@playwright/test';
import { assignmentId, attemptId, expectNoRawIds, installRecruiterApi, screenshotPath } from './fixtures';

test('recruiter attempt detail shows option text instead of ids', async ({ page }) => {
  await installRecruiterApi(page);
  const detail = `/assignments/${assignmentId}/attempts/${attemptId}`;

  await page.goto(`/login?returnUrl=${encodeURIComponent(detail)}`);
  await page.getByLabel('Email').fill('recruiter.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Recruiter!1');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Attempt' })).toBeVisible();
  await expect(page.getByText('201 Created')).toBeVisible();
  await expect(page.getByText('Kestrel, Health checks')).toBeVisible();
  await expect(page.getByText('GET: Read a resource; POST: Create a resource')).toBeVisible();
  await expect(page.getByText('Authentication → Authorization → Endpoint')).toBeVisible();
  await expect(page.getByText('Which HTTP status means a resource was created?')).toBeVisible();

  const body = await page.locator('main, .page-heading').locator('..').innerText().catch(async () => page.locator('body').innerText());
  const text = await page.locator('body').innerText();
  await expectNoRawIds(text);
  expect(text).toContain('1 / 1 points');
  expect(text).toContain('Multiple choice (single)');
  expect(body.length).toBeGreaterThan(0);

  await page.screenshot({ path: screenshotPath('after-recruiter-answers.png'), fullPage: true });
});
