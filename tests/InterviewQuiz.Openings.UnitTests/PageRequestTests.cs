using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Openings.UnitTests;

public sealed class PageRequestTests
{
    [Fact]
    public void Caps_page_size()
    {
        var page = new PageRequest(1, 10_000);
        Assert.Equal(PageRequest.MaxPageSize, page.PageSize);
        Assert.Equal(0, page.Skip);
    }

    [Fact]
    public void Defaults_invalid_values()
    {
        var page = new PageRequest(0, 0);
        Assert.Equal(1, page.Page);
        Assert.Equal(PageRequest.DefaultPageSize, page.PageSize);
    }
}
