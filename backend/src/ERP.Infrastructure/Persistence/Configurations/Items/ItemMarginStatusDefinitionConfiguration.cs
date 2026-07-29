using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class ItemMarginStatusDefinitionConfiguration
    : IEntityTypeConfiguration<ItemMarginStatusDefinition>
{
    public void Configure(EntityTypeBuilder<ItemMarginStatusDefinition> builder)
    {
        builder.ToTable("item_margin_statuses", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20);
        builder.Property(x => x.Label).HasColumnName("label").HasMaxLength(60).IsRequired();
        builder
            .Property(x => x.ColorToken)
            .HasColumnName("color_token")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasData(
            new ItemMarginStatusDefinition
            {
                Code = "SALUDABLE",
                Label = "Saludable",
                ColorToken = "success",
            },
            new ItemMarginStatusDefinition
            {
                Code = "BAJO",
                Label = "Bajo",
                ColorToken = "warning",
            },
            new ItemMarginStatusDefinition
            {
                Code = "NEGATIVO",
                Label = "Negativo",
                ColorToken = "error",
            },
            new ItemMarginStatusDefinition
            {
                Code = "CERO",
                Label = "Sin margen",
                ColorToken = "neutral",
            },
            new ItemMarginStatusDefinition
            {
                Code = "SIN_PRECIO",
                Label = "Sin precio",
                ColorToken = "neutral",
            }
        );
    }
}
