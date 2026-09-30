using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.Application.Contracts;

/// <summary>
/// List filter shape stored by Search as saved-filter criteria JSON (target: templates).
/// Also bindable from query-string fields on GET /api/templates.
/// </summary>
public sealed class TemplateListCriteria
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string? Keyword { get; set; }
    public int? ExperienceMinYears { get; set; }
    public int? ExperienceMaxYears { get; set; }
    public Dictionary<string, string>? Tags { get; set; }

    public static TemplateListCriteria FromQuery(TemplateListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Criteria))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<TemplateListCriteria>(query.Criteria, JsonOptions);
                if (parsed is null)
                {
                    throw new DomainException("Template list criteria JSON is invalid.");
                }

                return parsed;
            }
            catch (JsonException ex)
            {
                throw new DomainException("Template list criteria JSON is invalid.", ex);
            }
        }

        Dictionary<string, string>? tags = null;
        if (!string.IsNullOrWhiteSpace(query.Tags))
        {
            try
            {
                tags = JsonSerializer.Deserialize<Dictionary<string, string>>(query.Tags, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new DomainException("Template list tags JSON is invalid.", ex);
            }

            if (tags is null)
            {
                throw new DomainException("Template list tags JSON is invalid.");
            }
        }

        return new TemplateListCriteria
        {
            Keyword = query.Keyword,
            ExperienceMinYears = query.ExperienceMinYears,
            ExperienceMaxYears = query.ExperienceMaxYears,
            Tags = tags
        };
    }
}

public sealed class TemplateListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = Kernel.Pagination.PageRequest.DefaultPageSize;
    public string? Keyword { get; set; }
    public int? ExperienceMinYears { get; set; }
    public int? ExperienceMaxYears { get; set; }

    /// <summary>JSON object of tag key/value pairs, e.g. {"Role":"Backend"}.</summary>
    public string? Tags { get; set; }

    /// <summary>Full criteria JSON (same shape Search persists for saved filters).</summary>
    public string? Criteria { get; set; }
}
