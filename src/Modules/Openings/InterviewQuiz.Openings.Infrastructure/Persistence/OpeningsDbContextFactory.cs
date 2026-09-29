using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InterviewQuiz.Openings.Infrastructure.Persistence;

public sealed class OpeningsDbContextFactory : IDesignTimeDbContextFactory<OpeningsDbContext>
{
    public OpeningsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__InterviewQuiz")
            ?? "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only";

        var options = new DbContextOptionsBuilder<OpeningsDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "openings");
            })
            .Options;

        return new OpeningsDbContext(options);
    }
}
