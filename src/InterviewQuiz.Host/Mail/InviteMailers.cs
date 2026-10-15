using System.Net;
using System.Net.Mail;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Mail;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Host.Mail;

public sealed class InviteMailOptions
{
    public const string SectionName = "InviteMail";

    /// <summary>Sink (default) writes the message to disk. Smtp sends through the configured server.</summary>
    public string Mode { get; set; } = "Sink";

    public string FromAddress { get; set; } = "invites@localhost";

    public string FromDisplayName { get; set; } = "Interview Quiz";

    public string Subject { get; set; } = "Your interview quiz invite";

    /// <summary>Empty uses {content root}/App_Data/invite-mail.</summary>
    public string SinkDirectory { get; set; } = "";

    public SmtpInviteOptions Smtp { get; set; } = new();
}

public sealed class SmtpInviteOptions
{
    public string Host { get; set; } = "";

    public int Port { get; set; } = 1025;

    public bool UseSsl { get; set; }

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";
}

public static class InviteMailRegistration
{
    public static IServiceCollection AddInviteMail(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<InviteMailOptions>(configuration.GetSection(InviteMailOptions.SectionName));

        if (environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<RecordingInviteMailer>();
            services.AddSingleton<IInviteMailer>(sp => sp.GetRequiredService<RecordingInviteMailer>());
            return services;
        }

        var mode = configuration[$"{InviteMailOptions.SectionName}:Mode"];
        if (string.Equals(mode, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IInviteMailer, SmtpInviteMailer>();
        }
        else
        {
            services.AddSingleton<IInviteMailer, FileSinkInviteMailer>();
        }

        return services;
    }
}

/// <summary>
/// Development sink. The file is the email. Log lines name the assignment and file only.
/// </summary>
public sealed class FileSinkInviteMailer : IInviteMailer
{
    private readonly IOptions<InviteMailOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly IClock _clock;
    private readonly ILogger<FileSinkInviteMailer> _logger;

    public FileSinkInviteMailer(
        IOptions<InviteMailOptions> options,
        IHostEnvironment environment,
        IClock clock,
        ILogger<FileSinkInviteMailer> logger)
    {
        _options = options;
        _environment = environment;
        _clock = clock;
        _logger = logger;
    }

    public async Task<InviteMailResult> SendAsync(InviteMailMessage message, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var directory = string.IsNullOrWhiteSpace(options.SinkDirectory)
            ? Path.Combine(_environment.ContentRootPath, "App_Data", "invite-mail")
            : options.SinkDirectory;
        Directory.CreateDirectory(directory);

        var fileName = $"{message.AssignmentId:N}-{_clock.UtcNow:yyyyMMddHHmmssfff}.eml";
        var path = Path.Combine(directory, fileName);
        var body = InviteMailContent.Render(message);
        var subject = message.Resend ? $"{options.Subject} (resent)" : options.Subject;
        var contents =
            $"""
            To: {message.ToEmail}
            From: {options.FromDisplayName} <{options.FromAddress}>
            Subject: {subject}
            Date: {_clock.UtcNow:O}

            {body}
            """;
        await File.WriteAllTextAsync(path, contents, cancellationToken);

        _logger.LogInformation(
            "Invite email saved for assignment {AssignmentId} to sink file {FileName}",
            message.AssignmentId,
            fileName);
        return new InviteMailResult(true, null);
    }
}

/// <summary>
/// SMTP sender. Credentials and host come from configuration. Failures are redacted before they are logged or stored.
/// </summary>
public sealed class SmtpInviteMailer : IInviteMailer
{
    private readonly IOptions<InviteMailOptions> _options;
    private readonly ILogger<SmtpInviteMailer> _logger;

    public SmtpInviteMailer(IOptions<InviteMailOptions> options, ILogger<SmtpInviteMailer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<InviteMailResult> SendAsync(InviteMailMessage message, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.Smtp.Host))
        {
            return new InviteMailResult(false, "SMTP host is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            return new InviteMailResult(false, "Invite from-address is not configured.");
        }

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(options.FromAddress, options.FromDisplayName),
                Subject = message.Resend ? $"{options.Subject} (resent)" : options.Subject,
                Body = InviteMailContent.Render(message)
            };
            mail.To.Add(new MailAddress(message.ToEmail));
            using var client = CreateClient(options.Smtp);
            await client.SendMailAsync(mail, cancellationToken);
        }
        catch (Exception ex)
        {
            var safe = InviteSecretRedactor.Redact(ex.Message);
            _logger.LogWarning(
                "Invite email SMTP failed for assignment {AssignmentId}: {Error}",
                message.AssignmentId,
                safe);
            return new InviteMailResult(false, string.IsNullOrWhiteSpace(safe) ? "Email could not be sent." : safe);
        }

        _logger.LogInformation(
            "Invite email handed to SMTP for assignment {AssignmentId}",
            message.AssignmentId);
        return new InviteMailResult(true, null);
    }

#pragma warning disable SYSLIB0014
    private static SmtpClient CreateClient(SmtpInviteOptions smtp)
    {
        var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        }

        return client;
    }
#pragma warning restore SYSLIB0014
}

/// <summary>
/// In-memory mailer for the Testing environment. It never opens a socket.
/// </summary>
public sealed class RecordingInviteMailer : IInviteMailer
{
    private readonly List<InviteMailMessage> _messages = [];

    public IReadOnlyList<InviteMailMessage> Messages
    {
        get
        {
            lock (_messages)
            {
                return _messages.ToArray();
            }
        }
    }

    public bool FailNext { get; set; }

    public Task<InviteMailResult> SendAsync(InviteMailMessage message, CancellationToken cancellationToken)
    {
        if (FailNext)
        {
            FailNext = false;
            return Task.FromResult(new InviteMailResult(false, "Email could not be sent."));
        }

        lock (_messages)
        {
            _messages.Add(message);
        }

        return Task.FromResult(new InviteMailResult(true, null));
    }

    public void Clear()
    {
        lock (_messages)
        {
            _messages.Clear();
        }
    }
}
