using InterviewQuiz.Host.Mail;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Mail;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Host.IntegrationTests;

public sealed class InviteMailerTests
{
    [Fact]
    public async Task File_sink_writes_the_email_and_does_not_log_the_token()
    {
        var root = Path.Combine(Path.GetTempPath(), "iq-invite-mail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var logs = new ListLogger<FileSinkInviteMailer>();
            var mailer = new FileSinkInviteMailer(
                Options.Create(new InviteMailOptions
                {
                    SinkDirectory = root,
                    FromAddress = "invites@localhost",
                    FromDisplayName = "Interview Quiz",
                    Subject = "Your interview quiz invite"
                }),
                new FakeEnv(),
                new FixedClock(DateTimeOffset.Parse("2026-10-15T12:00:00Z")),
                logs);
            const string token = "opaque-token-value";
            var url = $"http://localhost:4200/attempt?token={token}";
            var assignmentId = Guid.Parse("7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001");

            var result = await mailer.SendAsync(
                new InviteMailMessage(assignmentId, "candidate@example.com", url, DateTimeOffset.Parse("2026-10-20T12:00:00Z"), false),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            var file = Assert.Single(Directory.GetFiles(root, "*.eml"));
            var body = await File.ReadAllTextAsync(file);
            Assert.Contains(url, body, StringComparison.Ordinal);
            var written = string.Join('\n', logs.Messages);
            Assert.DoesNotContain(token, written, StringComparison.Ordinal);
            Assert.DoesNotContain(url, written, StringComparison.Ordinal);
            Assert.Contains(assignmentId.ToString(), written, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Smtp_without_a_host_fails_without_opening_a_socket()
    {
        var mailer = new SmtpInviteMailer(
            Options.Create(new InviteMailOptions { Smtp = new SmtpInviteOptions { Host = "" } }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SmtpInviteMailer>.Instance);

        var result = await mailer.SendAsync(
            new InviteMailMessage(Guid.NewGuid(), "candidate@example.com", "http://localhost:4200/attempt?token=abc", null, false),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("SMTP host is not configured.", result.Error);
    }

    [Fact]
    public void Redactor_strips_token_query_values()
    {
        var redacted = InviteSecretRedactor.Redact("failure for http://localhost/attempt?token=abc+def&x=1");
        Assert.DoesNotContain("abc", redacted, StringComparison.Ordinal);
        Assert.Contains("token=[redacted]", redacted, StringComparison.Ordinal);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class FakeEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
