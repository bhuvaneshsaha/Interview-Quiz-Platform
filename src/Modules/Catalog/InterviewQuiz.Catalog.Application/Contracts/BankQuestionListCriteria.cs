using System.Text.Json;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.Application.Contracts;

/// <summary>
/// List filter shape for GET /api/questions (and saved-filter criteria JSON later).
/// </summary>
public sealed class BankQuestionListCriteria
{
    public string? Keyword { get; set; }
    public QuestionType? Type { get; set; }
    public int? ExperienceMinYears { get; set; }
    public int? ExperienceMaxYears { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public bool Archived { get; set; }

    public static BankQuestionListCriteria FromQuery(BankQuestionListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Criteria))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<BankQuestionListCriteria>(
                    query.Criteria,
                    CatalogJson.SerializerOptions);
                if (parsed is null)
                {
                    throw new DomainException("Question list criteria JSON is invalid.");
                }

                return parsed;
            }
            catch (JsonException ex)
            {
                throw new DomainException("Question list criteria JSON is invalid.", ex);
            }
        }

        Dictionary<string, string>? tags = null;
        if (!string.IsNullOrWhiteSpace(query.Tags))
        {
            try
            {
                tags = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    query.Tags,
                    CatalogJson.SerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new DomainException("Question list tags JSON is invalid.", ex);
            }

            if (tags is null)
            {
                throw new DomainException("Question list tags JSON is invalid.");
            }
        }

        return new BankQuestionListCriteria
        {
            Keyword = query.Keyword,
            Type = ParseType(query.Type),
            ExperienceMinYears = query.ExperienceMinYears,
            ExperienceMaxYears = query.ExperienceMaxYears,
            Tags = tags,
            Archived = query.Archived
        };
    }

    private static QuestionType? ParseType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QuestionType>(
                JsonSerializer.Serialize(raw.Trim()),
                CatalogJson.SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new DomainException($"Unknown question type '{raw}'.", ex);
        }
    }
}

public sealed class BankQuestionListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = Kernel.Pagination.PageRequest.DefaultPageSize;
    public string? Keyword { get; set; }

    /// <summary>CamelCase question type, e.g. multipleChoiceSingle.</summary>
    public string? Type { get; set; }

    public int? ExperienceMinYears { get; set; }
    public int? ExperienceMaxYears { get; set; }

    /// <summary>JSON object of tag key/value pairs, e.g. {"Role":"Backend"}.</summary>
    public string? Tags { get; set; }

    /// <summary>Full criteria JSON (<see cref="BankQuestionListCriteria"/>).</summary>
    public string? Criteria { get; set; }

    /// <summary>When true, list only archived items (requires questions.write).</summary>
    public bool Archived { get; set; }
}
