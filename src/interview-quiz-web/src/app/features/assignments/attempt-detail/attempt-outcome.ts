export type AttemptOutcome = 'correct' | 'wrong' | 'partial' | 'pending';

export interface AttemptOutcomeInput {
  status: string | null | undefined;
  pointsAwarded: number | null | undefined;
  points: number | null | undefined;
}

export const ATTEMPT_OUTCOME_LABELS: Record<AttemptOutcome, string> = {
  correct: 'Correct',
  wrong: 'Wrong',
  partial: 'Partly correct',
  pending: 'Pending review',
};

const ATTEMPT_OUTCOME_ICONS: Record<AttemptOutcome, string> = {
  correct: '✓',
  wrong: '✕',
  partial: '◐',
  pending: '●',
};

/** Outcome from the stored per-question score. Pending covers unsettled and missing points. */
export function attemptOutcome(item: AttemptOutcomeInput): AttemptOutcome {
  if (item.status === 'unsettled' || item.pointsAwarded === null || item.pointsAwarded === undefined) {
    return 'pending';
  }
  const points = item.points ?? 0;
  if (points > 0 && item.pointsAwarded >= points) {
    return 'correct';
  }
  if (item.pointsAwarded <= 0) {
    return 'wrong';
  }
  return 'partial';
}

export function attemptOutcomeLabel(item: AttemptOutcomeInput): string {
  return ATTEMPT_OUTCOME_LABELS[attemptOutcome(item)];
}

export function attemptOutcomeIcon(item: AttemptOutcomeInput): string {
  return ATTEMPT_OUTCOME_ICONS[attemptOutcome(item)];
}
