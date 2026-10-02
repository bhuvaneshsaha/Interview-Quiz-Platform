const STATUS_LABELS: Record<string, string> = {
  notStarted: 'Not started',
  inProgress: 'In progress',
  submitted: 'Submitted',
  pendingReview: 'Pending review',
  completed: 'Completed',
};

const RESULT_STATUS_LABELS: Record<string, string> = {
  complete: 'Complete',
  incomplete: 'Incomplete',
};

/** Assignment and attempt lifecycle statuses. Unknown values are still humanized. */
export function statusLabel(status: string | null | undefined): string {
  return labelFrom(status, STATUS_LABELS);
}

/** Attempt result statuses (`complete`, `incomplete`, and any later camelCase values). */
export function resultStatusLabel(status: string | null | undefined): string {
  return labelFrom(status, RESULT_STATUS_LABELS);
}

function labelFrom(status: string | null | undefined, known: Record<string, string>): string {
  if (!status) {
    return '—';
  }
  return known[status] ?? humanizeToken(status);
}

function humanizeToken(value: string): string {
  const spaced = value
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .trim();
  if (!spaced) {
    return value;
  }
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}
