using System.Text.Json;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Host.Composition;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Search.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
[HasPermission(PermissionCodes.Evaluation.ReportsRead)]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportingComposer _reports;

    public ReportsController(ReportingComposer reports)
    {
        _reports = reports;
    }

    [HttpGet("funnel")]
    public Task<FunnelReportResponse> Funnel([FromQuery] ReportListQuery query, CancellationToken cancellationToken)
        => _reports.FunnelAsync(ParseCriteria(query.Criteria), query.Page, query.PageSize, cancellationToken);

    [HttpGet("scores")]
    public Task<ScoresReportResponse> Scores([FromQuery] ReportListQuery query, CancellationToken cancellationToken)
        => _reports.ScoresAsync(ParseCriteria(query.Criteria), query.Page, query.PageSize, cancellationToken);

    [HttpGet("time")]
    public Task<TimeReportResponse> Time([FromQuery] ReportListQuery query, CancellationToken cancellationToken)
        => _reports.TimeAsync(ParseCriteria(query.Criteria), query.Page, query.PageSize, cancellationToken);

    [HttpGet("questions")]
    public Task<QuestionsReportResponse> Questions([FromQuery] ReportListQuery query, CancellationToken cancellationToken)
        => _reports.QuestionsAsync(ParseCriteria(query.Criteria), query.Page, query.PageSize, cancellationToken);

    [HttpGet("candidates")]
    public Task<CandidatesReportResponse> Candidates([FromQuery] ReportListQuery query, CancellationToken cancellationToken)
        => _reports.CandidatesAsync(ParseCriteria(query.Criteria), query.Page, query.PageSize, cancellationToken);

    [HttpGet("pending-review")]
    public Task<PendingReviewReportResponse> PendingReview([FromQuery] ReportListQuery query, CancellationToken cancellationToken)
        => _reports.PendingReviewAsync(ParseCriteria(query.Criteria), query.Page, query.PageSize, cancellationToken);

    [HttpGet("{reportKey}/export")]
    public async Task<IActionResult> Export(string reportKey, [FromQuery] string? criteria, CancellationToken cancellationToken)
    {
        if (!ReportKeys.TryParseRoute(reportKey, out var key))
        {
            throw new EntityNotFoundException("Report", reportKey);
        }

        var bytes = await _reports.ExportAsync(key, ParseCriteria(criteria), cancellationToken);
        return File(bytes, "text/csv; charset=utf-8", $"{ReportKeys.Route(key)}.csv");
    }

    private static JsonElement ParseCriteria(string? criteria)
    {
        if (string.IsNullOrWhiteSpace(criteria))
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }

        try
        {
            using var document = JsonDocument.Parse(criteria);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new DomainException("Criteria must be a JSON object.", ex);
        }
    }
}

public sealed class ReportListQuery
{
    public string? Criteria { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
