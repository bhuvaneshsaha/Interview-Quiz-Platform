import { attemptOutcome, attemptOutcomeIcon, attemptOutcomeLabel } from './attempt-outcome';

describe('attempt outcome', () => {
  it('marks full credit correct, zero wrong, and a middle score partly correct', () => {
    expect(attemptOutcome({ status: 'scored', pointsAwarded: 1, points: 1 })).toBe('correct');
    expect(attemptOutcomeLabel({ status: 'scored', pointsAwarded: 2, points: 2 })).toBe('Correct');
    expect(attemptOutcome({ status: 'scored', pointsAwarded: 0, points: 1 })).toBe('wrong');
    expect(attemptOutcomeLabel({ status: 'scored', pointsAwarded: 0, points: 3 })).toBe('Wrong');
    expect(attemptOutcome({ status: 'scored', pointsAwarded: 1, points: 2 })).toBe('partial');
    expect(attemptOutcomeLabel({ status: 'scored', pointsAwarded: 1, points: 2 })).toBe('Partly correct');
  });

  it('treats unsettled and missing points as pending, with an icon and a label', () => {
    expect(attemptOutcome({ status: 'unsettled', pointsAwarded: null, points: 5 })).toBe('pending');
    expect(attemptOutcome({ status: 'scored', pointsAwarded: null, points: 5 })).toBe('pending');
    expect(attemptOutcomeLabel({ status: 'unsettled', pointsAwarded: null, points: 5 })).toBe('Pending review');
    expect(attemptOutcomeIcon({ status: 'unsettled', pointsAwarded: null, points: 5 })).toBe('●');
    expect(attemptOutcomeIcon({ status: 'scored', pointsAwarded: 1, points: 1 })).toBe('✓');
    expect(attemptOutcomeIcon({ status: 'scored', pointsAwarded: 0, points: 1 })).toBe('✕');
  });
});
