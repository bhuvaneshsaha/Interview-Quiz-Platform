import { formatCandidateAnswer } from './format-candidate-answer';

describe('formatCandidateAnswer', () => {
  it('shows text, true/false, and choice ids', () => {
    expect(formatCandidateAnswer({ text: '  A quiz belongs to one opening.  ' })).toBe(
      'A quiz belongs to one opening.',
    );
    expect(formatCandidateAnswer({ value: false })).toBe('False');
    expect(formatCandidateAnswer({ optionId: 'opt-201' })).toBe('opt-201');
    expect(formatCandidateAnswer({ optionIds: ['a', 'b'] })).toBe('a, b');
    expect(formatCandidateAnswer({ itemIds: ['c', 'a'] })).toBe('c → a');
    expect(
      formatCandidateAnswer({ slots: [{ slotId: 'slot-1', itemId: 'item-9' }] }),
    ).toBe('slot-1: item-9');
  });

  it('does not print answer-key fields', () => {
    const formatted = formatCandidateAnswer({
      text: 'Because the opening owns the quiz.',
      isCorrect: true,
      acceptableAnswers: ['owned'],
      correctItemId: 'secret',
    });
    expect(formatted).toBe('Because the opening owns the quiz.');
    expect(formatted).not.toContain('isCorrect');
    expect(formatted).not.toContain('acceptableAnswers');
    expect(formatted).not.toContain('secret');
  });

  it('uses a plain empty state', () => {
    expect(formatCandidateAnswer(null)).toBe('No answer');
    expect(formatCandidateAnswer({ text: '   ' })).toBe('No answer');
    expect(formatCandidateAnswer({ isCorrect: false })).toBe('Answer recorded');
  });
});
