/** Render a recruiter attempt answer using snapshot labels. Never show raw ids. */

const UNTITLED = '(untitled option)';

export function formatCandidateAnswer(value: unknown, body?: unknown): string {
  if (value == null) {
    return 'No answer';
  }
  if (typeof value === 'string') {
    return value.trim() || 'No answer';
  }
  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }
  if (typeof value !== 'object' || Array.isArray(value)) {
    return 'Answer recorded';
  }
  const labels = labelBook(body);
  const record = value as Record<string, unknown>;
  if (typeof record['text'] === 'string') {
    return record['text'].trim() || 'No answer';
  }
  if (typeof record['value'] === 'boolean') {
    return record['value'] ? 'True' : 'False';
  }
  if (typeof record['optionId'] === 'string' && record['optionId']) {
    return labels.option(record['optionId']);
  }
  if (Array.isArray(record['optionIds'])) {
    const ids = stringsOf(record['optionIds']);
    return ids.length ? ids.map((id) => labels.option(id)).join(', ') : 'No answer';
  }
  if (Array.isArray(record['itemIds'])) {
    const ids = stringsOf(record['itemIds']);
    return ids.length ? ids.map((id) => labels.item(id)).join(' → ') : 'No answer';
  }
  if (Array.isArray(record['slots'])) {
    const parts = record['slots']
      .map((slot) => formatSlot(slot, labels))
      .filter((part) => part.length > 0);
    return parts.length ? parts.join('; ') : 'No answer';
  }
  return 'Answer recorded';
}

interface LabelBook {
  option: (id: string) => string;
  item: (id: string) => string;
  slot: (id: string) => string;
}

function labelBook(body: unknown): LabelBook {
  const root = asObject(body);
  const options = new Map<string, string>();
  const items = new Map<string, string>();
  const slots = new Map<string, string>();

  for (const entry of asArray(root['options'])) {
    remember(options, entry, 'text');
  }
  for (const entry of asArray(root['bank'])) {
    remember(items, entry, 'text');
  }
  for (const entry of asArray(root['items'])) {
    remember(items, entry, 'text');
  }
  for (const entry of asArray(root['slots'])) {
    const slot = asObject(entry);
    const slotId = asString(slot['id']);
    if (slotId) {
      slots.set(slotId, display(slot['label']));
    }
    for (const option of asArray(slot['options'])) {
      remember(options, option, 'text');
    }
  }

  return {
    option: (id) => options.get(id) ?? UNTITLED,
    item: (id) => items.get(id) ?? options.get(id) ?? UNTITLED,
    slot: (id) => (id ? (slots.get(id) ?? UNTITLED) : UNTITLED),
  };
}

function formatSlot(slot: unknown, labels: LabelBook): string {
  if (!slot || typeof slot !== 'object' || Array.isArray(slot)) {
    return '';
  }
  const record = slot as Record<string, unknown>;
  const slotId = typeof record['slotId'] === 'string' ? record['slotId'] : '';
  const itemId = typeof record['itemId'] === 'string' ? record['itemId'] : '';
  const optionId = typeof record['optionId'] === 'string' ? record['optionId'] : '';
  const chosen = itemId ? labels.item(itemId) : optionId ? labels.option(optionId) : '';
  if (!slotId && !chosen) {
    return '';
  }
  const slotLabel = labels.slot(slotId);
  return chosen ? `${slotLabel}: ${chosen}` : slotLabel;
}

function remember(target: Map<string, string>, entry: unknown, textKey: string): void {
  const record = asObject(entry);
  const id = asString(record['id']);
  if (!id) {
    return;
  }
  target.set(id, display(record[textKey]));
}

function display(value: unknown): string {
  const text = typeof value === 'string' ? value.trim() : '';
  return text || UNTITLED;
}

function asObject(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function asArray(value: unknown): unknown[] {
  return Array.isArray(value) ? value : [];
}

function asString(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function stringsOf(values: unknown[]): string[] {
  return values.filter((value): value is string => typeof value === 'string' && value.length > 0);
}
