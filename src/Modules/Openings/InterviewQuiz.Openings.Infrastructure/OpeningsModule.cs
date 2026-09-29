using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Services;
using InterviewQuiz.Openings.Infrastructure.Persistence;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Openings.Infrastructure;

public static class OpeningsModule
{
    public static IServiceCollection AddOpeningsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InterviewQuiz")
            ?? throw new InvalidOperationException(
                "Connection string 'InterviewQuiz' is not configured. Set ConnectionStrings__InterviewQuiz.");

        services.AddDbContext<OpeningsDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "openings");
                npgsql.EnableRetryOnFailure();
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
            });
        });

        services.AddScoped<IOpeningRepository, OpeningRepository>();
        services.AddScoped<IOpeningFieldDefinitionRepository, OpeningFieldDefinitionRepository>();
        services.AddScoped<OpeningService>();
        services.AddScoped<IOpeningService>(sp => sp.GetRequiredService<OpeningService>());
        services.AddScoped<IOpeningLookup>(sp => sp.GetRequiredService<OpeningService>());
        services.AddScoped<IOpeningFieldDefinitionService, OpeningFieldDefinitionService>();
        services.AddScoped<DevelopmentOpeningSeeder>();
        return services;
    }
}
