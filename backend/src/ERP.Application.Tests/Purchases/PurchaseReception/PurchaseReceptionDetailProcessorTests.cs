using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// <see cref="PurchaseReceptionDetailProcessor"/> es la única implementación de "XML -> líneas +
/// Item Matching" — la usan tanto la descarga inicial (<c>DownloadPurchaseReceptionXmlHandler</c>)
/// como la reconstrucción transparente del snapshot dentro de <c>CreatePurchaseReceptionDraftHandler</c>. Estos tests
/// reemplazan los que antes vivían embebidos en el handler de descarga.
/// </summary>
public sealed class PurchaseReceptionDetailProcessorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();

    private static (
        PurchaseReceptionDetailProcessor processor,
        Mock<IPurchaseXmlDraftParser> parser,
        Mock<IItemRepository> itemRepo,
        Mock<IItemMatchFinder> matchFinder
    ) BuildProcessor()
    {
        var parser = new Mock<IPurchaseXmlDraftParser>();
        var itemRepo = new Mock<IItemRepository>();
        var matchFinder = new Mock<IItemMatchFinder>();
        matchFinder
            .Setup(m =>
                m.FindCandidatesAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        var logger = new Mock<ILogger<PurchaseReceptionDetailProcessor>>();

        var processor = new PurchaseReceptionDetailProcessor(
            parser.Object,
            itemRepo.Object,
            matchFinder.Object,
            logger.Object
        );
        return (processor, parser, itemRepo, matchFinder);
    }

    [Fact]
    public async Task ProcessAsync_auto_matches_a_line_when_the_supplier_code_already_exists_in_the_catalog()
    {
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var (processor, parser, itemRepo, _) = BuildProcessor();

        var parsedLine = new ParsedPurchaseXmlLine(
            Description: "COCA COLA 500 ML",
            Quantity: 10,
            UnitPrice: 0.5m,
            DiscountPct: 0,
            VatCode: "2",
            IceCode: null,
            IceValue: 0,
            SupplierCode: "PROV-001",
            SupplierAuxCode: null,
            Discount: 0,
            LineSubtotal: 5,
            TaxCode: "2",
            VatPercentage: 15,
            TaxValue: 0.75m,
            TotalLine: 5.75m
        );
        parser
            .Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(
                Result<ParsedPurchaseXml>.Success(
                    new ParsedPurchaseXml(
                        "1791352688001",
                        "QUALA ECUADOR S A",
                        "01",
                        "015-027-000161740",
                        new DateOnly(2026, 7, 1),
                        null,
                        [parsedLine],
                        []
                    )
                )
            );
        itemRepo
            .Setup(r =>
                r.FindItemIdBySupplierCodeAsync(
                    supplierId,
                    "PROV-001",
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(itemId);

        var result = await processor.ProcessAsync(
            DocumentId,
            TenantId,
            supplierId,
            "<factura>...</factura>",
            CancellationToken.None
        );

        result.Lines.Should().ContainSingle();
        var line = result.Lines[0];
        line.ItemId.Should().Be(itemId);
        line.MatchStatus.Should().Be(ItemMatchStatus.AutoMatched);
        result.Processing.Status.Should().Be(PurchaseReceptionProcessingStatus.Processed);
        result.DocTypeCode.Should().Be("01");
        result.SriPaymentMethodCode.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_marks_Failed_when_the_header_cannot_be_parsed()
    {
        var (processor, parser, _, _) = BuildProcessor();
        parser
            .Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(
                Result<ParsedPurchaseXml>.ValidationFailure(
                    "Falta el elemento obligatorio 'infoFactura'."
                )
            );

        var result = await processor.ProcessAsync(
            DocumentId,
            TenantId,
            null,
            "<factura>ilegible</factura>",
            CancellationToken.None
        );

        result.Lines.Should().BeEmpty();
        result.Processing.Status.Should().Be(PurchaseReceptionProcessingStatus.Failed);
        result.Processing.LinesDetected.Should().Be(0);
        result.Processing.LinesProcessed.Should().Be(0);
        result.Processing.Notes.Should().Contain("infoFactura");
        result.DocTypeCode.Should().BeNull();
        result.SriPaymentMethodCode.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_marks_ProcessedWithWarnings_when_one_line_fails_among_several()
    {
        var (processor, parser, _, _) = BuildProcessor();

        var goodLine = new ParsedPurchaseXmlLine(
            Description: "Producto OK",
            Quantity: 1,
            UnitPrice: 10,
            DiscountPct: 0,
            VatCode: "2",
            IceCode: null,
            IceValue: 0,
            SupplierCode: "PROV-OK",
            SupplierAuxCode: null,
            Discount: 0,
            LineSubtotal: 10,
            TaxCode: "2",
            VatPercentage: 15,
            TaxValue: 1.5m,
            TotalLine: 11.5m
        );
        var badLineError = new ParsedPurchaseXmlLineError(
            2,
            "PROV-BAD",
            "Producto defectuoso",
            "La línea no tiene impuesto IVA."
        );
        parser
            .Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(
                Result<ParsedPurchaseXml>.Success(
                    new ParsedPurchaseXml(
                        "1791352688001",
                        "QUALA ECUADOR S A",
                        "01",
                        "015-027-000161740",
                        new DateOnly(2026, 7, 1),
                        null,
                        [goodLine],
                        [badLineError]
                    )
                )
            );

        var result = await processor.ProcessAsync(
            DocumentId,
            TenantId,
            null,
            "<factura>...</factura>",
            CancellationToken.None
        );

        result.Lines.Should().ContainSingle();
        result.Lines[0].Description.Should().Be("Producto OK");
        result
            .Processing.Status.Should()
            .Be(PurchaseReceptionProcessingStatus.ProcessedWithWarnings);
        result.Processing.LinesDetected.Should().Be(2);
        result.Processing.LinesProcessed.Should().Be(1);
        result.Processing.Notes.Should().Contain("PROV-BAD");
    }
}
