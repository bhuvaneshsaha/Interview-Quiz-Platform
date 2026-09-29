using System.Text.Json;
using System.Text.Json.Serialization;

namespace InterviewQuiz.Openings.Application.Contracts;

/// <summary>
/// List filter shape stored later by Search as saved-filter criteria JSON.
/// Also bindable from query-string fields on GET /api/openings.
/// </summary>
public sealed class OpeningListCriteria
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string? Owner { get; set; }
    public int? ExperienceMinYears { get; set; }
    public int? ExperienceMaxYears { get; set; }
    public DateOnly? StartDateFrom { get; set; }
    public DateOnly? StartDateTo { get; set; }
    public DateOnly? ExpectedCloseDateFrom { get; set; }
    public DateOnly? ExpectedCloseDateTo { get; set; }
    public Dictionary<string, string>? Tags { get; set; }

    public static OpeningListCriteria FromQuery(OpeningListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Criteria))
        {
            var parsed = JsonSerializer.Deserialize<OpeningListCriteria>(query.Criteria, JsonOptions);
            if (parsed is null)
            {
                throw new Kernel.Exceptions.DomainException("Opening list criteria JSON is invalid.");
            }

            return parsed;
        }

        Dictionary<string, string>? tags = null;
        if (!string.IsNullOrWhiteSpace(query.Tags))
        {
            tags = JsonSerializer.Deserialize<Dictionary<string, string>>(query.Tags, JsonOptions);
            if (tags is null)
            {
                throw new Kernel.Exceptions.DomainException("Opening list tags JSON is invalid.");
            }
        }

        return new OpeningListCriteria
        {
            Owner = query.Owner,
            ExperienceMinYears = query.ExperienceMinYears,
            ExperienceMaxYears = query.ExperienceMaxYears,
            StartDateFrom = query.StartDateFrom,
            StartDateTo = query.StartDateTo,
            ExpectedCloseDateFrom = query.ExpectedCloseDateFrom,
            ExpectedCloseDateTo = query.ExpectedCloseDateTo,
            Tags = tags
        };
    }
}

public sealed class OpeningListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = Kernel.Pagination.PageRequest.DefaultPageSize;
    public string? Owner { get; set; }
    public int? ExperienceMinYears { get; set; }
    public int? ExperienceMaxYears { get; set; }
    public DateOnly? StartDateFrom { get; set; }
    public DateOnly? StartDateTo { get; set; }
    public DateOnly? ExpectedCloseDateFrom { get; set; }
    public DateOnly? ExpectedCloseDateTo { get; set; }

    /// <summary>JSON object of tag key/value pairs, e.g. {"Client":"Acme"}.</summary>
    public string? Tags { get; set; }

    /// <summary>Full criteria JSON (same shape Search will persist for saved filters).</summary>
    public string? Criteria { get; set; }
}
