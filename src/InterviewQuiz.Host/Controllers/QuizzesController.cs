using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Scoring;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/quizzes")]
public sealed class QuizzesController : ControllerBase
{
    private readonly IQuizService _quizzes;
    private readonly ITemplateService _templates;
    private readonly IQuizSnapshotReader _snapshots;

    public QuizzesController(IQuizService quizzes, ITemplateService templates, IQuizSnapshotReader snapshots)
    {
        _quizzes = quizzes;
        _templates = templates;
        _snapshots = snapshots;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Catalog.QuizzesRead)]
    public async Task<ActionResult<PagedResult<QuizResponse>>> List(
        [FromQuery] QuizListQuery query,
        CancellationToken cancellationToken)
    {
        var criteria = QuizListCriteria.FromQuery(query);
        var page = new PageRequest(query.Page, query.PageSize);
        var result = await _quizzes.ListAsync(criteria, page, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasAnyPermission(PermissionCodes.Catalog.QuizzesRead, PermissionCodes.Catalog.QuizzesWrite)]
    public async Task<ActionResult<QuizResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var quiz = await _quizzes.GetAsync(id, cancellationToken);
        Response.Headers.ETag = $"\"{quiz.RowVersion}\"";
        return Ok(quiz);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Catalog.QuizzesWrite)]
    public async Task<ActionResult<QuizResponse>> Create(
        [FromBody] CreateQuizRequest request,
        CancellationToken cancellationToken)
    {
        var quiz = await _quizzes.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = quiz.Id }, quiz);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Catalog.QuizzesWrite)]
    public async Task<ActionResult<QuizResponse>> Update(
        Guid id,
        [FromBody] UpdateQuizRequest request,
        CancellationToken cancellationToken)
    {
        request.Id = id;
        var quiz = await _quizzes.UpdateAsync(request, cancellationToken);
        Response.Headers.ETag = $"\"{quiz.RowVersion}\"";
        return Ok(quiz);
    }

    [HttpPost("{quizId:guid}/include-questions")]
    [HasPermission(PermissionCodes.Catalog.QuizzesWrite)]
    [HasPermission(PermissionCodes.Catalog.QuestionsRead)]
    public async Task<ActionResult<QuizResponse>> IncludeQuestions(
        Guid quizId,
        [FromBody] IncludeQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        var quiz = await _quizzes.IncludeQuestionsAsync(quizId, request, cancellationToken);
        Response.Headers.ETag = $"\"{quiz.RowVersion}\"";
        return Ok(quiz);
    }

    [HttpPost("{id:guid}/publish-template")]
    [HasPermission(PermissionCodes.Catalog.TemplatesWrite)]
    public async Task<ActionResult<PublishTemplateResponse>> PublishTemplate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.FindUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var published = await _templates.PublishAsync(id, userId, cancellationToken);
        return Created($"/api/templates/{published.TemplateId}", published);
    }

    [HttpGet("{id:guid}/candidate-preview")]
    [HasPermission(PermissionCodes.Catalog.QuizzesRead)]
    public async Task<ActionResult<CandidateQuizPreviewResponse>> Preview(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _quizzes.PreviewAsync(id, cancellationToken));

    [HttpPost("candidate-preview")]
    [HasPermission(PermissionCodes.Catalog.QuizzesRead)]
    public async Task<ActionResult<CandidateQuizPreviewResponse>> PreviewDraft(
        [FromBody] CreateQuizRequest request,
        CancellationToken cancellationToken)
        => Ok(await _quizzes.PreviewDraftAsync(request, cancellationToken));

    [HttpPost("{id:guid}/candidate-preview/score")]
    [HasPermission(PermissionCodes.Catalog.QuizzesRead)]
    public async Task<ActionResult<CandidatePreviewScoreResponse>> ScorePreview(
        Guid id,
        [FromBody] PreviewScoreRequest request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _snapshots.GetSnapshotAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Quiz", id);
        return Ok(CandidatePreviewScorer.Score(snapshot, request.Answers));
    }

    [HttpPost("candidate-preview/score")]
    [HasPermission(PermissionCodes.Catalog.QuizzesRead)]
    public ActionResult<CandidatePreviewScoreResponse> ScoreDraftPreview(
        [FromBody] DraftPreviewScoreRequest request)
    {
        var snapshot = _quizzes.BuildDraftSnapshot(request.Quiz);
        return Ok(CandidatePreviewScorer.Score(snapshot, request.Answers));
    }
}

public sealed class DraftPreviewScoreRequest
{
    public CreateQuizRequest Quiz { get; set; } = new();

    public List<AnswerDto> Answers { get; set; } = [];
}
