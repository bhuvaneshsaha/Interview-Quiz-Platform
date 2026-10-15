import { CandidateQuestion, QuestionType } from '../../../core/api/contracts';
import {
  answerHint,
  bankItems,
  candidateTypeBadge,
  choiceOptions,
  orderingItems,
  perSlots,
  sanitizeForSave,
  sharedSlots,
  UNTITLED_OPTION,
} from './candidate-answer.util';

function question(type: QuestionType, body: Record<string, unknown>): CandidateQuestion {
  return {
    id: 'q-1',
    sortOrder: 0,
    type,
    stem: 'Stem',
    body,
    sourceQuestionId: null,
  };
}

describe('candidate answer utils', () => {
  it('shows authored text for every question type and never the id', () => {
    const single = choiceOptions(
      question('multipleChoiceSingle', {
        options: [
          { id: 'opt-201', text: '201 Created' },
          { id: 'opt-blank', text: '  ' },
        ],
      }),
    );
    expect(single.map((option) => option.text)).toEqual(['201 Created', UNTITLED_OPTION]);
    expect(single.some((option) => option.text === option.id)).toBe(false);

    const multi = choiceOptions(
      question('multipleChoiceMulti', {
        options: [{ id: 'opt-kestrel', text: 'Kestrel' }],
      }),
    );
    expect(multi[0].text).toBe('Kestrel');

    const shared = sharedSlots(
      question('dragDropSharedBank', {
        slots: [{ id: 'slot-get', label: 'GET' }, { id: 'slot-empty', label: '' }],
        bank: [{ id: 'item-read', text: 'Read a resource' }],
      }),
    );
    expect(shared.map((slot) => slot.label)).toEqual(['GET', UNTITLED_OPTION]);
    expect(bankItems(question('dragDropSharedBank', {
      bank: [{ id: 'item-read', text: 'Read a resource' }, { id: 'item-blank', text: '' }],
    })).map((item) => item.text)).toEqual(['Read a resource', UNTITLED_OPTION]);

    const per = perSlots(
      question('dragDropPerSlot', {
        slots: [
          {
            id: 'slot-401',
            label: '401',
            options: [{ id: 'opt-unauth', text: 'Unauthenticated' }, { id: 'opt-empty', text: '' }],
          },
        ],
      }),
    );
    expect(per[0].label).toBe('401');
    expect(per[0].options.map((option) => option.text)).toEqual(['Unauthenticated', UNTITLED_OPTION]);

    const ordered = orderingItems(
      question('ordering', {
        items: [
          { id: 'item-authn', text: 'Authentication' },
          { id: 'item-missing', text: '' },
        ],
      }),
    );
    expect(ordered.map((item) => item.text)).toEqual(['Authentication', UNTITLED_OPTION]);
    expect(ordered.some((item) => item.text.includes('item-'))).toBe(false);
  });

  it('gives each question type an answering hint without a type name or points', () => {
    const hints: Record<QuestionType, string> = {
      multipleChoiceSingle: answerHint(question('multipleChoiceSingle', {})),
      multipleChoiceMulti: answerHint(question('multipleChoiceMulti', {})),
      trueFalse: answerHint(question('trueFalse', {})),
      shortText: answerHint(question('shortText', {})),
      longText: answerHint(question('longText', { guidance: 'Mention module boundaries.' })),
      dragDropSharedBank: answerHint(question('dragDropSharedBank', {})),
      dragDropPerSlot: answerHint(question('dragDropPerSlot', {})),
      ordering: answerHint(question('ordering', {})),
    };
    expect(hints.multipleChoiceMulti).toBe('Select all that apply.');
    expect(hints.longText).toBe('Mention module boundaries.');
    expect(hints.dragDropSharedBank).toBe('Match each slot to an item from the list.');
    expect(hints.dragDropPerSlot).toBe('Match each slot to one of its options.');
    expect(`${hints.dragDropSharedBank} ${hints.dragDropPerSlot}`).not.toMatch(/drag/i);
    expect(candidateTypeBadge('dragDropSharedBank')).toBe('Match each slot');
    expect(candidateTypeBadge('dragDropPerSlot')).toBe('Match each slot');
    expect(candidateTypeBadge('multipleChoiceSingle')).toBe('Multiple choice (single)');
    expect(candidateTypeBadge('ordering')).toBe('Ordering');
    expect(hints.ordering).toContain('Move up');
    expect(hints.ordering).toContain('Move down');
    expect(Object.values(hints).join(' ')).not.toMatch(/pts|Multiple choice|Drag and drop/);
  });

  it('keeps the submitted answer shape for ordering and drag-and-drop', () => {
    expect(
      sanitizeForSave('ordering', { itemIds: ['item-endpoint', 'item-authn'] }),
    ).toEqual({ itemIds: ['item-endpoint', 'item-authn'] });
    expect(
      sanitizeForSave('dragDropSharedBank', {
        slots: [{ slotId: 'slot-get', itemId: 'item-read' }],
      }),
    ).toEqual({ slots: [{ slotId: 'slot-get', itemId: 'item-read' }] });
    expect(
      sanitizeForSave('dragDropPerSlot', {
        slots: [{ slotId: 'slot-401', optionId: 'opt-unauth' }],
      }),
    ).toEqual({ slots: [{ slotId: 'slot-401', optionId: 'opt-unauth' }] });
    expect(sanitizeForSave('multipleChoiceSingle', { optionId: 'opt-201' })).toEqual({
      optionId: 'opt-201',
    });
    expect(sanitizeForSave('multipleChoiceMulti', { optionIds: ['a'] })).toEqual({ optionIds: ['a'] });
    expect(sanitizeForSave('trueFalse', { value: true })).toEqual({ value: true });
    expect(sanitizeForSave('shortText', { text: 'REST' })).toEqual({ text: 'REST' });
    expect(sanitizeForSave('longText', { text: 'Because.' })).toEqual({ text: 'Because.' });
  });
});