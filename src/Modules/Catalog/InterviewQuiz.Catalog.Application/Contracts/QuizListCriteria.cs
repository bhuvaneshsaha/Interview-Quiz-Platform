namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class QuizListCriteria
{
    public Guid? OpeningId { get; init; }
}

public sealed class QuizListQuery
{
    public Guid? OpeningId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = Kernel.Pagination.PageRequest.DefaultPageSize;
}
