using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// Regresión de la auditoría de códigos tributarios opcionales (2026-07-20): IceCode debe
/// normalizar "" y "   " a NULL igual que un IceCode ausente — mismo patrón que
/// PurchaseInvoiceDetail, único punto de normalización compartido (OptionalCode.Normalize).
/// </summary>
public sealed class SalesInvoiceDetailIceCodeTests
{
    private static SalesInvoiceDetail CreateLine(string? iceCode) =>
        SalesInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            description: "Línea test",
            quantity: 1m,
            unitPrice: 10m,
            vatCode: "4",
            uomCode: "UNIT",
            iceCode: iceCode
        );

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
