namespace InterviewQuiz.Catalog.Application.Contracts;

public class UpdateTemplateMetadataRequest
{
    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public int ExpectedExperienceYears { get; set; }

    public Dictionary<string, string>? Tags { get; set; }

    public uint RowVersion { get; set; }

    public string? ChangeNote { get; set; }
}

public sealed class SaveTemplateContentRequest : UpdateTemplateMetadataRequest
{
    public IReadOnlyList<QuestionRequest>? Questions { get; set; }
}
