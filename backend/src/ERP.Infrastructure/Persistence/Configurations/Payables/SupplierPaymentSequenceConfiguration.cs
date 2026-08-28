using ERP.Domain.Modules.Payables.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Payables;

/// <summary>
/// Mapeo EF de <see cref="SupplierPaymentSequence"/> — mismo criterio que
/// <c>PurchaseReturnSequenceConfiguration</c>: ámbito <c>(TenantId, CompanyId)</c> sin
/// <c>EmissionPointId</c>/<c>DocTypeCode</c>.
/// </summary>
public sealed class SupplierPaymentSequenceConfiguration : IEntityTypeConfiguration<SupplierPaymentSequence>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentSequence> builder)
    {
        builder.ToTable(
            "supplier_payment_sequences",
            t =>
                t.HasCheckConstraint(
                    "chk_supplier_payment_sequence_current_seq_positive",
                    "\"current_seq\" >= 1"
                )
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.CurrentSeq).HasColumnName("current_seq").IsRequired();
        builder
            .Property(x => x.Prefix)
            .HasColumnName("prefix")
            .HasMaxLength(SupplierPaymentSequence.PrefixMaxLen);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .IsUnique()
            .HasDatabaseName("uq_supplier_payment_sequences_tenant_company");
    }
}
