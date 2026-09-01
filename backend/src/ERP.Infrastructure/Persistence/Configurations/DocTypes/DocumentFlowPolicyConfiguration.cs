using ERP.Domain.Modules.DocTypes.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.DocTypes;

public sealed class DocumentFlowPolicyConfiguration : IEntityTypeConfiguration<DocumentFlowPolicy>
{
    public void Configure(EntityTypeBuilder<DocumentFlowPolicy> builder)
    {
        builder.ToTable("document_flow_policy");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(e => e.DocumentTypeCode).HasColumnName("document_type_code").HasMaxLength(10).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();

        ConfigureModeColumn(builder.Property(e => e.CreationMode), "creation_mode");
        ConfigureModeColumn(builder.Property(e => e.ConfirmationMode), "confirmation_mode");
        ConfigureModeColumn(builder.Property(e => e.AuthorizationMode), "authorization_mode");
        ConfigureModeColumn(builder.Property(e => e.PendingDocumentMode), "pending_document_mode");
        ConfigureModeColumn(builder.Property(e => e.CancellationMode), "cancellation_mode");
        ConfigureModeColumn(builder.Property(e => e.PayableGenerationMode), "payable_generation_mode");
        ConfigureModeColumn(builder.Property(e => e.AccountingPostingMode), "accounting_posting_mode");
        ConfigureModeColumn(builder.Property(e => e.InventoryImpactMode), "inventory_impact_mode");
        ConfigureModeColumn(builder.Property(e => e.NotificationMode), "notification_mode");

        builder
            .Property(e => e.RequiresCancellationReason)
            .HasColumnName("requires_cancellation_reason")
            .IsRequired();
        builder.Property(e => e.RequiresAttachment).HasColumnName("requires_attachment").IsRequired();
        builder.Property(e => e.RequiresSupplier).HasColumnName("requires_supplier").IsRequired();
        builder.Property(e => e.RequiresDueDate).HasColumnName("requires_due_date").IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.DocumentTypeCode,
            })
            .IsUnique()
            .HasDatabaseName("uq_document_flow_policy_company_doc_type");

        builder
            .HasOne<DocType>()
            .WithMany()
            .HasForeignKey(e => e.DocumentTypeCode)
            .HasPrincipalKey(dt => dt.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // 64, no 32: CancellationMode.AllowedAfterConfirmationWithReversal por sí solo tiene 36
    // caracteres en minúsculas — el primer intento con 32 rompió el backfill real
    // (Npgsql 22001 "value too long") en cuanto GASDOC intentó sembrarse con este valor.
    private static void ConfigureModeColumn<TEnum>(PropertyBuilder<TEnum> property, string columnName)
        where TEnum : struct, Enum
    {
        property
            .HasColumnName(columnName)
            .HasConversion(v => v.ToString().ToLowerInvariant(), v => Enum.Parse<TEnum>(v, ignoreCase: true))
            .HasMaxLength(64)
            .IsRequired();
    }
}
