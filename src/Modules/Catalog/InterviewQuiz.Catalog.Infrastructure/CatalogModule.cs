using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Services;
using InterviewQuiz.Catalog.Infrastructure.Persistence;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Catalog.Infrastructure;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InterviewQuiz")
            ?? throw new InvalidOperationException(
                "Connection string 'InterviewQuiz' is not configured. Set ConnectionStrings__InterviewQuiz.");

        services.AddDbContext<CatalogDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog");
                npgsql.EnableRetryOnFailure();
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
            });
        });

        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<QuizService>();
        services.AddScoped<IQuizService>(sp => sp.GetRequiredService<QuizService>());
        services.AddScoped<IQuizSnapshotReader>(sp => sp.GetRequiredService<QuizService>());
        services.AddScoped<DevelopmentQuizSeeder>();
        return services;
    }
}
