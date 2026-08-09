using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>Mapeo EF de <see cref="PurchaseCreditNoteDetail"/> — diseño FLOW-READY-02C §2.2.</summary>
public sealed class PurchaseCreditNoteDetailConfiguration
    : IEntityTypeConfiguration<PurchaseCreditNoteDetail>
{
    public void Configure(EntityTypeBuilder<PurchaseCreditNoteDetail> builder)
    {
        builder.ToTable("purchase_credit_note_details");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.PurchaseCreditNoteId)
            .HasColumnName("purchase_credit_note_id")
            .IsRequired();

        builder
            .Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(PurchaseCreditNoteDetail.DescriptionMaxLen)
            .IsRequired();

        builder
            .Property(x => x.Subtotal)
            .HasColumnName("subtotal")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(x => x.VatCode).HasColumnName("vat_code").HasMaxLength(20);
        builder.Property(x => x.VatRate).HasColumnName("vat_rate").HasColumnType("numeric(5,2)");
        builder
            .Property(x => x.VatAmount)
            .HasColumnName("vat_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        // ── Indexes ──────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseCreditNoteId })
            .HasDatabaseName("ix_purchase_credit_note_details_tenant_purchase_credit_note");
    }
}
