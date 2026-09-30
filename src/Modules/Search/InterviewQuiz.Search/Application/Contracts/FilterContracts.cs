using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using InterviewQuiz.Search.Domain;

namespace InterviewQuiz.Search.Application.Contracts;

public sealed class CreateFilterRequest
{
    [Required]
    [MaxLength(SavedFilter.NameMaxLength)]
    public string Name { get; set; } = "";

    [Required]
    public FilterTarget? Target { get; set; }

    public JsonElement Criteria { get; set; }
}

public sealed class UpdateFilterRequest
{
    [Required]
    [MaxLength(SavedFilter.NameMaxLength)]
    public string Name { get; set; } = "";

    [Required]
    public FilterTarget? Target { get; set; }

    public JsonElement Criteria { get; set; }
}

public sealed class ShareFilterRequest
{
    [Required]
    public FilterShareMode? ShareMode { get; set; }

    public List<string>? UserIds { get; set; }
}

public sealed class FilterResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required FilterTarget Target { get; init; }
    public required JsonElement Criteria { get; init; }
    public required string OwnerUserId { get; init; }
    public required FilterShareMode ShareMode { get; init; }
    public required IReadOnlyList<string> SharedWithUserIds { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class FilterListQuery
{
    public FilterTarget? Target { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = Kernel.Pagination.PageRequest.DefaultPageSize;
}
