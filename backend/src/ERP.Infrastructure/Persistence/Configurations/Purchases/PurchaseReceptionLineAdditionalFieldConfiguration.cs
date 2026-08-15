using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseReceptionLineAdditionalFieldConfiguration
    : IEntityTypeConfiguration<PurchaseReceptionLineAdditionalField>
{
    public void Configure(EntityTypeBuilder<PurchaseReceptionLineAdditionalField> builder)
    {
        builder.ToTable("purchase_reception_line_additional_fields");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.PurchaseReceptionLineId)
            .HasColumnName("purchase_reception_line_id")
            .IsRequired();

        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(PurchaseReceptionLineAdditionalField.NameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Value)
            .HasColumnName("value")
            .HasMaxLength(PurchaseReceptionLineAdditionalField.ValueMaxLen)
            .IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        builder
            .HasIndex(x => x.PurchaseReceptionLineId)
            .HasDatabaseName("ix_purchase_reception_line_additional_fields_line");
        builder
            .HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_purchase_reception_line_additional_fields_tenant");
    }
}
