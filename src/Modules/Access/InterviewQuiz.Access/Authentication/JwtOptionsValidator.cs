using Microsoft.Extensions.Options;

namespace InterviewQuiz.Access.Authentication;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add("Jwt__Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add("Jwt__Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey)
            || options.SigningKey.Length < JwtOptions.MinimumSigningKeyLength)
        {
            errors.Add(
                "Jwt__SigningKey is required and must be at least 32 characters. " +
                "Set it via environment, an OS-protected file, or Vault. " +
                "Never use the Development key in Production.");
        }

        if (options.AccessTokenMinutes is < 1 or > 180)
        {
            errors.Add("Jwt__AccessTokenMinutes must be between 1 and 180.");
        }

        if (options.RefreshTokenDays is < 1 or > 90)
        {
            errors.Add("Jwt__RefreshTokenDays must be between 1 and 90.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
