using System.ComponentModel.DataAnnotations;
using InterviewQuiz.Openings.Domain;

namespace InterviewQuiz.Openings.Application.Contracts;

public sealed class OpeningFieldDefinitionDto
{
    [Required]
    [MaxLength(OpeningFieldDefinition.KeyMaxLength)]
    public string Key { get; set; } = "";

    [Required]
    [MaxLength(OpeningFieldDefinition.DisplayNameMaxLength)]
    public string DisplayName { get; set; } = "";

    [Range(0, 10_000)]
    public int SortOrder { get; set; }
}

public sealed class ReplaceOpeningFieldDefinitionsRequest
{
    [Required]
    public List<OpeningFieldDefinitionDto> Items { get; set; } = [];
}

public sealed class OpeningFieldDefinitionResponse
{
    public required Guid Id { get; init; }
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required int SortOrder { get; init; }
}
