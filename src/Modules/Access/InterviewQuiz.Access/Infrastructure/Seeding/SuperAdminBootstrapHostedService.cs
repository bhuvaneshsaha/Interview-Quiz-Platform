using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Access.Infrastructure.Seeding;

public sealed class SuperAdminBootstrapHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SuperAdminBootstrapHostedService> _logger;

    public SuperAdminBootstrapHostedService(
        IServiceScopeFactory scopes,
        ILogger<SuperAdminBootstrapHostedService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<SuperAdminBootstrap>();
        try
        {
            await bootstrap.EnsureAsync(cancellationToken);
        }
        catch (Exception exception) when (SuperAdminBootstrap.IsMissingSchema(exception))
        {
            _logger.LogWarning(
                exception,
                "Super Admin bootstrap skipped because the access schema is not migrated yet.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
