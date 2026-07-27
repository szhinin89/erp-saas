using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseReceptionLineConfiguration : IEntityTypeConfiguration<PurchaseReceptionLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReceptionLine> builder)
    {
        builder.ToTable("purchase_reception_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PurchaseReceptionDocumentId).HasColumnName("purchase_reception_document_id").IsRequired();

        builder.Property(x => x.SupplierCode).HasColumnName("supplier_code")
            .HasMaxLength(PurchaseReceptionLine.SupplierCodeMaxLen);
        builder.Property(x => x.SupplierAuxCode).HasColumnName("supplier_aux_code")
            .HasMaxLength(PurchaseReceptionLine.SupplierCodeMaxLen);
        builder.Property(x => x.Description).HasColumnName("description")
            .HasMaxLength(PurchaseReceptionLine.DescriptionMaxLen).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity")
            .HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price")
            .HasColumnType("numeric(18,6)").IsRequired();

        builder.Property(x => x.ItemId).HasColumnName("item_id");
        builder.Property(x => x.MatchStatus).HasColumnName("match_status")
            .HasConversion<int>().IsRequired();
        builder.Property(x => x.MatchedAt).HasColumnName("matched_at");
        builder.Property(x => x.MatchedBy).HasColumnName("matched_by");

        builder.HasIndex(x => x.PurchaseReceptionDocumentId)
            .HasDatabaseName("ix_purchase_reception_lines_document");

        // FK real a Items: una línea conciliada siempre referencia un ítem existente del mismo
        // tenant. Sin cascada: un ítem no debe arrastrar líneas de recepción al eliminarse.
        builder.HasOne<Item>()
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ItemId)
            .HasDatabaseName("ix_purchase_reception_lines_item")
            .HasFilter("item_id IS NOT NULL");
    }
}
