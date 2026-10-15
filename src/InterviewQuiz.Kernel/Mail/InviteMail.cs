using System.Text.RegularExpressions;

namespace InterviewQuiz.Kernel.Mail;

public interface IInviteMailer
{
    Task<InviteMailResult> SendAsync(InviteMailMessage message, CancellationToken cancellationToken);
}

public sealed record InviteMailMessage(
    Guid AssignmentId,
    string ToEmail,
    string InviteUrl,
    DateTimeOffset? ExpiresAt,
    bool Resend);

public sealed record InviteMailResult(bool Succeeded, string? Error);

public static class InviteSecretRedactor
{
    private static readonly Regex TokenQuery = new(
        @"(?i)(token=)[^&\s""']+",
        RegexOptions.Compiled);

    public static string Redact(string? value, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var redacted = TokenQuery.Replace(value, "$1[redacted]");
        redacted = redacted.Replace("\r", " ").Replace("\n", " ").Trim();
        if (redacted.Length > maxLength)
        {
            redacted = redacted[..maxLength];
        }

        return redacted;
    }
}

public static class InviteMailContent
{
    public static string Render(InviteMailMessage message)
    {
        var expiry = message.ExpiresAt is { } at
            ? at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")
            : "no fixed expiry";
        var intro = message.Resend
            ? "This is a resend of your interview quiz invite."
            : "You have been invited to an interview quiz.";

        return
            $"""
            {intro}

            Open this link when you are ready:
            {message.InviteUrl}

            Expiry: {expiry}

            You can open the link again until you submit, unless it expires or the recruiter replaces it.
            """;
    }
}
