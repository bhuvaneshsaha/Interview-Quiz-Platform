using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
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

    public QuizzesController(IQuizService quizzes)
    {
        _quizzes = quizzes;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Catalog.QuizzesRead)]
    public async Task<ActionResult<PagedResult<QuizResponse>>> List(
        [FromQuery] QuizListQuery query,
        CancellationToken cancellationToken)
    {
        var criteria = new QuizListCriteria { OpeningId = query.OpeningId };
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
}
