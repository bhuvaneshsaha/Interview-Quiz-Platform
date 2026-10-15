using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Delivery.Application.Contracts;

public sealed class AssignmentListQuery
{
    public Guid? OpeningId { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = PageRequest.DefaultPageSize;
}
