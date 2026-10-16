import { expect, test, type Page, type Route } from '@playwright/test';
import { screenshotPath } from './fixtures';

const templateId = '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001';
const versionId = '5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3101';
const authorId = '22222222-2222-2222-2222-222222222222';

const question = {
  id: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
  sortOrder: 0,
  type: 'dragDropSharedBank',
  stem: 'Match each method.',
  scoringMode: 'auto',
  creditMode: null,
  points: 2,
  sourceQuestionId: 'bbbbbbbb-bbbb-4ccc-8ddd-eeeeeeeeeeee',
  body: {
    slots: [{ id: 'slot-get', label: 'GET', correctItemId: 'item-read' }],
    bank: [
      { id: 'item-read', text: 'Read a resource', isDistractor: false },
      { id: 'item-extra', text: 'Unused', isDistractor: true },
    ],
  },
};

test('metadata reuses questions and the editor saves drag-and-drop slots', async ({ page }) => {
  let metadataBody: Record<string, unknown> | null = null;
  let contentBody: Record<string, unknown> | null = null;
  await installTemplateApi(page, (kind, body) => {
    if (kind === 'metadata') {
      metadataBody = body;
    } else {
      contentBody = body;
    }
  });

  await page.goto(`/login?returnUrl=${encodeURIComponent('/templates/' + templateId)}`);
  await page.getByLabel('Email').fill('author.dev@example.com');
  await page.getByLabel('Password').fill('Dev.Author!1');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Template', exact: true })).toBeVisible();
  await expect(page.getByTestId('version-author')).toHaveText('Ada Author');
  await expect(page.getByTestId('version-author')).not.toHaveText(authorId);
  await expect(page.getByText('reuses the same questions')).toBeVisible();
  await page.screenshot({ path: screenshotPath('template-author-name.png'), fullPage: true });

  await page.getByLabel('Change note').fill('Rename only');
  await page.getByRole('button', { name: 'Save metadata' }).click();
  await expect(page.getByText('Rename only')).toBeVisible();
  expect(metadataBody).toMatchObject({
    title: 'Backend interview template',
    changeNote: 'Rename only',
    rowVersion: 3,
  });
  expect(metadataBody).not.toHaveProperty('questions');

  await page.getByRole('link', { name: 'Edit template' }).click();
  await expect(page.getByRole('heading', { name: 'Edit template' })).toBeVisible();
  const slot = page.getByTestId('shared-slot-label');
  await expect(slot).toHaveValue('GET');
  await expect(page.getByRole('button', { name: 'Add slot' })).toBeVisible();
  await expect(page.getByLabel('Correct choice')).toBeVisible();
  await page.screenshot({ path: screenshotPath('template-slot-editor.png'), fullPage: true });

  await slot.fill('GET method');
  await page.getByLabel('Change note').fill('Retitle the slot');
  await page.getByRole('button', { name: 'Save new version' }).click();
  await expect(page.getByRole('heading', { name: 'Template', exact: true })).toBeVisible();

  expect(contentBody).toBeTruthy();
  const questions = (contentBody as { questions: Array<{ body: { slots: Array<{ label: string }> } }> }).questions;
  expect(questions[0].body.slots[0].label).toBe('GET method');
});

async function installTemplateApi(
  page: Page,
  onSave: (kind: 'metadata' | 'content', body: Record<string, unknown>) => void,
): Promise<void> {
  let template = {
    id: templateId,
    title: 'Backend interview template',
    description: 'Seeded template',
    expectedExperienceYears: 5,
    tags: { Role: 'Backend' },
    latestVersionId: versionId,
    latestVersionNumber: 1,
    originQuizId: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-02T00:00:00Z',
    rowVersion: 3,
  };
  let changeNote = 'Published from quiz';
  const version = () => ({
    id: versionId,
    templateId,
    versionNumber: template.latestVersionNumber,
    title: template.title,
    description: template.description,
    expectedExperienceYears: 5,
    tags: template.tags,
    publishedFromQuizId: template.originQuizId,
    publishedAtUtc: '2026-01-02T00:00:00Z',
    publishedByUserId: authorId,
    publishedByDisplayName: 'Ada Author',
    changeNote,
    questionCount: 1,
    questions: [question],
  });

  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    const method = request.method();
    if (method === 'POST' && pathname.endsWith('/api/auth/login')) {
      await json(route, {
        accessToken: 'author-access-token',
        refreshToken: 'author-refresh-token',
        accessTokenExpiresAt: '2099-01-01T00:00:00.000Z',
        refreshTokenExpiresAt: '2099-01-02T00:00:00.000Z',
        tokenType: 'Bearer',
      });
      return;
    }
    if (method === 'GET' && pathname.endsWith('/api/me')) {
      await json(route, {
        id: authorId,
        email: 'author.dev@example.com',
        permissions: ['templates.read', 'templates.write'],
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/templates/${templateId}/versions`) {
      const { questions: _questions, ...summary } = version();
      await json(route, {
        items: [summary],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      });
      return;
    }
    if (method === 'GET' && pathname === `/api/templates/${templateId}/versions/${versionId}`) {
      await json(route, version());
      return;
    }
    if (method === 'GET' && pathname === `/api/templates/${templateId}`) {
      await json(route, template);
      return;
    }
    if (method === 'PUT' && pathname === `/api/templates/${templateId}/metadata`) {
      const body = request.postDataJSON() as Record<string, unknown>;
      onSave('metadata', body);
      changeNote = String(body['changeNote'] ?? changeNote);
      template = {
        ...template,
        title: String(body['title'] ?? template.title),
        rowVersion: template.rowVersion + 1,
        latestVersionNumber: template.latestVersionNumber + 1,
      };
      await json(route, template);
      return;
    }
    if (method === 'PUT' && pathname === `/api/templates/${templateId}/content`) {
      const body = request.postDataJSON() as Record<string, unknown>;
      onSave('content', body);
      template = { ...template, rowVersion: template.rowVersion + 1, latestVersionNumber: template.latestVersionNumber + 1 };
      await json(route, template);
      return;
    }
    await json(route, { message: `Unmocked ${method} ${pathname}` }, 404);
  });
}

async function json(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}
