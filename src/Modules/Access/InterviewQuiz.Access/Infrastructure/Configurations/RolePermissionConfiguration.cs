using InterviewQuiz.Access.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Access.Infrastructure.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(item => new { item.RoleId, item.PermissionCode });
        builder.Property(item => item.PermissionCode).HasMaxLength(128);

        builder.HasOne(item => item.Permission)
            .WithMany()
            .HasForeignKey(item => item.PermissionCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
