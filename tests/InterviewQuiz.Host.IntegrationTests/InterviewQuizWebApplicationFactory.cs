using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Catalog.Infrastructure.Persistence;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
using InterviewQuiz.Openings.Infrastructure.Persistence;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using InterviewQuiz.Search.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<InterviewQuizWebApplicationFactory>;

public sealed class InterviewQuizWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__InterviewQuiz");

    public static bool HasDatabase => !string.IsNullOrWhiteSpace(ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            if (HasDatabase)
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:InterviewQuiz"] = ConnectionString
                });
            }
        });
    }

    public async Task InitializeAsync()
    {
        if (!HasDatabase)
        {
            return;
        }

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccessDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<OpeningsDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SearchDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentAccessSeeder>()
            .SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentOpeningSeeder>()
            .SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentQuizSeeder>()
            .SeedAsync();
    }

    public new async Task DisposeAsync() => await base.DisposeAsync();
}
