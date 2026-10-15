using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Application.Services;
using InterviewQuiz.Delivery.Infrastructure.Persistence;
using InterviewQuiz.Delivery.Infrastructure.Seeding;
using InterviewQuiz.Kernel.Assignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Delivery.Infrastructure;

public static class DeliveryModule
{
    public static IServiceCollection AddDeliveryModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InterviewQuiz")
            ?? throw new InvalidOperationException(
                "Connection string 'InterviewQuiz' is not configured. Set ConnectionStrings__InterviewQuiz.");

        services.AddDbContext<DeliveryDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "delivery");
                npgsql.EnableRetryOnFailure();
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
            });
        });

        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<AssignmentService>();
        services.AddScoped<IAssignmentService>(sp => sp.GetRequiredService<AssignmentService>());
        services.AddScoped<IAssignmentInviteInfo>(sp => sp.GetRequiredService<AssignmentService>());
        services.AddScoped<IAssignmentSnapshotReader>(sp => sp.GetRequiredService<AssignmentService>());
        services.AddScoped<IAssignmentLifecycle>(sp => sp.GetRequiredService<AssignmentService>());
        services.AddScoped<DevelopmentAssignmentSeeder>();
        return services;
    }
}
