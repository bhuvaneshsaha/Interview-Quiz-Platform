import { HttpParams } from '@angular/common/http';

export function compactCriteria(criteria: object): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(criteria)) {
    const compacted = compactValue(value);
    if (compacted !== undefined) {
      result[key] = compacted;
    }
  }
  return result;
}

export function pageAndCriteria(page: number, pageSize: number, criteria?: object): HttpParams {
  let params = new HttpParams().set('page', page).set('pageSize', pageSize);
  const compact = compactCriteria(criteria ?? {});
  if (Object.keys(compact).length > 0) {
    params = params.set('criteria', JSON.stringify(compact));
  }
  return params;
}

function compactValue(value: unknown): unknown {
  if (value === null || value === undefined || value === '') {
    return undefined;
  }
  if (typeof value === 'number' && Number.isNaN(value)) {
    return undefined;
  }
  if (Array.isArray(value)) {
    const items = value.map(compactValue).filter((item) => item !== undefined);
    return items.length > 0 ? items : undefined;
  }
  if (typeof value === 'object') {
    const nested = compactCriteria(value as Record<string, unknown>);
    return Object.keys(nested).length > 0 ? nested : undefined;
  }
  return value;
}
