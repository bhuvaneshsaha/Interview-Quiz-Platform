namespace InterviewQuiz.Kernel.Users;

/// <summary>
/// Active employee directory for opening owner and handler pickers.
/// An employee can sign in with a password and holds at least one employee role.
/// Candidate identities, users with no employee role, and disabled users are omitted.
/// Does not grant permissions. Handlers are not an authorization rule.
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
