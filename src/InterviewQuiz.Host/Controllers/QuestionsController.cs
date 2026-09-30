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
[Route("api/questions")]
public sealed class QuestionsController : ControllerBase
{
    private readonly IBankQuestionService _questions;

    public QuestionsController(IBankQuestionService questions)
    {
        _questions = questions;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Catalog.QuestionsRead)]
    public async Task<ActionResult<PagedResult<BankQuestionResponse>>> List(
        [FromQuery] BankQuestionListQuery query,
        CancellationToken cancellationToken)
    {
        var criteria = BankQuestionListCriteria.FromQuery(query);
        var canListArchived = User.HasClaim(
            PermissionClaims.Permission,
            PermissionCodes.Catalog.QuestionsWrite);
        var page = new PageRequest(query.Page, query.PageSize);
        var result = await _questions.ListAsync(criteria, page, canListArchived, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasAnyPermission(PermissionCodes.Catalog.QuestionsRead, PermissionCodes.Catalog.QuestionsWrite)]
    public async Task<ActionResult<BankQuestionResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var question = await _questions.GetAsync(id, cancellationToken);
        Response.Headers.ETag = $"\"{question.RowVersion}\"";
        return Ok(question);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Catalog.QuestionsWrite)]
    public async Task<ActionResult<BankQuestionResponse>> Create(
        [FromBody] CreateBankQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var question = await _questions.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = question.Id }, question);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Catalog.QuestionsWrite)]
    public async Task<ActionResult<BankQuestionResponse>> Update(
        Guid id,
        [FromBody] UpdateBankQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var question = await _questions.UpdateAsync(id, request, cancellationToken);
        Response.Headers.ETag = $"\"{question.RowVersion}\"";
        return Ok(question);
    }

    [HttpPost("{id:guid}/archive")]
    [HasPermission(PermissionCodes.Catalog.QuestionsWrite)]
    public async Task<ActionResult<BankQuestionResponse>> Archive(
        Guid id,
        CancellationToken cancellationToken)
    {
        var question = await _questions.ArchiveAsync(id, cancellationToken);
        return Ok(question);
    }

    [HttpPost("{id:guid}/unarchive")]
    [HasPermission(PermissionCodes.Catalog.QuestionsWrite)]
    public async Task<ActionResult<BankQuestionResponse>> Unarchive(
        Guid id,
        CancellationToken cancellationToken)
    {
        var question = await _questions.UnarchiveAsync(id, cancellationToken);
        return Ok(question);
    }
}
