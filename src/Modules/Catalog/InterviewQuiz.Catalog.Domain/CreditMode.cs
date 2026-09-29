namespace InterviewQuiz.Catalog.Domain;

/// <summary>
/// Credit mode for multi-select and ordering only. Required on those types; omitted otherwise.
/// </summary>
/// <remarks>
/// Partial credit for ordering uses the <b>adjacent-pair</b> formula: one share of the
/// question points per correctly ordered adjacent pair (keyed items at positions i and i+1),
/// divided by (n − 1) pairs. Evaluation implements scoring; Catalog only stores the mode.
/// </remarks>
public enum CreditMode
{
    Partial,
    AllOrNothing
}
