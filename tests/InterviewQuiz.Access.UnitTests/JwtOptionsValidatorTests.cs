using InterviewQuiz.Access.Authentication;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Access.UnitTests;

public sealed class JwtOptionsValidatorTests
{
    [Fact]
    public void Accepts_default_candidate_lifetime()
    {
        var result = new JwtOptionsValidator().Validate(null, Valid());
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(181)]
    public void Rejects_candidate_lifetime_out_of_range(int minutes)
    {
        var options = Valid();
        options.CandidateAccessTokenMinutes = minutes;
        var result = new JwtOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures ?? [],
            failure => failure.Contains("Jwt__CandidateAccessTokenMinutes", StringComparison.Ordinal));
    }

    private static JwtOptions Valid()
        => new()
        {
            Issuer = "InterviewQuiz.Tests",
            Audience = "InterviewQuiz.Tests",
            SigningKey = "InterviewQuiz-Testing-Signing-Key-Not-For-Production!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
            CandidateAccessTokenMinutes = 60
        };
}
