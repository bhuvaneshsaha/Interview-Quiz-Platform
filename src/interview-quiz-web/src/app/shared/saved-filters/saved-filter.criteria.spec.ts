import {
  compactCriteria,
  criteriaFromUnknown,
  openingCriteriaFromUnknown,
  quizCriteriaFromUnknown,
  tagsForListForm,
  tagsFromPair,
  templateCriteriaFromUnknown,
} from './saved-filter.criteria';

describe('saved-filter.criteria', () => {
  it('drops empty strings, nulls, and empty nested objects', () => {
    expect(
      compactCriteria({
        keyword: '  backend  ',
        openingId: '',
        experienceMinYears: 0,
        tags: {},
        extra: null,
      }),
    ).toEqual({
      keyword: '  backend  ',
      experienceMinYears: 0,
    });
  });

  it('maps quiz saved-filter criteria onto list fields', () => {
    expect(
      quizCriteriaFromUnknown({
        openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
        keyword: ' Kestrel ',
        experienceMinYears: 3,
        experienceMaxYears: '8',
        tags: { Role: 'Backend', Empty: '  ' },
        unknown: 'ignored',
      }),
    ).toEqual({
      openingId: '3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001',
      keyword: 'Kestrel',
      experienceMinYears: 3,
      experienceMaxYears: 8,
      tags: { Role: 'Backend' },
    });
  });

  it('maps opening and template criteria and empty unknown payloads', () => {
    expect(
      openingCriteriaFromUnknown({
        owner: ' Recruiting ',
        startDateFrom: '2026-01-01',
        tags: { Client: 'Acme' },
      }),
    ).toEqual({
      owner: 'Recruiting',
      startDateFrom: '2026-01-01',
      tags: { Client: 'Acme' },
    });

    expect(templateCriteriaFromUnknown({ keyword: 'sample', experienceMaxYears: 10 })).toEqual({
      keyword: 'sample',
      experienceMaxYears: 10,
    });

    expect(criteriaFromUnknown('quizzes', null)).toEqual({});
    expect(criteriaFromUnknown('openings', 'nope')).toEqual({});
    expect(criteriaFromUnknown('templates', [])).toEqual({});
  });

  it('builds a single tag pair only when both key and value are present', () => {
    expect(tagsFromPair('Role', 'Backend')).toEqual({ Role: 'Backend' });
    expect(tagsFromPair('Role', '')).toBeUndefined();
    expect(tagsFromPair('', 'Backend')).toBeUndefined();
    expect(tagsFromPair('  ', '  ')).toBeUndefined();
  });

  it('keeps extra applied tags when the visible pair is unchanged', () => {
    const applied = { Role: 'Backend', Client: 'Acme' };
    expect(tagsForListForm('Role', 'Backend', applied)).toEqual(applied);
    expect(tagsForListForm('Role', 'Frontend', applied)).toEqual({ Role: 'Frontend', Client: 'Acme' });
    expect(tagsForListForm('Stack', 'Angular', applied)).toEqual({ Client: 'Acme', Stack: 'Angular' });
    expect(tagsForListForm('', '', applied)).toEqual(applied);
    expect(tagsForListForm('Role', '', applied)).toBeUndefined();
  });
});
