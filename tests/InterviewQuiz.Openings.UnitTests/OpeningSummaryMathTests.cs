using InterviewQuiz.Openings.Application;

namespace InterviewQuiz.Openings.UnitTests;

public sealed class OpeningSummaryMathTests
{
    [Fact]
    public void Counts_distinct_candidates_and_keeps_per_quiz_separate_from_the_opening()
    {
        var quizA = Guid.NewGuid();
        var quizB = Guid.NewGuid();
        var started = Guid.NewGuid();
        var submitted = Guid.NewGuid();
        var idle = Guid.NewGuid();
        var samePersonOtherQuiz = Guid.NewGuid();

        var assignments = new[]
        {
            new OpeningSummaryMath.AssignmentRow(started, quizA, "ada@example.com"),
            new OpeningSummaryMath.AssignmentRow(submitted, quizA, "bea@example.com"),
            new OpeningSummaryMath.AssignmentRow(idle, quizA, "cy@example.com"),
            new OpeningSummaryMath.AssignmentRow(samePersonOtherQuiz, quizB, "ada@example.com")
        };
        var attempts = new Dictionary<Guid, OpeningSummaryMath.AttemptRow>
        {
            [started] = new(Started: true, Submitted: false),
            [submitted] = new(Started: true, Submitted: true),
            [samePersonOtherQuiz] = new(Started: true, Submitted: true)
        };

        var opening = OpeningSummaryMath.Count(assignments, attempts);
        var quizAOnly = OpeningSummaryMath.Count(
            assignments.Where(row => row.QuizId == quizA),
            attempts);

        Assert.Equal(3, opening.Assigned);
        Assert.Equal(2, opening.Appeared);
        Assert.Equal(2, opening.Submitted);
        Assert.Equal(3, quizAOnly.Assigned);
        Assert.Equal(2, quizAOnly.Appeared);
        Assert.Equal(1, quizAOnly.Submitted);
    }

    [Fact]
    public void Empty_assignments_are_zero()
    {
        var counts = OpeningSummaryMath.Count([], new Dictionary<Guid, OpeningSummaryMath.AttemptRow>());

        Assert.Equal(0, counts.Assigned);
        Assert.Equal(0, counts.Appeared);
        Assert.Equal(0, counts.Submitted);
    }
}
