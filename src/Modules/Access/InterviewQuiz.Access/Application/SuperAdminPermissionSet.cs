using InterviewQuiz.Kernel.Permissions;

namespace InterviewQuiz.Access.Application;

/// <summary>
/// Super Admin receives every catalog code except <see cref="PermissionCodes.Candidate.AttemptParticipate"/>.
/// Computed from the catalog passed in, so a new code is included without editing this list.
/// </summary>
public static class SuperAdminPermissionSet
{
    public static IReadOnlyList<string> Codes(IEnumerable<PermissionDescriptor>? catalog = null)
    {
        var source = catalog ?? PermissionCodes.All;
        return source
            .Select(permission => permission.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Where(code => !string.Equals(
                code,
                PermissionCodes.Candidate.AttemptParticipate,
                StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
    }
}
