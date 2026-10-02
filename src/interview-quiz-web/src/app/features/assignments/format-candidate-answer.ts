/** Render a recruiter attempt answer without dumping snapshot keys. */
export function formatCandidateAnswer(value: unknown): string {
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
  const record = value as Record<string, unknown>;
  if (typeof record['text'] === 'string') {
    return record['text'].trim() || 'No answer';
  }
  if (typeof record['value'] === 'boolean') {
    return record['value'] ? 'True' : 'False';
  }
  if (typeof record['optionId'] === 'string' && record['optionId']) {
    return record['optionId'];
  }
  if (Array.isArray(record['optionIds'])) {
    const ids = stringsOf(record['optionIds']);
    return ids.length ? ids.join(', ') : 'No answer';
  }
  if (Array.isArray(record['itemIds'])) {
    const ids = stringsOf(record['itemIds']);
    return ids.length ? ids.join(' → ') : 'No answer';
  }
  if (Array.isArray(record['slots'])) {
    const parts = record['slots']
      .map((slot) => formatSlot(slot))
      .filter((part) => part.length > 0);
    return parts.length ? parts.join('; ') : 'No answer';
  }
  return 'Answer recorded';
}

function formatSlot(slot: unknown): string {
  if (!slot || typeof slot !== 'object' || Array.isArray(slot)) {
    return '';
  }
  const record = slot as Record<string, unknown>;
  const slotId = typeof record['slotId'] === 'string' ? record['slotId'] : '';
  const chosen =
    typeof record['itemId'] === 'string'
      ? record['itemId']
      : typeof record['optionId'] === 'string'
        ? record['optionId']
        : '';
  if (!slotId && !chosen) {
    return '';
  }
  return chosen ? `${slotId}: ${chosen}` : slotId;
}

function stringsOf(values: unknown[]): string[] {
  return values.filter((value): value is string => typeof value === 'string' && value.length > 0);
}
