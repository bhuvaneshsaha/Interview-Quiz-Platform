import {
  CreateQuizRequest,
  QuestionType,
  QuizResponse,
} from '../../core/api/contracts';
import {
  defaultBodyForType,
  defaultCreditMode,
  defaultQuestionDraft,
  defaultScoringMode,
  newItemId,
  QuestionDraft,
  QuizDraft,
  requiresCreditMode,
  toCreateRequest,
  toUpdateRequest,
  quizToDraft,
  validateQuizDraft,
} from './quiz-form.mapper';

function draftWithQuestion(question: QuestionDraft): QuizDraft {
  return {
    openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
    title: 'Backend interview',
    description: 'Working copy',
    expectedExperienceYears: 5,
    tags: [{ key: 'Role', value: 'Backend' }],
    questions: [question],
  };
}

function filledMc(type: QuestionType): QuestionDraft {
  const question = defaultQuestionDraft(type);
  if ('options' in question.body) {
    question.body.options[0].text = 'Kestrel';
    question.body.options[1].text = 'A required mediator library';
  }
  question.stem = 'Pick hosting concerns.';
  return question;
}

describe('quiz-form.mapper', () => {
  it('builds default bodies that satisfy structural rules', () => {
    const single = defaultBodyForType('multipleChoiceSingle');
    expect('options' in single && single.options.length).toBe(2);
    if ('options' in single) {
      expect(single.options[0].isCorrect).toBe(true);
      expect(single.options[1].isCorrect).toBe(false);
      expect(new Set(single.options.map((option) => option.id)).size).toBe(2);
    }

    const multi = defaultBodyForType('multipleChoiceMulti');
    if ('options' in multi) {
      expect(multi.options[0].isCorrect).toBe(true);
      expect(defaultCreditMode('multipleChoiceMulti')).toBe('partial');
    }

    const tf = defaultBodyForType('trueFalse');
    expect(tf).toEqual({ correct: true });

    const shortText = defaultBodyForType('shortText');
    expect(shortText).toEqual({ acceptableAnswers: [], caseSensitive: false });
    expect(defaultScoringMode('shortText')).toBe('humanOnly');

    const longText = defaultBodyForType('longText');
    expect(longText).toEqual({ maxLength: null, guidance: '' });
    expect(defaultScoringMode('longText')).toBe('humanOnly');

    const shared = defaultBodyForType('dragDropSharedBank');
    expect('slots' in shared && shared.slots.length).toBe(1);
    expect('bank' in shared && shared.bank.length).toBe(1);
    if ('slots' in shared && 'bank' in shared) {
      expect(shared.bank[0].isDistractor).toBe(false);
      expect(shared.slots[0].correctItemId).toBe(shared.bank[0].id);
    }

    const perSlot = defaultBodyForType('dragDropPerSlot');
    if ('slots' in perSlot && !('bank' in perSlot)) {
      expect(perSlot.slots.length).toBe(1);
      expect(perSlot.slots[0].options.length).toBe(2);
      expect(perSlot.slots[0].options[0].isCorrect).toBe(true);
    }

    const ordering = defaultBodyForType('ordering');
    if ('items' in ordering) {
      expect(ordering.items.map((item) => item.correctIndex)).toEqual([0, 1]);
    }
  });

  it('omits creditMode except for multiple choice multi and ordering', () => {
    const types: QuestionType[] = [
      'multipleChoiceSingle',
      'trueFalse',
      'shortText',
      'longText',
      'dragDropSharedBank',
      'dragDropPerSlot',
    ];
    for (const type of types) {
      const question = defaultQuestionDraft(type);
      question.stem = 'Stem';
      question.creditMode = 'partial';
      if (type === 'multipleChoiceSingle' && 'options' in question.body) {
        question.body.options[0].text = 'A';
        question.body.options[1].text = 'B';
      }
      if (type === 'trueFalse' && 'correct' in question.body) {
        question.body.correct = true;
      }
      if (type === 'dragDropSharedBank' && 'slots' in question.body && 'bank' in question.body) {
        question.body.slots[0].label = 'GET';
        question.body.bank[0].text = 'Read';
      }
      if (type === 'dragDropPerSlot' && 'slots' in question.body && !('bank' in question.body)) {
        question.body.slots[0].label = '401';
        question.body.slots[0].options[0].text = 'Unauthenticated';
        question.body.slots[0].options[1].text = 'Forbidden';
      }
      const request = toCreateRequest(draftWithQuestion(question));
      expect(request.questions[0].creditMode).toBeUndefined();
      expect('creditMode' in request.questions[0]).toBe(false);
    }

    const multi = filledMc('multipleChoiceMulti');
    multi.creditMode = 'allOrNothing';
    expect(toCreateRequest(draftWithQuestion(multi)).questions[0].creditMode).toBe('allOrNothing');

    const ordering = defaultQuestionDraft('ordering');
    ordering.stem = 'Order the pipeline.';
    if ('items' in ordering.body) {
      ordering.body.items[0].text = 'Authentication';
      ordering.body.items[1].text = 'Authorization';
    }
    ordering.creditMode = 'partial';
    expect(toCreateRequest(draftWithQuestion(ordering)).questions[0].creditMode).toBe('partial');
  });

  it('uses array order as sort order and omits ids for new questions', () => {
    const first = defaultQuestionDraft('trueFalse');
    first.stem = 'First';
    const second = defaultQuestionDraft('trueFalse');
    second.stem = 'Second';
    const request = toCreateRequest({
      ...draftWithQuestion(first),
      questions: [first, second],
    });
    expect(request.questions.map((question) => question.stem)).toEqual(['First', 'Second']);
    expect(request.questions[0].id).toBeUndefined();
    expect(request.questions[1].id).toBeUndefined();
  });

  it('preserves server question ids and rowVersion on update', () => {
    const quiz: QuizResponse = {
      id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001',
      openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
      title: 'Backend interview — working copy',
      description: 'Sample authoring quiz',
      expectedExperienceYears: 5,
      tags: { Client: 'Internal' },
      questions: [
        {
          id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2103',
          sortOrder: 1,
          type: 'trueFalse',
          stem: 'A quiz belongs to exactly one opening.',
          scoringMode: 'auto',
          creditMode: null,
          points: 1,
          body: { correct: true },
        },
        {
          id: '4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2101',
          sortOrder: 0,
          type: 'multipleChoiceSingle',
          stem: 'Which HTTP status means a resource was created?',
          scoringMode: 'auto',
          creditMode: null,
          points: 1,
          body: {
            options: [
              { id: 'opt-200', text: '200 OK', isCorrect: false },
              { id: 'opt-201', text: '201 Created', isCorrect: true },
            ],
          },
        },
      ],
      rowVersion: 3,
      createdAtUtc: '2026-01-01T00:00:00Z',
      updatedAtUtc: '2026-01-02T00:00:00Z',
    };

    const draft = quizToDraft(quiz);
    expect(draft.questions.map((question) => question.stem)).toEqual([
      'Which HTTP status means a resource was created?',
      'A quiz belongs to exactly one opening.',
    ]);

    const created = defaultQuestionDraft('trueFalse');
    created.stem = 'New question';
    draft.questions.push(created);

    const update = toUpdateRequest(draft, quiz.id, quiz.rowVersion);
    expect(update.id).toBe(quiz.id);
    expect(update.rowVersion).toBe(3);
    expect(update.questions[0].id).toBe('4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2101');
    expect(update.questions[1].id).toBe('4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2103');
    expect(update.questions[2].id).toBeUndefined();
    expect(update.questions[0].creditMode).toBeUndefined();
  });

  it('maps tags and optional description onto the create request', () => {
    const question = defaultQuestionDraft('trueFalse');
    question.stem = 'Stem';
    const request: CreateQuizRequest = toCreateRequest({
      openingId: ' 3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001 ',
      title: ' Title ',
      description: '',
      expectedExperienceYears: 4,
      tags: [
        { key: 'Client', value: 'Internal' },
        { key: '  ', value: 'ignored' },
      ],
      questions: [question],
    });
    expect(request.openingId).toBe('3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001');
    expect(request.title).toBe('Title');
    expect(request.description).toBeUndefined();
    expect(request.tags).toEqual({ Client: 'Internal' });
  });

  it('rejects long text auto scoring and missing credit mode for multi', () => {
    const longText = defaultQuestionDraft('longText');
    longText.stem = 'Explain modular monoliths.';
    longText.scoringMode = 'auto';
    expect(validateQuizDraft(draftWithQuestion(longText)).some((error) => error.path.endsWith('scoringMode'))).toBe(
      true,
    );

    const multi = filledMc('multipleChoiceMulti');
    multi.creditMode = null;
    expect(validateQuizDraft(draftWithQuestion(multi)).some((error) => error.path.endsWith('creditMode'))).toBe(
      true,
    );
  });

  it('requires creditMode only for multi and ordering', () => {
    expect(requiresCreditMode('multipleChoiceMulti')).toBe(true);
    expect(requiresCreditMode('ordering')).toBe(true);
    expect(requiresCreditMode('multipleChoiceSingle')).toBe(false);
    expect(requiresCreditMode('trueFalse')).toBe(false);
  });

  it('generates short unique body ids', () => {
    const ids = new Set(Array.from({ length: 20 }, () => newItemId('opt')));
    expect(ids.size).toBe(20);
    for (const id of ids) {
      expect(id.startsWith('opt-')).toBe(true);
      expect(id.length).toBeLessThanOrEqual(64);
    }
  });
});
