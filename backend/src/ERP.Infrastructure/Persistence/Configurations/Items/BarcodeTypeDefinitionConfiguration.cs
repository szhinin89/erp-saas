using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class BarcodeTypeDefinitionConfiguration
    : IEntityTypeConfiguration<BarcodeTypeDefinition>
{
    public void Configure(EntityTypeBuilder<BarcodeTypeDefinition> builder)
    {
        builder.ToTable("barcode_types", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new BarcodeTypeDefinition
            {
                Code = "EAN13",
                Name = "EAN-13",
                IsActive = true,
            },
            new BarcodeTypeDefinition
            {
                Code = "EAN8",
                Name = "EAN-8",
                IsActive = true,
            },
            new BarcodeTypeDefinition
            {
                Code = "QR",
                Name = "QR",
                IsActive = true,
            },
            new BarcodeTypeDefinition
            {
                Code = "Code128",
                Name = "Code 128",
                IsActive = true,
            },
            new BarcodeTypeDefinition
            {
                Code = "Internal",
                Name = "Interno",
                IsActive = true,
            },
            new BarcodeTypeDefinition
            {
                Code = "Other",
                Name = "Otro",
                IsActive = true,
            }
        );
    }
}
