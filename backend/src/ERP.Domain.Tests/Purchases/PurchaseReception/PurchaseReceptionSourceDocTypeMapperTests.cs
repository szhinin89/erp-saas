using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using FluentAssertions;
using Xunit;

namespace ERP.Domain.Tests.Purchases.PurchaseReception;

/// <summary>
/// FLOW-READY-02B.1-FIX2: TIPO_COMPROBANTE del TXT SRI llega con variaciones de acento,
/// mayúsculas/minúsculas y con/sin la palabra "de" — la clasificación no debe depender de una
/// comparación exacta del texto original.
/// </summary>
public sealed class PurchaseReceptionSourceDocTypeMapperTests
{
    [Theory]
    [InlineData("Nota de Crédito")]
    [InlineData("Nota de Credito")]
    [InlineData("Nota Crédito")]
    [InlineData("Nota Credito")]
    [InlineData("NOTA DE CRÉDITO")]
    [InlineData("NOTA CREDITO")]
    [InlineData("  nota   de   crédito  ")]
    public void FromRawText_recognizes_credit_note_variants(string raw)
    {
        PurchaseReceptionSourceDocTypeMapper
            .FromRawText(raw)
            .Should()
            .Be(PurchaseReceptionSourceDocType.CreditNote);
    }

    [Theory]
    [InlineData("Factura")]
    [InlineData("FACTURA")]
    [InlineData("  factura  ")]
    public void FromRawText_recognizes_invoice_variants(string raw)
    {
        PurchaseReceptionSourceDocTypeMapper
            .FromRawText(raw)
            .Should()
            .Be(PurchaseReceptionSourceDocType.Invoice);
    }

    [Theory]
    [InlineData("Nota de Débito")]
    [InlineData("Nota de Debito")]
    public void FromRawText_recognizes_debit_note_variants(string raw)
    {
        PurchaseReceptionSourceDocTypeMapper
            .FromRawText(raw)
            .Should()
            .Be(PurchaseReceptionSourceDocType.DebitNote);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Algo Desconocido")]
    public void FromRawText_returns_unknown_for_anything_else(string? raw)
    {
        PurchaseReceptionSourceDocTypeMapper
            .FromRawText(raw)
            .Should()
            .Be(PurchaseReceptionSourceDocType.Unknown);
    }
}
