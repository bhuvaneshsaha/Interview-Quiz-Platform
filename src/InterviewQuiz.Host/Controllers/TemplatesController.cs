using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Scoring;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/templates")]
public sealed class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templates;

    public TemplatesController(ITemplateService templates)
    {
        _templates = templates;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Catalog.TemplatesRead)]
    public async Task<ActionResult<PagedResult<TemplateSummaryResponse>>> List(
        [FromQuery] TemplateListQuery query,
        CancellationToken cancellationToken)
    {
        var criteria = TemplateListCriteria.FromQuery(query);
        var page = new PageRequest(query.Page, query.PageSize);
        var result = await _templates.ListAsync(criteria, page, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.Catalog.TemplatesRead)]
    public async Task<ActionResult<TemplateResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var template = await _templates.GetAsync(id, cancellationToken);
        return Ok(template);
    }

    [HttpGet("{id:guid}/versions")]
    [HasPermission(PermissionCodes.Catalog.TemplatesRead)]
    public async Task<ActionResult<PagedResult<TemplateVersionSummaryResponse>>> ListVersions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _templates.ListVersionsAsync(id, new PageRequest(page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}")]
    [HasPermission(PermissionCodes.Catalog.TemplatesRead)]
    public async Task<ActionResult<TemplateVersionResponse>> GetVersion(
        Guid id,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await _templates.GetVersionAsync(id, versionId, cancellationToken);
        return Ok(version);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/clone")]
    [HasPermission(PermissionCodes.Catalog.QuizzesWrite)]
    [HasPermission(PermissionCodes.Catalog.TemplatesRead)]
    public async Task<ActionResult<QuizResponse>> Clone(
        Guid id,
        Guid versionId,
        [FromBody] CloneTemplateVersionRequest request,
        CancellationToken cancellationToken)
    {
        var quiz = await _templates.CloneAsync(id, versionId, request, cancellationToken);
        return Created($"/api/quizzes/{quiz.Id}", quiz);
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}/candidate-preview")]
    [HasPermission(PermissionCodes.Catalog.TemplatesRead)]
    public async Task<ActionResult<CandidateQuizPreviewResponse>> PreviewVersion(
        Guid id,
        Guid versionId,
        CancellationToken cancellationToken)
        => Ok(await _templates.PreviewVersionAsync(id, versionId, cancellationToken));

    [HttpPost("{id:guid}/versions/{versionId:guid}/candidate-preview/score")]
    [HasPermission(PermissionCodes.Catalog.TemplatesRead)]
    public async Task<ActionResult<CandidatePreviewScoreResponse>> ScorePreview(
        Guid id,
        Guid versionId,
        [FromBody] PreviewScoreRequest request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _templates.GetVersionSnapshotAsync(id, versionId, cancellationToken);
        return Ok(CandidatePreviewScorer.Score(snapshot, request.Answers));
    }
}
