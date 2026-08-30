using ERP.Domain.Modules.DocTypes.Entities;
using ERP.Domain.Modules.DocTypes.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.DocTypes;

public sealed class DocWorkflowPolicyConfiguration : IEntityTypeConfiguration<DocWorkflowPolicy>
{
    public void Configure(EntityTypeBuilder<DocWorkflowPolicy> builder)
    {
        builder.ToTable("doc_workflow_policy");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(e => e.DocTypeCode).HasColumnName("doc_type_code").HasMaxLength(10).IsRequired();
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled").IsRequired();

        builder
            .Property(e => e.DraftMode)
            .HasColumnName("draft_mode")
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<DraftMode>(v, ignoreCase: true)
            )
            .HasMaxLength(16)
            .IsRequired();

        builder
            .Property(e => e.DefaultAction)
            .HasColumnName("default_action")
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<DocWorkflowDefaultAction>(v, ignoreCase: true)
            )
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.DocTypeCode,
            })
            .IsUnique()
            .HasDatabaseName("uq_doc_workflow_policy_company_doc_type");

        builder
            .HasOne<DocType>()
            .WithMany()
            .HasForeignKey(e => e.DocTypeCode)
            .HasPrincipalKey(dt => dt.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
