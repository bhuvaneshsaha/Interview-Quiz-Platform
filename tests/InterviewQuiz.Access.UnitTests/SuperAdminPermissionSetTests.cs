using InterviewQuiz.Access.Application;
using InterviewQuiz.Kernel.Permissions;

namespace InterviewQuiz.Access.UnitTests;

public sealed class SuperAdminPermissionSetTests
{
    [Fact]
    public void Codes_include_a_new_catalog_entry_and_exclude_candidate_attempt()
    {
        var extra = new PermissionDescriptor("reports.export", "Export reports", "reports");
        var catalog = PermissionCodes.All.Append(extra);

        var codes = SuperAdminPermissionSet.Codes(catalog);

        Assert.Contains(extra.Code, codes);
        Assert.Contains(PermissionCodes.Access.RolesManage, codes);
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, codes);
        Assert.Equal(codes.Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal), codes);
    }

    [Fact]
    public void Default_catalog_covers_every_employee_code()
    {
        var codes = SuperAdminPermissionSet.Codes();

        Assert.Equal(
            PermissionCodes.All.Count(permission => permission.Code != PermissionCodes.Candidate.AttemptParticipate),
            codes.Count);
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, codes);
    }
}
