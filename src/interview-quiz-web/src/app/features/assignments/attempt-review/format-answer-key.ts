/** Readable answer key for reviewers. Candidate results never receive this payload. */

export function formatAnswerKey(type: string, body: unknown): string {
  if (!isRecord(body)) {
    return 'No keyed answer.';
  }

  switch (type) {
    case 'multipleChoiceSingle':
    case 'multipleChoiceMulti': {
      const correct = arrayOf(body['options'])
        .filter((option) => option['isCorrect'] === true)
        .map((option) => textOf(option['text']));
      return correct.length ? correct.join(', ') : 'No keyed answer.';
    }
    case 'trueFalse':
      if (typeof body['correct'] === 'boolean') {
        return body['correct'] ? 'True' : 'False';
      }
      return 'No keyed answer.';
    case 'shortText': {
      const answers = arrayOf(body['acceptableAnswers']).map((item) => textOf(item['text'] ?? item));
      const plain = Array.isArray(body['acceptableAnswers'])
        ? body['acceptableAnswers'].filter((item) => typeof item === 'string').map((item) => item.trim())
        : [];
      const labels = plain.length ? plain : answers.filter((item) => item.length > 0);
      if (!labels.length) {
        return guidanceOrNone(body);
      }
      return body['caseSensitive'] === true ? `${labels.join(', ')} (case sensitive)` : labels.join(', ');
    }
    case 'ordering': {
      const labels = arrayOf(body['items']).map((item) => textOf(item['text']));
      return labels.length ? labels.join(' → ') : 'No keyed answer.';
    }
    case 'dragDropSharedBank':
    case 'dragDropPerSlot':
      return formatSlots(body, type);
    default:
      return guidanceOrNone(body);
  }
}

function formatSlots(body: Record<string, unknown>, type: string): string {
  const bank = new Map<string, string>();
  for (const item of arrayOf(body['bank'])) {
    if (typeof item['id'] === 'string') {
      bank.set(item['id'], textOf(item['text']));
    }
  }
  const parts = arrayOf(body['slots']).map((slot) => {
    const prompt = textOf(slot['prompt'] ?? slot['label'] ?? slot['text']);
    if (type === 'dragDropSharedBank' && typeof slot['correctItemId'] === 'string') {
      return `${prompt || 'Slot'}: ${bank.get(slot['correctItemId']) ?? 'Keyed item'}`;
    }
    const correct = arrayOf(slot['options'])
      .filter((option) => option['isCorrect'] === true)
      .map((option) => textOf(option['text']));
    if (!correct.length) {
      return '';
    }
    return `${prompt || 'Slot'}: ${correct.join(', ')}`;
  }).filter((part) => part.length > 0);
  return parts.length ? parts.join('; ') : guidanceOrNone(body);
}

function guidanceOrNone(body: Record<string, unknown>): string {
  if (typeof body['guidance'] === 'string' && body['guidance'].trim()) {
    return `No keyed answer. Guidance: ${body['guidance'].trim()}`;
  }
  return 'No keyed answer.';
}

function arrayOf(value: unknown): Record<string, unknown>[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value.filter(isRecord);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function textOf(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}
