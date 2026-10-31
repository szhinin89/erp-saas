using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

/// <summary>
/// PHYSICAL-FISCAL-F4/F5 — régimen tributario × tipo de comprobante permitido en forma física. Solo
/// contiene los casos con base normativa inequívoca; lo no listado no se emite en físico. Fuentes y
/// casos pendientes: docs/architecture/company-readiness.md § Política fiscal de emisión física.
/// </summary>
public class SriTaxRegimePhysicalDocTypeConfiguration
    : IEntityTypeConfiguration<SriTaxRegimePhysicalDocType>
{
    public void Configure(EntityTypeBuilder<SriTaxRegimePhysicalDocType> builder)
    {
        builder.ToTable("sri_tax_regime_physical_doc_type", schema: "global");
        builder.HasKey(x => new { x.TaxRegimeCode, x.DocTypeCode });
        builder.Property(x => x.TaxRegimeCode).HasColumnName("tax_regime_code").HasMaxLength(5);
        builder.Property(x => x.DocTypeCode).HasColumnName("doc_type_code").HasMaxLength(5);
        builder
            .Property(x => x.LegalBasis)
            .HasColumnName("legal_basis")
            .HasMaxLength(200)
            .IsRequired();
        // ValueGeneratedNever: conserva false con DEFAULT true (ADR-042).
        builder
            .Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder
            .HasOne<SriTaxRegime>()
            .WithMany()
            .HasForeignKey(x => x.TaxRegimeCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<SriDocType>()
            .WithMany()
            .HasForeignKey(x => x.DocTypeCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new SriTaxRegimePhysicalDocType
            {
                // Régimen General — BLOQUEADO (inactivo). Desde el 30-XI-2022 los preimpresos de un
                // emisor obligado solo se usan excepcionalmente por fuerza mayor o caso fortuito
                // (Res. NAC-DGERCGC22-00000024, sin texto oficial consultado): una autorización
                // registrada no basta y la circunstancia no es verificable. La fila documenta el
                // caso; no habilita la factura física ordinaria.
                TaxRegimeCode = "01",
                DocTypeCode = "01",
                LegalBasis =
                    "BLOQUEADO: Res. NAC-DGERCGC22-00000024, preimpresos solo por fuerza mayor o caso fortuito (no verificable)",
                IsActive = false,
            },
            new SriTaxRegimePhysicalDocType
            {
                // RIMPE Negocio Popular: notas de venta (o facturas electrónicas) a su elección
                // (Res. NAC-DGERCGC21-00000060; sri.gob.ec/rimpe). La emisión de 02 no está
                // implementada en Ventas: sri_doc_type 02 sigue inactivo.
                TaxRegimeCode = "03",
                DocTypeCode = "02",
                LegalBasis = "Res. NAC-DGERCGC21-00000060 (RIMPE Negocio Popular: notas de venta)",
                IsActive = true,
            }
        );
    }
}
