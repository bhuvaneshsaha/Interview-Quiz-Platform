using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InterviewQuiz.Access.Infrastructure;

public sealed class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__InterviewQuiz")
            ?? "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only";

        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "access");
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
            })
            .Options;

        return new AccessDbContext(options);
    }
}
