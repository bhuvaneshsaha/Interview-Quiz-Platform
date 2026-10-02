using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence;

public sealed class EvaluationDbContextFactory : IDesignTimeDbContextFactory<EvaluationDbContext>
{
    public EvaluationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__InterviewQuiz")
            ?? "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only";

        var options = new DbContextOptionsBuilder<EvaluationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "evaluation");
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
            })
            .Options;

        return new EvaluationDbContext(options);
    }
}
