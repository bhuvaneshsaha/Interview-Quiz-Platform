using InterviewQuiz.Access.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Access.Infrastructure;

public sealed class AccessDbContext : DbContext
{
    public AccessDbContext(DbContextOptions<AccessDbContext> options)
        : base(options)
    {
    }

    public DbSet<Permission> Permissions => Set<Permission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("access");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);
    }
}
