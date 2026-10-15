using InterviewQuiz.Access.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Access.Infrastructure;

public sealed class AccessDbContext : IdentityUserContext<ApplicationUser>, IDataProtectionKeyContext
{
    public AccessDbContext(DbContextOptions<AccessDbContext> options)
        : base(options)
    {
    }

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<AccessRole> Roles => Set<AccessRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRoleAssignment> UserRoles => Set<UserRoleAssignment>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<MagicLinkInvite> MagicLinkInvites => Set<MagicLinkInvite>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("access");

        modelBuilder.Entity<DataProtectionKey>(entity =>
        {
            entity.ToTable("data_protection_keys");
            entity.Property(key => key.FriendlyName).HasMaxLength(256);
            entity.Property(key => key.Xml).IsRequired();
        });

        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("identity_user_claims");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("identity_user_logins");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("identity_user_tokens");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);
    }
}
