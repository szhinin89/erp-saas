using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>
/// Mapeo EF de <see cref="PurchaseReturnSequence"/> — diseño P0-02 §7.1bis, Fase 2. Ámbito
/// <c>(TenantId, CompanyId)</c> sin <c>EmissionPointId</c>/<c>DocTypeCode</c> — deliberadamente no
/// es una segunda <c>DocumentSequence</c> (§7.1bis).
/// </summary>
public sealed class PurchaseReturnSequenceConfiguration
    : IEntityTypeConfiguration<PurchaseReturnSequence>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnSequence> builder)
    {
        builder.ToTable(
            "purchase_return_sequence",
            t => t.HasCheckConstraint("chk_purchase_return_sequence_current_seq_positive", "\"current_seq\" >= 1")
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.CurrentSeq).HasColumnName("current_seq").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .IsUnique()
            .HasDatabaseName("uq_purchase_return_sequence_tenant_company");
    }
}
