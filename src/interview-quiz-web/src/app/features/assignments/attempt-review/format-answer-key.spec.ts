import { formatAnswerKey } from './format-answer-key';

describe('formatAnswerKey', () => {
  it('reads keyed answers and leaves written items without a key', () => {
    expect(
      formatAnswerKey('trueFalse', { correct: true }),
    ).toBe('True');
    expect(
      formatAnswerKey('multipleChoiceSingle', {
        options: [
          { id: 'opt-200', text: '200 OK', isCorrect: false },
          { id: 'opt-201', text: '201 Created', isCorrect: true },
        ],
      }),
    ).toBe('201 Created');
    expect(
      formatAnswerKey('longText', { guidance: 'Name the public API.' }),
    ).toBe('No keyed answer. Guidance: Name the public API.');
    expect(formatAnswerKey('longText', {})).toBe('No keyed answer.');
  });
});
