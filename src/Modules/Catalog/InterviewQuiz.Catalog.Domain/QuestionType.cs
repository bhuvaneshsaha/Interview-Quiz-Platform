namespace InterviewQuiz.Catalog.Domain;

/// <summary>
/// Authoring question types. Serialized as camelCase strings in JSON and the API
/// (<c>multipleChoiceSingle</c>, <c>trueFalse</c>, …). Code / coding types are rejected.
/// </summary>
public enum QuestionType
{
    MultipleChoiceSingle,
    MultipleChoiceMulti,
    TrueFalse,
    ShortText,
    LongText,
    DragDropSharedBank,
    DragDropPerSlot,
    Ordering
}
