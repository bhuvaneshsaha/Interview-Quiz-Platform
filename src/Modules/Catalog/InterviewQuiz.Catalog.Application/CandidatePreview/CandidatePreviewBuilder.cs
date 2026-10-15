using InterviewQuiz.Catalog.Application.Contracts;

namespace InterviewQuiz.Catalog.Application.CandidatePreview;

public static class CandidatePreviewBuilder
{
    public static CandidateQuizPreviewResponse FromSnapshot(QuizSnapshotDto snapshot, string openingName)
    {
        var emptyOrder = new Dictionary<Guid, IReadOnlyList<string>>();
        var questions = snapshot.Questions
            .OrderBy(q => q.SortOrder)
            .ThenBy(q => q.Id)
            .Select(question => new CandidatePreviewQuestionDto
            {
                Id = question.Id,
                SortOrder = question.SortOrder,
                Type = question.Type,
                Stem = question.Stem,
                Body = CandidateQuestionRedactor.RedactBody(question, emptyOrder),
                SourceQuestionId = null
            })
            .ToList();

        return new CandidateQuizPreviewResponse
        {
            Title = snapshot.Title,
            OpeningName = string.IsNullOrWhiteSpace(openingName) ? "Preview" : openingName.Trim(),
            QuestionCount = questions.Count,
            SuggestedDurationMinutes = SuggestDuration(questions.Count),
            RowVersion = snapshot.RowVersion,
            Questions = questions
        };
    }

    public static int SuggestDuration(int questionCount)
        => Math.Clamp(Math.Max(questionCount, 1) * 2, 10, 60);
}
