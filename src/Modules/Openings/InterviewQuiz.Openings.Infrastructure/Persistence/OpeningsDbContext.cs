using InterviewQuiz.Openings.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Openings.Infrastructure.Persistence;

public sealed class OpeningsDbContext : DbContext
{
    public OpeningsDbContext(DbContextOptions<OpeningsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Opening> Openings => Set<Opening>();
    public DbSet<OpeningFieldDefinition> OpeningFieldDefinitions => Set<OpeningFieldDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("openings");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpeningsDbContext).Assembly);
    }
}
