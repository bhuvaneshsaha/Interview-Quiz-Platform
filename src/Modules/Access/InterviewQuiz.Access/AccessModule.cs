using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Access.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Access;

public static class AccessModule
{
    public static IServiceCollection AddAccessModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InterviewQuiz")
            ?? throw new InvalidOperationException(
                "Connection string 'InterviewQuiz' is not configured. Set ConnectionStrings__InterviewQuiz.");

        services.AddDbContext<AccessDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "access");
                npgsql.EnableRetryOnFailure();
            });
        });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, TemporaryAllowAuthenticatedPermissionHandler>();
        return services;
    }
}
