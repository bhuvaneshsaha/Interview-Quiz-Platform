using InterviewQuiz.Access.Domain;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewQuiz.Access.Infrastructure.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(e => e.Code);
        builder.Property(e => e.Code).HasMaxLength(128);
        builder.Property(e => e.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Module).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IncludeInEmployeeRoleEditor).IsRequired();

        builder.HasData(
            PermissionCodes.All.Select(p => new Permission
            {
                Code = p.Code,
                DisplayName = p.DisplayName,
                Module = p.Module,
                IncludeInEmployeeRoleEditor = p.IncludeInEmployeeRoleEditor
            }).ToArray());
    }
}
