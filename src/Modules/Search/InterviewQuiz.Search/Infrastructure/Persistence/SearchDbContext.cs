using InterviewQuiz.Search.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Search.Infrastructure.Persistence;

public sealed class SearchDbContext : DbContext
{
    public SearchDbContext(DbContextOptions<SearchDbContext> options)
        : base(options)
    {
    }

    public DbSet<SavedFilter> Filters => Set<SavedFilter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("search");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SearchDbContext).Assembly);
    }
}
