namespace InterviewQuiz.Kernel.Pagination;

public sealed class PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; }
    public int PageSize { get; }

    public PageRequest(int page, int pageSize)
    {
        Page = page < 1 ? 1 : page;
        PageSize = pageSize < 1
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);
    }

    public int Skip => (Page - 1) * PageSize;
}
