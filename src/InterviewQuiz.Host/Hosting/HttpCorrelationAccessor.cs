using InterviewQuiz.Kernel.Assignments;

namespace InterviewQuiz.Host.Hosting;

public sealed class HttpCorrelationAccessor : ICorrelationAccessor
{
    private readonly IHttpContextAccessor _http;

    public HttpCorrelationAccessor(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string Current
    {
        get
        {
            var value = _http.HttpContext?.TraceIdentifier;
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        }
    }
}
