namespace InterviewQuiz.Kernel.Users;

/// <summary>
/// Staff directory for opening owner and handler pickers. Does not grant
/// permissions. Handlers are not an authorization rule.
/// </summary>
public interface IAssignableUserLookup
{
    Task<IReadOnlyList<AssignableUserDto>> SearchAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, AssignableUserDto>> GetByIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken);
}
