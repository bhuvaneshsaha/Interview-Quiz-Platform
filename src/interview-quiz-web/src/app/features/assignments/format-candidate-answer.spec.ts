import { formatCandidateAnswer } from './format-candidate-answer';

const seededBodies = {
  multipleChoiceSingle: {
    options: [
      { id: 'opt-200', text: '200 OK' },
      { id: 'opt-201', text: '201 Created' },
    ],
  },
  multipleChoiceMulti: {
    options: [
      { id: 'opt-kestrel', text: 'Kestrel' },
      { id: 'opt-health', text: 'Health checks' },
      { id: 'opt-blank', text: '   ' },
    ],
  },
  ordering: {
    items: [
      { id: 'item-authn', text: 'Authentication' },
      { id: 'item-authz', text: 'Authorization' },
      { id: 'item-endpoint', text: 'Endpoint' },
    ],
  },
  shared: {
    slots: [
      { id: 'slot-get', label: 'GET' },
      { id: 'slot-post', label: 'POST' },
    ],
    bank: [
      { id: 'item-read', text: 'Read a resource' },
      { id: 'item-create', text: 'Create a resource' },
    ],
  },
  perSlot: {
    slots: [
      {
        id: 'slot-401',
        label: '401',
        options: [
          { id: 'opt-unauth', text: 'Unauthenticated' },
          { id: 'opt-forbid', text: 'Forbidden' },
        ],
      },
    ],
  },
};

describe('formatCandidateAnswer', () => {
  it('shows text and true/false without dumping keys', () => {
    expect(formatCandidateAnswer({ text: '  A quiz belongs to one opening.  ' })).toBe(
      'A quiz belongs to one opening.',
    );
    expect(formatCandidateAnswer({ value: false })).toBe('False');
    expect(formatCandidateAnswer({ value: true })).toBe('True');
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

  it('resolves every choice, bank item, slot, and ordering id to authored text', () => {
    expect(formatCandidateAnswer({ optionId: 'opt-201' }, seededBodies.multipleChoiceSingle)).toBe(
      '201 Created',
    );
    expect(
      formatCandidateAnswer(
        { optionIds: ['opt-kestrel', 'opt-health'] },
        seededBodies.multipleChoiceMulti,
      ),
    ).toBe('Kestrel, Health checks');
    expect(
      formatCandidateAnswer(
        { itemIds: ['item-endpoint', 'item-authn', 'item-authz'] },
        seededBodies.ordering,
      ),
    ).toBe('Endpoint → Authentication → Authorization');
    expect(
      formatCandidateAnswer(
        {
          slots: [
            { slotId: 'slot-get', itemId: 'item-read' },
            { slotId: 'slot-post', itemId: 'item-create' },
          ],
        },
        seededBodies.shared,
      ),
    ).toBe('GET: Read a resource; POST: Create a resource');
    expect(
      formatCandidateAnswer(
        { slots: [{ slotId: 'slot-401', optionId: 'opt-unauth' }] },
        seededBodies.perSlot,
      ),
    ).toBe('401: Unauthenticated');
  });

  it('uses a neutral label when text is missing and never prints the id', () => {
    expect(formatCandidateAnswer({ optionId: 'opt-201' })).toBe('(untitled option)');
    expect(
      formatCandidateAnswer({ optionIds: ['opt-blank'] }, seededBodies.multipleChoiceMulti),
    ).toBe('(untitled option)');
    expect(formatCandidateAnswer({ itemIds: ['missing'] }, seededBodies.ordering)).toBe(
      '(untitled option)',
    );
    expect(
      formatCandidateAnswer({ slots: [{ slotId: 'slot-get', itemId: 'missing' }] }, seededBodies.shared),
    ).toBe('GET: (untitled option)');
    const shown = formatCandidateAnswer({ optionId: 'opt-201', optionIds: ['opt-kestrel'] });
    expect(shown).not.toContain('opt-');
  });

  it('uses a plain empty state', () => {
    expect(formatCandidateAnswer(null)).toBe('No answer');
    expect(formatCandidateAnswer({ text: '   ' })).toBe('No answer');
    expect(formatCandidateAnswer({ isCorrect: false })).toBe('Answer recorded');
    expect(formatCandidateAnswer({ optionIds: [] }, seededBodies.multipleChoiceMulti)).toBe('No answer');
  });
});
