import {
  FilterTarget,
  ListCriteria,
  OpeningListCriteria,
  QuizListCriteria,
  ReportFilterChip,
  ReportFilterCriteria,
  ReportFilterValue,
  TemplateListCriteria,
} from '../../core/api/contracts';
import { compactCriteria } from '../../core/api/list-query';

export { compactCriteria } from '../../core/api/list-query';

export function openingCriteriaFromUnknown(raw: unknown): OpeningListCriteria {
  const obj = asRecord(raw);
  return compactCriteria({
    owner: optionalString(obj['owner']),
    experienceMinYears: optionalInt(obj['experienceMinYears']),
    experienceMaxYears: optionalInt(obj['experienceMaxYears']),
    startDateFrom: optionalString(obj['startDateFrom']),
    startDateTo: optionalString(obj['startDateTo']),
    expectedCloseDateFrom: optionalString(obj['expectedCloseDateFrom']),
    expectedCloseDateTo: optionalString(obj['expectedCloseDateTo']),
    tags: optionalTags(obj['tags']),
  }) as OpeningListCriteria;
}

export function quizCriteriaFromUnknown(raw: unknown): QuizListCriteria {
  const obj = asRecord(raw);
  return compactCriteria({
    openingId: optionalString(obj['openingId']),
    keyword: optionalString(obj['keyword']),
    experienceMinYears: optionalInt(obj['experienceMinYears']),
    experienceMaxYears: optionalInt(obj['experienceMaxYears']),
    tags: optionalTags(obj['tags']),
  }) as QuizListCriteria;
}

export function templateCriteriaFromUnknown(raw: unknown): TemplateListCriteria {
  const obj = asRecord(raw);
  return compactCriteria({
    keyword: optionalString(obj['keyword']),
    experienceMinYears: optionalInt(obj['experienceMinYears']),
    experienceMaxYears: optionalInt(obj['experienceMaxYears']),
    tags: optionalTags(obj['tags']),
  }) as TemplateListCriteria;
}

export function reportCriteriaFromUnknown(raw: unknown): ReportFilterCriteria {
  const obj = asRecord(raw);
  const filters = Array.isArray(obj['filters']) ? obj['filters'].flatMap(chipFromUnknown) : [];
  return filters.length > 0 ? { filters } : {};
}

export function criteriaFromUnknown(target: FilterTarget, raw: unknown): ListCriteria {
  switch (target) {
    case 'openings':
      return openingCriteriaFromUnknown(raw);
    case 'quizzes':
      return quizCriteriaFromUnknown(raw);
    case 'templates':
      return templateCriteriaFromUnknown(raw);
    case 'reportsFunnel':
    case 'reportsScores':
    case 'reportsTime':
    case 'reportsQuestions':
    case 'reportsCandidates':
    case 'reportsPendingReview':
      return reportCriteriaFromUnknown(raw);
  }
}

function chipFromUnknown(raw: unknown): ReportFilterChip[] {
  const obj = asRecord(raw);
  const field = optionalString(obj['field']);
  const operator = optionalString(obj['operator']);
  if (!field || !operator || !Object.prototype.hasOwnProperty.call(obj, 'value')) {
    return [];
  }
  const value = chipValue(obj['value']);
  return value === undefined ? [] : [{ field, operator, value }];
}

function chipValue(value: unknown): ReportFilterValue | undefined {
  if (typeof value === 'string' || typeof value === 'number') {
    return value;
  }
  if (Array.isArray(value) && value.every((item) => typeof item === 'string' || typeof item === 'number')) {
    return value as string[] | number[];
  }
  if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
    const record = value as Record<string, unknown>;
    const from = record['from'];
    const to = record['to'];
    if (
      (typeof from === 'string' || typeof from === 'number') &&
      (typeof to === 'string' || typeof to === 'number')
    ) {
      return { from, to };
    }
  }
  return undefined;
}

export function tagsFromPair(key: string, value: string): Record<string, string> | undefined {
  const trimmedKey = key.trim();
  const trimmedValue = value.trim();
  if (!trimmedKey && !trimmedValue) {
    return undefined;
  }
  if (!trimmedKey || !trimmedValue) {
    return undefined;
  }
  return { [trimmedKey]: trimmedValue };
}

export function firstTagPair(tags: Record<string, string> | undefined): { key: string; value: string } {
  const entries = Object.entries(tags ?? {});
  if (entries.length === 0) {
    return { key: '', value: '' };
  }
  return { key: entries[0][0], value: entries[0][1] };
}

/** Visible key/value pair plus any extra tags from the last applied criteria. */
export function tagsForListForm(
  key: string,
  value: string,
  applied: Record<string, string> | undefined,
): Record<string, string> | undefined {
  const pair = tagsFromPair(key, value);
  if (!pair) {
    if (key.trim() || value.trim()) {
      return undefined;
    }
    return applied && Object.keys(applied).length > 0 ? applied : undefined;
  }

  const result = { ...(applied ?? {}) };
  const first = firstTagPair(applied);
  const nextKey = Object.keys(pair)[0];
  if (first.key && first.key !== nextKey) {
    delete result[first.key];
  }
  Object.assign(result, pair);
  return Object.keys(result).length > 0 ? result : undefined;
}

export function optionalIntFromInput(raw: string): number | undefined {
  const trimmed = raw.trim();
  if (!trimmed) {
    return undefined;
  }
  const value = Number(trimmed);
  return Number.isInteger(value) ? value : undefined;
}

function asRecord(raw: unknown): Record<string, unknown> {
  if (raw !== null && typeof raw === 'object' && !Array.isArray(raw)) {
    return raw as Record<string, unknown>;
  }
  return {};
}

function optionalString(value: unknown): string | undefined {
  if (typeof value !== 'string') {
    return undefined;
  }
  const trimmed = value.trim();
  return trimmed ? trimmed : undefined;
}

function optionalInt(value: unknown): number | undefined {
  if (typeof value === 'number' && Number.isInteger(value)) {
    return value;
  }
  if (typeof value === 'string') {
    return optionalIntFromInput(value);
  }
  return undefined;
}

function optionalTags(value: unknown): Record<string, string> | undefined {
  if (value === null || value === undefined || typeof value !== 'object' || Array.isArray(value)) {
    return undefined;
  }
  const result: Record<string, string> = {};
  for (const [key, raw] of Object.entries(value as Record<string, unknown>)) {
    const trimmedKey = key.trim();
    if (!trimmedKey || typeof raw !== 'string') {
      continue;
    }
    const trimmedValue = raw.trim();
    if (!trimmedValue) {
      continue;
    }
    result[trimmedKey] = trimmedValue;
  }
  return Object.keys(result).length > 0 ? result : undefined;
}
