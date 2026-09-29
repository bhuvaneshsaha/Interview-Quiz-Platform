using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    private readonly ICurrentUserQuery _currentUser;

    public MeController(ICurrentUserQuery currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet]
    public ActionResult<MeResponse> Get()
        => Ok(_currentUser.GetMe(User));

    [HttpGet("permissions")]
    public ActionResult<IReadOnlyList<string>> Permissions()
        => Ok(_currentUser.GetPermissions(User));
}
