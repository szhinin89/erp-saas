using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriDocTypeConfiguration : IEntityTypeConfiguration<SriDocType>
{
    public void Configure(EntityTypeBuilder<SriDocType> builder)
    {
        builder.ToTable("sri_doc_type", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(5);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder
            .Property(x => x.ShortName)
            .HasColumnName("short_name")
            .HasMaxLength(20)
            .IsRequired();
        // Mismo criterio que SriRetentionCodeConfiguration (ZH-SRI-RETENTION-CATALOG-SSOT-01): con
        // DEFAULT true y generación "on add", EF omite un `false` (valor CLR por defecto) en seeds e
        // inserts — 02/08/09/18 declarados inactivos/no electrónicos quedaban true en BD.
        // ValueGeneratedNever conserva el DEFAULT de la columna pero obliga a EF a escribir el valor real.
        builder
            .Property(x => x.IsElectronic)
            .HasColumnName("is_electronic")
            .HasDefaultValue(true)
            .ValueGeneratedNever();
        builder
            .Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.HasData(
            new SriDocType
            {
                Code = "01",
                Name = "FACTURA",
                ShortName = "FACTURA",
                IsElectronic = true,
                IsActive = true,
            },
            new SriDocType
            {
                // 02/08/09/18: Catálogo ATS 06/08/2026, Tabla 4; no son tipos electrónicos (ficha 2.34 Tabla 3).
                Code = "02",
                Name = "Nota o boleta de venta",
                ShortName = "NOTA_VENTA",
                IsElectronic = false,
                IsActive = false,
            },
            new SriDocType
            {
                Code = "03",
                Name = "Liquidación de Compra de Bienes y Prestación de Servicios",
                ShortName = "LIQ_COMPRA",
                IsElectronic = true,
                IsActive = true,
            },
            new SriDocType
            {
                Code = "04",
                Name = "Nota de Crédito",
                ShortName = "N_CREDITO",
                IsElectronic = true,
                IsActive = true,
            },
            new SriDocType
            {
                Code = "05",
                Name = "Nota de Débito",
                ShortName = "N_DEBITO",
                IsElectronic = true,
                IsActive = true,
            },
            new SriDocType
            {
                Code = "06",
                Name = "Guía de Remisión",
                ShortName = "G_REMISION",
                IsElectronic = true,
                IsActive = true,
            },
            new SriDocType
            {
                Code = "07",
                Name = "Comprobante de Retención",
                ShortName = "retention",
                IsElectronic = true,
                IsActive = true,
            },
            new SriDocType
            {
                Code = "08",
                Name = "Boletos o entradas a espectáculos públicos",
                ShortName = "BOLETO",
                IsElectronic = false,
                IsActive = false,
            },
            new SriDocType
            {
                Code = "09",
                Name = "Tiquetes o vales emitidos por máquinas registradoras",
                ShortName = "CAJA_REG",
                IsElectronic = false,
                IsActive = false,
            },
            new SriDocType
            {
                Code = "18",
                Name = "Documentos autorizados utilizados en ventas excepto N/C N/D",
                ShortName = "DOC_AUT_VENTA",
                IsElectronic = false,
                IsActive = false,
            }
        );
    }
}
