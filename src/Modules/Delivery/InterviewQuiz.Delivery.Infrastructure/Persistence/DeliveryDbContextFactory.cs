using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InterviewQuiz.Delivery.Infrastructure.Persistence;

public sealed class DeliveryDbContextFactory : IDesignTimeDbContextFactory<DeliveryDbContext>
{
    public DeliveryDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__InterviewQuiz")
            ?? "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only";

        var options = new DbContextOptionsBuilder<DeliveryDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "delivery");
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
            })
            .Options;

        return new DeliveryDbContext(options);
    }
}
