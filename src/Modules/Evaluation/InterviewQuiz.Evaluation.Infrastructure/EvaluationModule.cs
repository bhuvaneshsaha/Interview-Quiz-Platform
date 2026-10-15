using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Evaluation.Application.Services;
using InterviewQuiz.Evaluation.Infrastructure.Persistence;
using InterviewQuiz.Kernel.Assignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Evaluation.Infrastructure;

public static class EvaluationModule
{
    public static IServiceCollection AddEvaluationModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InterviewQuiz")
            ?? throw new InvalidOperationException(
                "Connection string 'InterviewQuiz' is not configured. Set ConnectionStrings__InterviewQuiz.");

        services.AddDbContext<EvaluationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "evaluation");
                npgsql.EnableRetryOnFailure();
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
        });

        services.AddScoped<IAttemptRepository, AttemptRepository>();
        services.AddScoped<AttemptService>();
        services.AddScoped<IAttemptService>(sp => sp.GetRequiredService<AttemptService>());
        services.AddScoped<ICandidateAttemptIdLookup>(sp => sp.GetRequiredService<AttemptService>());
        return services;
    }
}
