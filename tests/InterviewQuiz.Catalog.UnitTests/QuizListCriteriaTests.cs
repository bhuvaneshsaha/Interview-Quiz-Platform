using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.UnitTests;

public sealed class QuizListCriteriaTests
{
    [Fact]
    public void FromQuery_maps_flat_fields()
    {
        var criteria = QuizListCriteria.FromQuery(new QuizListQuery
        {
            OpeningId = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001"),
            Keyword = "Backend",
            ExperienceMinYears = 3,
            ExperienceMaxYears = 8,
            Tags = """{"Role":"Backend"}"""
        });

        Assert.Equal(Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001"), criteria.OpeningId);
        Assert.Equal("Backend", criteria.Keyword);
        Assert.Equal(3, criteria.ExperienceMinYears);
        Assert.Equal(8, criteria.ExperienceMaxYears);
        Assert.Equal("Backend", criteria.Tags!["Role"]);
    }

    [Fact]
    public void FromQuery_criteria_json_wins_over_flat_fields()
    {
        var criteria = QuizListCriteria.FromQuery(new QuizListQuery
        {
            OpeningId = Guid.NewGuid(),
            Keyword = "ignored",
            Criteria = """{"keyword":"FromJson","experienceMinYears":1,"openingId":"3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001"}"""
        });

        Assert.Equal("FromJson", criteria.Keyword);
        Assert.Equal(1, criteria.ExperienceMinYears);
        Assert.Equal(Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001"), criteria.OpeningId);
    }

    [Fact]
    public void FromQuery_rejects_invalid_criteria_json()
    {
        var ex = Assert.Throws<DomainException>(() =>
            QuizListCriteria.FromQuery(new QuizListQuery { Criteria = "{not-json" }));
        Assert.Equal("Quiz list criteria JSON is invalid.", ex.Message);
    }

    [Fact]
    public void Template_fromQuery_maps_keyword_and_tags()
    {
        var criteria = TemplateListCriteria.FromQuery(new TemplateListQuery
        {
            Keyword = "Backend",
            ExperienceMinYears = 4,
            Tags = """{"Client":"Internal"}"""
        });

        Assert.Equal("Backend", criteria.Keyword);
        Assert.Equal(4, criteria.ExperienceMinYears);
        Assert.Equal("Internal", criteria.Tags!["Client"]);
    }

    [Fact]
    public void Template_fromQuery_rejects_invalid_tags_json()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TemplateListCriteria.FromQuery(new TemplateListQuery { Tags = "[]" }));
        Assert.Equal("Template list tags JSON is invalid.", ex.Message);
    }
}
