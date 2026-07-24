using System.Text;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Infrastructure.Modules.Purchases.PurchaseReception;
using FluentAssertions;
using Xunit;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

/// <summary>
/// Cubre el formato real del TXT de recepción SRI (tabulador, con encabezado) confirmado por el
/// usuario en la muestra "0350016432001_Recibidos_FC_JULIO2026.txt".
/// </summary>
public sealed class PurchaseInvoiceTxtParserTests
{
    private const string Header =
        "RUC_EMISOR\tRAZON_SOCIAL_EMISOR\tTIPO_COMPROBANTE\tSERIE_COMPROBANTE\tCLAVE_ACCESO\t" +
        "FECHA_AUTORIZACION\tFECHA_EMISION\tIDENTIFICACION_RECEPTOR\tVALOR_SIN_IMPUESTOS\tIVA\t" +
        "IMPORTE_TOTAL\tNUMERO_DOCUMENTO_MODIFICADO";

    private static Stream ToStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task ParseAsync_parses_real_sample_rows()
    {
        var content = Header + "\n" +
            "1791352688001\tQUALA ECUADOR S A\tFactura\t015-027-000161740\t0107202601179135268800120150270001617400016174011\t01/07/2026 21:06:55\t01/07/2026\t0350016432\t15.96\t2.4\t18.35\t\n" +
            "0300421401001\tANGAMARCA ANDRADE SEGUNDO MELCHOR\tFactura\t001-002-000005024\t0307202601030042140100120010020000050249846951115\t03/07/2026 14:31:06\t03/07/2026\t0350016432\t51.58\t.31\t51.89\t\n";

        var parser = new PurchaseInvoiceTxtParser();
        var result = await parser.ParseAsync(ToStream(content));

        result.Errors.Should().BeEmpty();
        result.SkippedUnsupportedCount.Should().Be(0);
        result.Records.Should().HaveCount(2);

        var first = result.Records[0];
        first.SourceDocType.Should().Be(PurchaseReceptionSourceDocType.Invoice);
        first.SupplierRuc.Should().Be("1791352688001");
        first.SupplierName.Should().Be("QUALA ECUADOR S A");
        first.InvoiceNumber.Should().Be("015-027-000161740");
        first.AccessKey.Should().Be("0107202601179135268800120150270001617400016174011");
        first.AccessKey.Should().HaveLength(49);
        first.IssueDate.Should().Be(new DateOnly(2026, 7, 1));
        first.AuthorizationDate.Should().Be(new DateTime(2026, 7, 1, 21, 6, 55));
        first.Subtotal.Should().Be(15.96m);
        first.VatAmount.Should().Be(2.4m);
        first.Total.Should().Be(18.35m);

        // Monto de IVA sin cero inicial (".31") debe parsear como 0.31, no fallar.
        result.Records[1].VatAmount.Should().Be(0.31m);
    }

    [Fact]
    public async Task ParseAsync_skips_non_invoice_doc_types_without_treating_them_as_errors()
    {
        var content = Header + "\n" +
            "1791352688001\tQUALA ECUADOR S A\tNota de Crédito\t015-027-000161741\t0107202601179135268800120150270001617410016174012\t01/07/2026 21:06:55\t01/07/2026\t0350016432\t1\t0\t1\t015-027-000161740\n";

        var parser = new PurchaseInvoiceTxtParser();
        var result = await parser.ParseAsync(ToStream(content));

        result.Records.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
        result.SkippedUnsupportedCount.Should().Be(1);
    }

    [Fact]
    public async Task ParseAsync_reports_malformed_line_as_error_without_aborting_the_file()
    {
        var content = Header + "\n" +
            "not-enough-columns\tOnly two\n" +
            "0300421401001\tANGAMARCA ANDRADE SEGUNDO MELCHOR\tFactura\t001-002-000005024\t0307202601030042140100120010020000050249846951115\t03/07/2026 14:31:06\t03/07/2026\t0350016432\t51.58\t.31\t51.89\t\n";

        var parser = new PurchaseInvoiceTxtParser();
        var result = await parser.ParseAsync(ToStream(content));

        result.Errors.Should().HaveCount(1);
        result.Errors[0].LineNumber.Should().Be(2);
        result.Records.Should().HaveCount(1);
    }

    [Fact]
    public async Task ParseAsync_returns_error_when_header_is_missing_required_columns()
    {
        var content = "RUC_EMISOR\tRAZON_SOCIAL_EMISOR\n1791352688001\tQUALA ECUADOR S A\n";

        var parser = new PurchaseInvoiceTxtParser();
        var result = await parser.ParseAsync(ToStream(content));

        result.Records.Should().BeEmpty();
        result.Errors.Should().ContainSingle(e => e.Reason.Contains("Encabezado"));
    }

    [Fact]
    public async Task ParseAsync_returns_empty_result_for_empty_file()
    {
        var parser = new PurchaseInvoiceTxtParser();
        var result = await parser.ParseAsync(ToStream(string.Empty));

        result.Records.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
        result.SkippedUnsupportedCount.Should().Be(0);
    }
}
