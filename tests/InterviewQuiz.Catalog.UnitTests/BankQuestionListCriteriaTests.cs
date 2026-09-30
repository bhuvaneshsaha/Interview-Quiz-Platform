using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.UnitTests;

public sealed class BankQuestionListCriteriaTests
{
    [Fact]
    public void FromQuery_maps_flat_fields()
    {
        var criteria = BankQuestionListCriteria.FromQuery(new BankQuestionListQuery
        {
            Keyword = "REST",
            Type = "shortText",
            ExperienceMinYears = 1,
            ExperienceMaxYears = 8,
            Tags = """{"Topic":"HTTP"}""",
            Archived = false
        });

        Assert.Equal("REST", criteria.Keyword);
        Assert.Equal(QuestionType.ShortText, criteria.Type);
        Assert.Equal(1, criteria.ExperienceMinYears);
        Assert.Equal(8, criteria.ExperienceMaxYears);
        Assert.Equal("HTTP", criteria.Tags!["Topic"]);
        Assert.False(criteria.Archived);
    }

    [Fact]
    public void FromQuery_criteria_json_wins_over_flat_fields()
    {
        var criteria = BankQuestionListCriteria.FromQuery(new BankQuestionListQuery
        {
            Keyword = "ignored",
            Type = "trueFalse",
            Archived = false,
            Criteria = """{"keyword":"FromJson","type":"multipleChoiceSingle","archived":true}"""
        });

        Assert.Equal("FromJson", criteria.Keyword);
        Assert.Equal(QuestionType.MultipleChoiceSingle, criteria.Type);
        Assert.True(criteria.Archived);
    }

    [Fact]
    public void FromQuery_rejects_invalid_criteria_json()
    {
        var ex = Assert.Throws<DomainException>(() =>
            BankQuestionListCriteria.FromQuery(new BankQuestionListQuery { Criteria = "{not-json" }));
        Assert.Equal("Question list criteria JSON is invalid.", ex.Message);
    }

    [Fact]
    public void FromQuery_rejects_unknown_type()
    {
        var ex = Assert.Throws<DomainException>(() =>
            BankQuestionListCriteria.FromQuery(new BankQuestionListQuery { Type = "code" }));
        Assert.Contains("Unknown question type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
