namespace InterviewQuiz.Openings.Application;

public static class OpeningSummaryMath
{
    public readonly record struct Counts(int Assigned, int Appeared, int Submitted);

    public readonly record struct AssignmentRow(Guid AssignmentId, Guid QuizId, string CandidateEmail);

    public readonly record struct AttemptRow(bool Started, bool Submitted);

    public static Counts Count(
        IEnumerable<AssignmentRow> assignments,
        IReadOnlyDictionary<Guid, AttemptRow> attempts)
    {
        var rows = assignments.ToList();
        var emails = rows
            .Select(row => row.CandidateEmail)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var appeared = 0;
        var submitted = 0;
        foreach (var email in emails)
        {
            var ids = rows
                .Where(row => string.Equals(row.CandidateEmail, email, StringComparison.Ordinal))
                .Select(row => row.AssignmentId);
            if (ids.Any(id => attempts.TryGetValue(id, out var attempt) && attempt.Started))
            {
                appeared++;
            }

            if (ids.Any(id => attempts.TryGetValue(id, out var attempt) && attempt.Submitted))
            {
                submitted++;
            }
        }

        return new Counts(emails.Count, appeared, submitted);
    }
}
