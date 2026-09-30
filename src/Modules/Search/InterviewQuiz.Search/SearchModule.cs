using InterviewQuiz.Search.Application;
using InterviewQuiz.Search.Application.Services;
using InterviewQuiz.Search.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Search;

public static class SearchModule
{
    public static IServiceCollection AddSearchModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InterviewQuiz")
            ?? throw new InvalidOperationException(
                "Connection string 'InterviewQuiz' is not configured. Set ConnectionStrings__InterviewQuiz.");

        services.AddDbContext<SearchDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "search");
                npgsql.EnableRetryOnFailure();
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
            });
        });

        services.AddScoped<IFilterRepository, FilterRepository>();
        services.AddScoped<IFilterService, FilterService>();
        return services;
    }
}
