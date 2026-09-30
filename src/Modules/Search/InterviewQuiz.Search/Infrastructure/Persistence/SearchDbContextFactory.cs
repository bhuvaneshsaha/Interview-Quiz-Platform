using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InterviewQuiz.Search.Infrastructure.Persistence;

public sealed class SearchDbContextFactory : IDesignTimeDbContextFactory<SearchDbContext>
{
    public SearchDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__InterviewQuiz")
            ?? "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only";

        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "search");
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
            })
            .Options;

        return new SearchDbContext(options);
    }
}
