using InterviewQuiz.Evaluation.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence;

public sealed class EvaluationDbContext : DbContext
{
    public EvaluationDbContext(DbContextOptions<EvaluationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();
    public DbSet<AttemptItemResult> AttemptItemResults => Set<AttemptItemResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("evaluation");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EvaluationDbContext).Assembly);
    }
}
