using System.Text.Json;
using InterviewQuiz.Delivery.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Delivery.Infrastructure.Persistence;

public sealed class DeliveryDbContext : DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentSnapshot> AssignmentSnapshots => Set<AssignmentSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("delivery");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryDbContext).Assembly);
    }
}
