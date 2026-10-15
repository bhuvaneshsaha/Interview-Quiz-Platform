using System.Text.Json;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Search.Application;
using InterviewQuiz.Search.Domain;

namespace InterviewQuiz.Search.UnitTests;

public sealed class ReportFilterParserTests
{
    [Fact]
    public void Unknown_field_is_rejected()
    {
        var criteria = JsonDocument.Parse(
            """
            {"filters":[{"field":"originTemplateId","operator":"is","value":"3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001"}]}
            """);

        var ex = Assert.Throws<DomainException>(() => ReportFilterParser.Parse(ReportKeys.Funnel, criteria.RootElement));
        Assert.Contains("originTemplateId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Score_field_is_rejected_on_funnel_and_accepted_on_scores()
    {
        var criteria = JsonDocument.Parse(
            """
            {"filters":[{"field":"score","operator":">","value":7}]}
            """);

        var denied = Assert.Throws<DomainException>(() => ReportFilterParser.Parse(ReportKeys.Funnel, criteria.RootElement));
        Assert.Contains("not allowed", denied.Message, StringComparison.Ordinal);

        var parsed = ReportFilterParser.Parse(ReportKeys.Scores, criteria.RootElement);
        Assert.True(parsed.HasScoreConstraint);
        Assert.False(parsed.Score.Matches(7m));
        Assert.True(parsed.Score.Matches(7.1m));
    }

    [Fact]
    public void Operator_is_not_is_rejected_and_between_must_be_ordered()
    {
        var wrongOperator = JsonDocument.Parse(
            """
            {"filters":[{"field":"openingId","operator":"is not","value":"3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001"}]}
            """);
        var denied = Assert.Throws<DomainException>(() => ReportFilterParser.Parse(ReportKeys.Funnel, wrongOperator.RootElement));
        Assert.Contains("is not", denied.Message, StringComparison.Ordinal);

        var unordered = JsonDocument.Parse(
            """
            {"filters":[{"field":"createdAtUtc","operator":"between","value":{"from":"2026-02-01T00:00:00Z","to":"2026-01-01T00:00:00Z"}}]}
            """);
        var order = Assert.Throws<DomainException>(() => ReportFilterParser.Parse(ReportKeys.Time, unordered.RootElement));
        Assert.Contains("out of order", order.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonicalize_renames_aliases_for_a_report_target()
    {
        var criteria = JsonDocument.Parse(
            """
            {"filters":[{"field":"assignedAt","operator":">","value":"2026-01-01T00:00:00Z"}]}
            """);
        var stored = ReportFilterParser.Canonicalize(FilterTarget.ReportsFunnel, criteria.RootElement);
        Assert.Equal("createdAtUtc", stored.RootElement.GetProperty("filters")[0].GetProperty("field").GetString());
    }
}
