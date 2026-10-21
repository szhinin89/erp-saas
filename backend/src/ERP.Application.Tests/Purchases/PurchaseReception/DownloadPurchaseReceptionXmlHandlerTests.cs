using ERP.Domain.Modules.Purchases.Entities;
using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.GetPurchaseReceptionXmlView;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// Cubre exclusivamente responsabilidades del handler (descarga SRI, transición de estado,
/// persistencia atómica). La interpretación del detalle XML + Item Matching ahora vive en
/// <see cref="PurchaseReceptionDetailProcessor"/> (compartida con el reprocesamiento manual) y se
/// prueba por separado en <c>PurchaseReceptionDetailProcessorTests</c>.
/// </summary>
public sealed class DownloadPurchaseReceptionXmlHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseReceptionDocument SampleDocument(
        Guid? supplierId = null,
        PurchaseReceptionSourceDocType sourceDocType = PurchaseReceptionSourceDocType.Invoice
    ) =>
        PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            sourceDocType,
            "1791352688001",
            "QUALA ECUADOR S A",
            supplierId,
            "0107202601179135268800120150270001617400016174011",
            "015-027-000161740",
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 21, 6, 55, DateTimeKind.Utc),
            15.96m,
            2.4m,
            18.35m,
            UserId
        );

    private static PurchaseReceptionDetailProcessingResult FailedResult(
        string note = "XML sin detalle de prueba."
    ) => new([], PurchaseReceptionProcessingOutcome.Failed(note), null, null, null);

    private static (
        DownloadPurchaseReceptionXmlHandler handler,
        Mock<IPurchaseReceptionDocumentRepository> repo,
        Mock<IPurchaseInvoiceRepository> purchaseRepo,
        Mock<IBusinessPartnerRepository> bpRepo,
        Mock<ISriReceptionXmlProvider> provider,
        Mock<IPurchaseReceptionDetailProcessor> detailProcessor
    ) BuildHandler(bool useRealInvoiceProcessor = false)
    {
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        var purchaseRepo = new Mock<IPurchaseInvoiceRepository>();
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        var provider = new Mock<ISriReceptionXmlProvider>();
        var detailProcessor = new Mock<IPurchaseReceptionDetailProcessor>();

        // Por defecto: sin detalle interpretable — los tests que no ejercitan el procesamiento
        // quedan enfocados exclusivamente en el ciclo de descarga/verificación del handler.
        detailProcessor
            .Setup(p =>
                p.ProcessAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(FailedResult());

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);
        var logger = new Mock<ILogger<DownloadPurchaseReceptionXmlHandler>>();
        var matchFinder = new Mock<IItemMatchFinder>();
        matchFinder.Setup(m => m.FindCandidatesAsync(
            It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new DownloadPurchaseReceptionXmlHandler(
            repo.Object,
            purchaseRepo.Object,
            Mock.Of<ERP.Domain.Modules.Expenses.Interfaces.IExpenseDocumentRepository>(),
            bpRepo.Object,
            provider.Object,
            useRealInvoiceProcessor
                ? new PurchaseReceptionDetailProcessor(
                    new PurchaseXmlDraftParser(), Mock.Of<IItemRepository>(),
                    matchFinder.Object,
                    Mock.Of<ILogger<PurchaseReceptionDetailProcessor>>())
                : detailProcessor.Object,
            tenant.Object,
            company.Object,
            user.Object,
            logger.Object
        );

        return (handler, repo, purchaseRepo, bpRepo, provider, detailProcessor);
    }

    [Theory]
    [InlineData("<factura><infoFactura /></factura>")]
    [InlineData("<notaCredito><infoTributaria><codDoc>01</codDoc></infoTributaria></notaCredito>")]
    public async Task Handle_rejects_wrong_xml_type_for_credit_note(string xml)
    {
        var document = SampleDocument(sourceDocType: PurchaseReceptionSourceDocType.CreditNote);
        var (handler, repo, _, _, provider, invoiceProcessor) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    document.AccessKey,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(true, document.AccessKey, DateTime.UtcNow, xml, null)
            );
        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );
        result.IsSuccess.Should().BeFalse();
        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Imported);
        invoiceProcessor.Verify(
            p =>
                p.ProcessAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("valid", PurchaseReceptionProcessingStatus.Processed, 1, 1)]
    [InlineData("empty", PurchaseReceptionProcessingStatus.Failed, 0, 0)]
    [InlineData("invalid", PurchaseReceptionProcessingStatus.Failed, 1, 0)]
    [InlineData("mixed", PurchaseReceptionProcessingStatus.ProcessedWithWarnings, 2, 1)]
    public async Task Handle_credit_note_uses_own_parser_and_preserves_verified_xml_and_lines(
        string detailCase,
        PurchaseReceptionProcessingStatus expectedStatus,
        int detected,
        int processed
    )
    {
        var document = SampleDocument(sourceDocType: PurchaseReceptionSourceDocType.CreditNote);
        var (handler, repo, _, _, provider, invoiceProcessor) = BuildHandler();
        var xml = $$"""
            <notaCredito><infoTributaria><codDoc>04</codDoc><claveAcceso>{{document.AccessKey}}</claveAcceso>
            <ruc>{{document.SupplierRuc}}</ruc><razonSocial>Proveedor</razonSocial><estab>015</estab><ptoEmi>027</ptoEmi><secuencial>000161740</secuencial></infoTributaria>
            <infoNotaCredito><fechaEmision>01/07/2026</fechaEmision><numDocModificado>001-001-000000001</numDocModificado><motivo>Descuento</motivo>
            <totalSinImpuestos>10</totalSinImpuestos><valorModificacion>11.5</valorModificacion></infoNotaCredito>
            <detalles><detalle><codigoInterno>NC-1</codigoInterno><descripcion>Descuento</descripcion><cantidad>1</cantidad><precioUnitario>10</precioUnitario>
            <descuento>0</descuento><precioTotalSinImpuesto>10</precioTotalSinImpuesto><impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje>
            <tarifa>15</tarifa><baseImponible>10</baseImponible><valor>1.5</valor></impuesto></impuestos></detalle></detalles></notaCredito>
            """;
        var tree = System.Xml.Linq.XDocument.Parse(xml);
        var details = tree.Root!.Element("detalles")!;
        if (detailCase == "empty")
            details.RemoveNodes();
        else if (detailCase == "invalid")
            details.Element("detalle")!.Element("cantidad")!.Value = "0";
        else if (detailCase == "mixed")
        {
            var invalid = new System.Xml.Linq.XElement(details.Element("detalle")!);
            invalid.Element("cantidad")!.Value = "0";
            details.Add(invalid);
        }
        xml = tree.ToString();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    document.AccessKey,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(true, document.AccessKey, DateTime.UtcNow, xml, null)
            );
        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );
        result.IsSuccess.Should().BeTrue();
        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        document.DocTypeCode.Should().Be("04");
        document.XmlContent.Should().Be(xml);
        document.ProcessingStatus.Should().Be(expectedStatus);
        document.LinesDetectedCount.Should().Be(detected);
        document.LinesProcessedCount.Should().Be(processed);
        document.Lines.Should().HaveCount(processed);
        if (processed > 0)
        {
            document.Lines.Single().SupplierCode.Should().Be("NC-1");
            document.Lines.Single().TotalLine.Should().Be(11.5m);
        }
        if (detailCase != "valid")
            document.ProcessingNotes.Should().NotBeNullOrWhiteSpace();

        var retry = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id), CancellationToken.None);
        retry.IsSuccess.Should().BeFalse();
        document.ProcessingStatus.Should().Be(expectedStatus);
        document.XmlContent.Should().Be(xml);
        document.Lines.Should().HaveCount(processed);
        provider.Verify(p => p.GetAuthorizedXmlAsync(
            TenantId, CompanyId, document.AccessKey, It.IsAny<CancellationToken>()), Times.Once);
        invoiceProcessor.Verify(
            p =>
                p.ProcessAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_downloads_Alpina_credit_note_without_discount_and_view_balances()
    {
        var xml = PurchaseCreditNoteXmlParserTests.MinimalAlpinaXml;
        var parsed = PurchaseCreditNoteXmlParser.Parse(xml, Guid.NewGuid(), TenantId);
        var document = PurchaseReceptionDocument.Create(
            TenantId, CompanyId, BranchId, PurchaseReceptionSourceDocType.CreditNote,
            parsed.SupplierRuc, parsed.SupplierName, null, parsed.AccessKey, parsed.DocumentNumber,
            parsed.IssueDate, DateTime.UtcNow, 0.86m, 0m, 0.86m, UserId,
            modifiedDocumentNumber: parsed.ModifiedDocumentNumber);
        var (handler, repo, purchaseRepo, _, provider, invoiceProcessor) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider.Setup(p => p.GetAuthorizedXmlAsync(
            TenantId, CompanyId, document.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriReceptionXmlQueryResult(
                true, document.AccessKey, DateTime.UtcNow, xml, null));

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Processed,
            document.ProcessingNotes);
        document.Lines.Should().ContainSingle().Which.SupplierCode.Should().Be("5523");
        document.XmlContent.Should().Be(xml);
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var view = await new GetPurchaseReceptionXmlViewHandler(repo.Object, purchaseRepo.Object, tenant.Object)
            .Handle(new GetPurchaseReceptionXmlViewQuery(document.Id), CancellationToken.None);
        view.IsSuccess.Should().BeTrue();
        view.Value!.ModifiedDocumentNumber.Should().Be("012-004-004230851");
        view.Value.Lines.Should().ContainSingle().Which.MainCode.Should().Be("5523");
        view.Value.TotalAmount.Should().Be(0.86m);
        view.Value.LineCalculatedTotal.Should().Be(0.86m);
        view.Value.RoundingDifference.Should().Be(0m);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        invoiceProcessor.Verify(p => p.ProcessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_normal_invoice_uses_real_parser_and_processes_its_line()
    {
        var document = SampleDocument();
        var (handler, repo, _, _, provider, _) = BuildHandler(useRealInvoiceProcessor: true);
        const string xml = """
            <factura><infoTributaria><codDoc>01</codDoc><ruc>1791352688001</ruc>
            <razonSocial>Proveedor</razonSocial><estab>015</estab><ptoEmi>027</ptoEmi>
            <secuencial>000161740</secuencial></infoTributaria>
            <infoFactura><fechaEmision>01/07/2026</fechaEmision></infoFactura>
            <detalles><detalle><codigoPrincipal>INV-1</codigoPrincipal><descripcion>Producto</descripcion>
            <cantidad>1</cantidad><precioUnitario>10</precioUnitario><descuento>0</descuento>
            <precioTotalSinImpuesto>10</precioTotalSinImpuesto><impuestos><impuesto>
            <codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15</tarifa>
            <baseImponible>10</baseImponible><valor>1.5</valor></impuesto></impuestos>
            </detalle></detalles></factura>
            """;
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider.Setup(p => p.GetAuthorizedXmlAsync(
            TenantId, CompanyId, document.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriReceptionXmlQueryResult(
                true, document.AccessKey, DateTime.UtcNow, xml, null));

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        document.DocTypeCode.Should().Be("01");
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Processed);
        document.LinesDetectedCount.Should().Be(1);
        document.LinesProcessedCount.Should().Be(1);
        document.Lines.Should().ContainSingle().Which.TotalLine.Should().Be(11.5m);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_downloads_and_persists_xml_for_an_existing_imported_document()
    {
        var document = SampleDocument();
        var (handler, repo, _, _, provider, _) = BuildHandler();

        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    document.AccessKey,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(
                    Authorized: true,
                    AuthorizationNumber: document.AccessKey,
                    AuthorizationDate: new DateTime(2026, 7, 1, 21, 7, 0, DateTimeKind.Utc),
                    XmlContent: "<factura>...</factura>",
                    ErrorMessage: null
                )
            );

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.XmlDownloaded.Should().BeTrue();
        result.Value.Status.Should().Be("VERIFIED");
        result.Value.AuthorizationNumber.Should().Be(document.AccessKey);
        result.Value.DocumentId.Should().Be(document.Id);

        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        document.XmlContent.Should().Be("<factura>...</factura>");
        // El mock por defecto no interpreta detalle (Failed) -> nunca un "éxito" silencioso con 0
        // líneas (Fase 5) — se persiste explícitamente como Failed.
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Failed);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_persists_lines_and_processing_outcome_reported_by_the_detail_processor()
    {
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var document = SampleDocument(supplierId);
        var (handler, repo, _, _, provider, detailProcessor) = BuildHandler();

        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    document.AccessKey,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(
                    Authorized: true,
                    AuthorizationNumber: document.AccessKey,
                    AuthorizationDate: DateTime.UtcNow,
                    XmlContent: "<factura>...</factura>",
                    ErrorMessage: null
                )
            );

        var line = PurchaseReceptionLine.Create(
            document.Id,
            TenantId,
            "COCA COLA 500 ML",
            10m,
            0.5m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.75m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 5m,
            totalLine: 5.75m,
            supplierCode: "PROV-001",
            itemId: itemId,
            matchStatus: ItemMatchStatus.AutoMatched
        );
        detailProcessor
            .Setup(p =>
                p.ProcessAsync(
                    document.Id,
                    TenantId,
                    supplierId,
                    "<factura>...</factura>",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PurchaseReceptionDetailProcessingResult(
                    [line],
                    new PurchaseReceptionProcessingOutcome(
                        PurchaseReceptionProcessingStatus.Processed,
                        1,
                        1,
                        null
                    ),
                    "01",
                    "20",
                    "Comercial ACME"
                )
            );

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        document.Lines.Should().ContainSingle();
        var persistedLine = document.Lines.Single();
        persistedLine.ItemId.Should().Be(itemId);
        persistedLine.MatchStatus.Should().Be(ItemMatchStatus.AutoMatched);
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Processed);
        document.DocTypeCode.Should().Be("01");
        document.SriPaymentMethodCode.Should().Be("20");
        result.Value!.SupplierTradeName.Should().Be("Comercial ACME");
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_nonexistent_document()
    {
        var (handler, repo, _, _, provider, _) = BuildHandler();
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceptionDocument?)null);

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(missingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        provider.Verify(
            p =>
                p.GetAuthorizedXmlAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_keeps_previous_status_when_sri_query_fails()
    {
        var document = SampleDocument();
        var (handler, repo, _, _, provider, _) = BuildHandler();

        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    document.AccessKey,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(
                    Authorized: false,
                    AuthorizationNumber: null,
                    AuthorizationDate: null,
                    XmlContent: null,
                    ErrorMessage: "ERROR_CONEXION"
                )
            );

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.SriCommunicationError);

        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Imported);
        document.XmlContent.Should().BeNull();
        document.AuthorizationNumber.Should().BeNull();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_rejects_a_document_that_is_not_in_Imported_status()
    {
        var document = SampleDocument();
        document.AttachSriAuthorization(
            document.AccessKey,
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);

        var (handler, repo, _, _, provider, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        provider.Verify(
            p =>
                p.GetAuthorizedXmlAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ── FASE 5 PURCHASE-UX — la descarga refresca el estado real de la compra ───────────────

    private static async Task<DownloadPurchaseReceptionXmlResultDto> DownloadWithPurchaseRepo(
        Action<Mock<IPurchaseInvoiceRepository>, PurchaseReceptionDocument> arrange
    )
    {
        var document = SampleDocument();
        var (handler, repo, purchaseRepo, _, provider, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider
            .Setup(p => p.GetAuthorizedXmlAsync(TenantId, CompanyId, document.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(true, document.AccessKey, DateTime.UtcNow, "<factura>...</factura>", null)
            );
        arrange(purchaseRepo, document);

        var result = await handler.Handle(new DownloadPurchaseReceptionXmlCommand(document.Id), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!;
    }

    [Fact]
    public async Task Handle_returns_DRAFT_when_the_related_purchase_is_an_active_draft()
    {
        var draftId = Guid.Empty;
        var dto = await DownloadWithPurchaseRepo((purchaseRepo, document) =>
        {
            var draft = PurchaseInvoice.CreateDraft(
                TenantId, CompanyId, BranchId, Guid.NewGuid(), "QUALA ECUADOR S A", "1791352688001",
                "01", "015-027-000161740", new DateOnly(2026, 7, 1), UserId, Guid.NewGuid(), "Contado",
                1, 30, accessKey: document.AccessKey
            );
            draftId = draft.Id;
            purchaseRepo
                .Setup(r => r.GetByAccessKeyAsync(TenantId, document.AccessKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(draft);
        });

        dto.PurchaseState.Should().Be("DRAFT");
        dto.PurchaseId.Should().Be(draftId);
        dto.CancelledPurchaseId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_returns_DISCARDED_and_history_id_when_the_last_purchase_was_discarded()
    {
        var discardedId = Guid.NewGuid();
        var dto = await DownloadWithPurchaseRepo((purchaseRepo, document) =>
            purchaseRepo
                .Setup(r =>
                    r.GetLatestHistoricalByAccessKeyAsync(TenantId, document.AccessKey, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(new HistoricalPurchaseRef(discardedId, Status: ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Discarded))
        );

        dto.PurchaseState.Should().Be("DISCARDED");
        dto.CancelledPurchaseId.Should().Be(discardedId);
    }
}
