using ERP.Domain.Security.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class SecurityAdminScopeAssignmentConfiguration : IEntityTypeConfiguration<SecurityAdminScopeAssignment>
{
    public void Configure(EntityTypeBuilder<SecurityAdminScopeAssignment> builder)
    {
        builder.ToTable("security_admin_scope_assignments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();

        builder.Property(x => x.SubjectType).HasColumnName("subject_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Scope).HasColumnName("scope").IsRequired();
        builder.Property(x => x.IsAllowed).HasColumnName("is_allowed").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.SubscriberId, x.SubjectType, x.SubjectKey, x.Scope })
            .IsUnique()
            .HasDatabaseName("ux_security_admin_scopes_subject");
    }
}

