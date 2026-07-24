using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// Regresión de la auditoría de códigos tributarios opcionales (2026-07-20): IceCode debe
/// normalizar "" y "   " a NULL igual que un IceCode ausente — nunca perzistir string vacío
/// como si fuera un código real, causa raíz del error "Código ICE '' no encontrado o inactivo."
/// </summary>
public sealed class PurchaseInvoiceDetailIceCodeTests
{
    private static PurchaseInvoiceDetail CreateLine(string? iceCode) =>
        PurchaseInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(), tenantId: Guid.NewGuid(),
            description: "Línea test", quantity: 1m, unitPrice: 10m,
            vatCode: "4", uomCode: "UNIT", iceCode: iceCode);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_con_IceCode_sin_contenido_real_normaliza_a_null(string? blank)
    {
        CreateLine(blank).IceCode.Should().BeNull();
    }

    [Fact]
    public void Create_con_IceCode_presente_lo_persiste_recortado()
    {
        CreateLine(" ICE01 ").IceCode.Should().Be("ICE01");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyTaxes_con_IceCode_sin_contenido_real_normaliza_a_null(string? blank)
    {
        var line = CreateLine(null);

        line.ApplyTaxes("4", 15m, "IVA 15%", blank, 0m, null);

        line.IceCode.Should().BeNull();
    }
}
