using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__InterviewQuiz")
            ?? "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only";

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog");
                npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson());
            })
            .Options;

        return new CatalogDbContext(options);
    }
}
