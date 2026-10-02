import { resultStatusLabel, statusLabel } from './status-labels';

describe('status labels', () => {
  it('humanizes assignment and attempt statuses', () => {
    expect(statusLabel('notStarted')).toBe('Not started');
    expect(statusLabel('inProgress')).toBe('In progress');
    expect(statusLabel('submitted')).toBe('Submitted');
    expect(statusLabel('pendingReview')).toBe('Pending review');
    expect(statusLabel('completed')).toBe('Completed');
  });

  it('humanizes result statuses', () => {
    expect(resultStatusLabel('complete')).toBe('Complete');
    expect(resultStatusLabel('incomplete')).toBe('Incomplete');
  });

  it('does not leave unknown camelCase enums raw', () => {
    expect(statusLabel('needsReview')).toBe('Needs review');
    expect(resultStatusLabel('pendingReview')).toBe('Pending review');
  });

  it('uses a dash when the status is missing', () => {
    expect(statusLabel(null)).toBe('—');
    expect(statusLabel('')).toBe('—');
    expect(resultStatusLabel(undefined)).toBe('—');
  });
});
