import { FilterTarget, ReportFilterValue } from '../../core/api/contracts';

export type ReportKey = 'funnel' | 'scores' | 'time' | 'questions' | 'candidates' | 'pendingReview';

export interface ReportDefinition {
  key: ReportKey;
  route: string;
  target: FilterTarget;
  title: string;
  description: string;
}

export const reports: ReportDefinition[] = [
  {
    key: 'funnel',
    route: 'funnel',
    target: 'reportsFunnel',
    title: 'Funnel',
    description: 'Assigned, appeared, submitted, completed, and pending review by opening and quiz.',
  },
  {
    key: 'scores',
    route: 'scores',
    target: 'reportsScores',
    title: 'Scores',
    description: 'Auto-score distribution and a 70% pass rate. Unsettled human or AI points stay out of the pass.',
  },
  {
    key: 'time',
    route: 'time',
    target: 'reportsTime',
    title: 'Time taken',
    description: 'Async time is submit minus start. Live time subtracts paused seconds once.',
  },
  {
    key: 'questions',
    route: 'questions',
    target: 'reportsQuestions',
    title: 'Questions',
    description: 'Difficulty and discrimination for settled auto-scored items. Discrimination is hidden below 30 responses.',
  },
  {
    key: 'candidates',
    route: 'candidates',
    target: 'reportsCandidates',
    title: 'Candidates',
    description: 'Rank by auto score, then by time taken.',
  },
  {
    key: 'pendingReview',
    route: 'pending-review',
    target: 'reportsPendingReview',
    title: 'Pending review',
    description: 'Queue age from submit until now, oldest first.',
  },
];

export function reportByRoute(route: string | null): ReportDefinition | undefined {
  return reports.find((report) => report.route === route);
}

export interface ReportField {
  field: string;
  label: string;
  operators: string[];
  kind: 'text' | 'datetime' | 'number' | 'status' | 'mode';
  reports: ReportKey[] | 'all';
}

export const reportFields: ReportField[] = [
  { field: 'openingId', label: 'Opening', operators: ['is', 'in'], kind: 'text', reports: 'all' },
  { field: 'quizId', label: 'Quiz', operators: ['is', 'in'], kind: 'text', reports: 'all' },
  { field: 'createdAtUtc', label: 'Assigned at', operators: ['between', '>', '<'], kind: 'datetime', reports: 'all' },
  { field: 'submittedAtUtc', label: 'Submitted at', operators: ['between', '>', '<'], kind: 'datetime', reports: 'all' },
  { field: 'status', label: 'Status', operators: ['is', 'in'], kind: 'status', reports: 'all' },
  { field: 'mode', label: 'Mode', operators: ['is'], kind: 'mode', reports: 'all' },
  { field: 'snapshotId', label: 'Snapshot', operators: ['is'], kind: 'text', reports: 'all' },
  { field: 'quizRowVersion', label: 'Quiz version', operators: ['is', 'in'], kind: 'number', reports: 'all' },
  {
    field: 'autoPointsAwarded',
    label: 'Auto score',
    operators: ['>', '<', 'between'],
    kind: 'number',
    reports: ['scores', 'candidates'],
  },
  {
    field: 'pendingAgeHours',
    label: 'Age (hours)',
    operators: ['>', 'between'],
    kind: 'number',
    reports: ['pendingReview'],
  },
];

export function fieldsFor(key: ReportKey): ReportField[] {
  return reportFields.filter((field) => field.reports === 'all' || field.reports.includes(key));
}

export function parseChipValue(field: ReportField, operator: string, raw: string, rawTo: string): ReportFilterValue | null {
  const value = raw.trim();
  const valueTo = rawTo.trim();
  if (!value || (operator === 'between' && !valueTo)) {
    return null;
  }
  if (operator === 'in') {
    const parts = value.split(',').map((part) => part.trim()).filter((part) => part.length > 0);
    if (parts.length === 0) {
      return null;
    }
    return field.kind === 'number' ? parts.map((part) => Number(part)) : parts;
  }
  if (operator === 'between') {
    if (field.kind === 'number') {
      return { from: Number(value), to: Number(valueTo) };
    }
    return { from: toUtcIso(value), to: toUtcIso(valueTo) };
  }
  if (field.kind === 'datetime') {
    return toUtcIso(value);
  }
  if (field.kind === 'number') {
    return Number(value);
  }
  return value;
}

export function chipText(field: string, operator: string, value: ReportFilterValue): string {
  if (Array.isArray(value)) {
    return `${field} ${operator} ${value.join(', ')}`;
  }
  if (typeof value === 'object') {
    return `${field} ${operator} ${value.from} – ${value.to}`;
  }
  return `${field} ${operator} ${value}`;
}

function toUtcIso(value: string): string {
  if (value.endsWith('Z') || /[+-]\d{2}:\d{2}$/.test(value)) {
    return new Date(value).toISOString();
  }
  return new Date(value).toISOString();
}
