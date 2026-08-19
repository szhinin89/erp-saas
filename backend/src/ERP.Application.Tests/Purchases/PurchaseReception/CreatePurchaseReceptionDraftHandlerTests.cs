using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

public sealed class CreatePurchaseReceptionDraftHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private static PurchaseReceptionDocument SampleDocument(Guid? supplierId = null) =>
        PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1790012345001",
            "PROVEEDOR ACME S.A.",
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

    private static PurchaseReceptionLine SampleLine(
        Guid documentId,
        string description = "Producto de prueba",
        string? supplierCode = "SKU-001",
        Guid? itemId = null,
        ItemMatchStatus matchStatus = ItemMatchStatus.Pending,
        IEnumerable<(
            string TaxCode,
            string TaxRateCode,
            decimal Tarifa,
            decimal TaxableBase,
            decimal TaxAmount
        )>? taxes = null
    ) =>
        PurchaseReceptionLine.Create(
            documentId,
            TenantId,
            description,
            quantity: 2m,
            unitPrice: 10m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 3m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 20m,
            totalLine: 23m,
            supplierCode: supplierCode,
            itemId: itemId,
            matchStatus: matchStatus,
            taxes: taxes
        );

    private static (
        CreatePurchaseReceptionDraftHandler handler,
        Mock<IPurchaseReceptionDocumentRepository> repo,
        Mock<IPurchaseInvoiceRepository> purchaseRepo,
        Mock<IBusinessPartnerRepository> bpRepo,
        Mock<IPurchaseReceptionDetailProcessor> detailProcessor,
        Mock<IItemRepository> itemRepo
    ) BuildHandler()
    {
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        var purchaseRepo = new Mock<IPurchaseInvoiceRepository>();
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        var detailProcessor = new Mock<IPurchaseReceptionDetailProcessor>();
        var itemRepo = new Mock<IItemRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new CreatePurchaseReceptionDraftHandler(
            repo.Object,
            purchaseRepo.Object,
            bpRepo.Object,
            detailProcessor.Object,
            itemRepo.Object,
            new PurchaseXmlDraftParser(),
            tenant.Object,
            user.Object,
            NullLogger<CreatePurchaseReceptionDraftHandler>.Instance
        );
        return (handler, repo, purchaseRepo, bpRepo, detailProcessor, itemRepo);
    }

    [Fact]
    public async Task Handle_builds_a_draft_from_the_persisted_lines_of_a_verified_document()
    {
        var document = SampleDocument(SupplierId);
        var line = SampleLine(document.Id);
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var dto = result.Value!;
        dto.SupplierId.Should().Be(SupplierId);
        dto.SupplierRuc.Should().Be("1790012345001");
        dto.DocTypeCode.Should().Be("01");
        dto.InvoiceNumber.Should().Be("015-027-000161740");
        dto.AccessKey.Should().Be(document.AccessKey);
        dto.AuthorizationNumber.Should().Be("1234567890");
        dto.Lines.Should().ContainSingle();
        var lineDto = dto.Lines[0];
        lineDto.WarehouseId.Should().BeNull();
        lineDto.Description.Should().Be("Producto de prueba");
        lineDto.VatCode.Should().Be("2");
    }

    // ── Caso real reportado por el usuario: línea INCA-KOLA (factura Arca Continental) con IRBPNR
    // (código "5") — verifica que el impuesto sobrevive TODO el pipeline (persistencia simulada del
    // documento de recepción -> PurchaseDraft.FromReceptionDocument -> PurchaseDraftMapper.ToDto)
    // hasta PurchaseDraftLineDto.Taxes, sin volver a parsear el XML. ──
    [Fact]
    public async Task Handle_preserves_the_IRBPNR_tax_of_a_line_all_the_way_to_the_draft_dto()
    {
        var document = SampleDocument(SupplierId);
        var line = SampleLine(
            document.Id,
            description: "INCA-KOLA ORGL 900ML PET NR 12",
            supplierCode: "12469",
            taxes:
            [
                ("2", "4", 15.00m, 12.98m, 1.95m),
                ("5", "5001", 0.02m, 36.00m, 0.72m),
            ]
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var lineDto = result.Value!.Lines.Single();
        lineDto.Taxes.Should().HaveCount(2);
        var irbpnr = lineDto.Taxes.Should().ContainSingle(t => t.TaxCode == "5").Subject;
        irbpnr.TaxRateCode.Should().Be("5001");
        irbpnr.TaxAmount.Should().Be(0.72m);
        irbpnr.TaxableBase.Should().Be(36.00m);
        irbpnr.Tarifa.Should().Be(0.02m);
    }

    [Fact]
    public async Task Handle_leaves_supplier_null_when_the_reception_document_never_matched_one()
    {
        var document = SampleDocument(supplierId: null);
        var line = SampleLine(document.Id);
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.SupplierId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_nonexistent_document()
    {
        var (handler, repo, _, _, _, _) = BuildHandler();
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceptionDocument?)null);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(missingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Handle_rejects_a_document_that_has_not_downloaded_its_xml_yet()
    {
        var document = SampleDocument();
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    // ── Caso 1: línea sin matching → el borrador debe conservar ItemId null (frontend mostrará "Crear Item") ──
    [Fact]
    public async Task Handle_preserves_a_pending_line_without_an_item()
    {
        var document = SampleDocument(SupplierId);
        var line = SampleLine(document.Id, matchStatus: ItemMatchStatus.Pending);
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        var lineDto = result.Value!.Lines.Single();
        lineDto.ItemId.Should().BeNull();
        lineDto.ItemMatchStatus.Should().Be("PENDING");
    }

    // ── Caso 2: línea AutoMatched → el borrador debe conservar el ItemId ya resuelto ──
    [Fact]
    public async Task Handle_preserves_the_item_of_an_auto_matched_line()
    {
        var document = SampleDocument(SupplierId);
        var itemId = Guid.NewGuid();
        var line = SampleLine(
            document.Id,
            itemId: itemId,
            matchStatus: ItemMatchStatus.AutoMatched
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        var lineDto = result.Value!.Lines.Single();
        lineDto.ItemId.Should().Be(itemId);
        lineDto.ItemMatchStatus.Should().Be("AUTO_MATCHED");
    }

    // ── Caso 3: línea ManuallyMatched → el borrador debe conservar el ItemId elegido por el usuario ──
    [Fact]
    public async Task Handle_preserves_the_item_of_a_manually_matched_line()
    {
        var document = SampleDocument(SupplierId);
        var itemId = Guid.NewGuid();
        var line = SampleLine(
            document.Id,
            itemId: itemId,
            matchStatus: ItemMatchStatus.ManuallyMatched
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        var lineDto = result.Value!.Lines.Single();
        lineDto.ItemId.Should().Be(itemId);
        lineDto.ItemMatchStatus.Should().Be("MANUALLY_MATCHED");
    }

    [Fact]
    public async Task Handle_rehydrates_packaging_from_the_current_supplier_code_match()
    {
        var document = SampleDocument(SupplierId);
        var itemId = Guid.NewGuid();
        var packagingLevelId = Guid.NewGuid();
        var line = SampleLine(
            document.Id,
            supplierCode: "3172",
            itemId: itemId,
            matchStatus: ItemMatchStatus.AutoMatched
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, _, itemRepo) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        itemRepo
            .Setup(r =>
                r.GetSupplierCodeMatchAsync(
                    SupplierId,
                    "3172",
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ItemSupplierCodeMatch(
                    itemId,
                    packagingLevelId,
                    "PACA",
                    12m,
                    "UNIT"
                )
            );

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var lineDto = result.Value!.Lines.Single();
        lineDto.ItemId.Should().Be(itemId);
        lineDto.PackagingLevelId.Should().Be(packagingLevelId);
        lineDto.UomCode.Should().Be("PACA");
        lineDto.BaseUomCode.Should().Be("UNIT");
        lineDto.ConversionFactor.Should().Be(12m);
        lineDto.QuantityInBaseUom.Should().Be(24m);
    }

    [Fact]
    public async Task Handle_keeps_base_unit_when_supplier_code_has_no_packaging_level()
    {
        var document = SampleDocument(SupplierId);
        var itemId = Guid.NewGuid();
        var line = SampleLine(
            document.Id,
            supplierCode: "3172",
            itemId: itemId,
            matchStatus: ItemMatchStatus.AutoMatched
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, _, itemRepo) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        itemRepo
            .Setup(r =>
                r.GetSupplierCodeMatchAsync(
                    SupplierId,
                    "3172",
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ItemSupplierCodeMatch(itemId, null, null, null, "UNIT"));

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var lineDto = result.Value!.Lines.Single();
        lineDto.PackagingLevelId.Should().BeNull();
        lineDto.UomCode.Should().Be("UNIT");
        lineDto.BaseUomCode.Should().Be("UNIT");
        lineDto.ConversionFactor.Should().Be(1m);
        lineDto.QuantityInBaseUom.Should().Be(2m);
    }

    // ── Fase 5: nunca generar un draft "exitoso" vacío cuando el detalle no se pudo interpretar ──
    [Fact]
    public async Task Handle_rejects_a_document_whose_processing_is_still_failed_after_the_transparent_reprocessing_attempt()
    {
        // Caso NO recuperable: el reintento transparente sobre el XML ya guardado (interno a este
        // handler, nunca expuesto al usuario como "reprocesar") vuelve a fallar — no se genera un
        // draft vacío ni se abre el formulario de Compras.
        var document = SampleDocument(SupplierId);
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [],
            UserId,
            docTypeCode: null,
            sriPaymentMethodCode: null,
            processing: PurchaseReceptionProcessingOutcome.Failed(
                "El XML no tiene un elemento raíz."
            )
        );
        var (handler, repo, _, _, detailProcessor, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        detailProcessor
            .Setup(p =>
                p.ProcessAsync(
                    document.Id,
                    TenantId,
                    document.SupplierId,
                    document.XmlContent!,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PurchaseReceptionDetailProcessingResult(
                    [],
                    PurchaseReceptionProcessingOutcome.Failed("El XML no tiene un elemento raíz."),
                    null,
                    null,
                    null
                )
            );

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("El XML no tiene un elemento raíz.");
        detailProcessor.Verify(
            p =>
                p.ProcessAsync(
                    document.Id,
                    TenantId,
                    document.SupplierId,
                    document.XmlContent!,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_transparently_recovers_a_previously_failed_document_and_builds_the_draft()
    {
        // Caso recuperable: el snapshot no existía/estaba desactualizado (p. ej. un parser más
        // tolerante ya está disponible) — el reintento interno reconstruye las líneas y el draft
        // se arma con normalidad, sin que el usuario perciba ningún paso adicional.
        var document = SampleDocument(SupplierId);
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [],
            UserId,
            docTypeCode: null,
            sriPaymentMethodCode: null,
            processing: PurchaseReceptionProcessingOutcome.Failed(
                "El parser anterior no pudo interpretar el detalle."
            )
        );
        var (handler, repo, _, _, detailProcessor, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        var recoveredLine = SampleLine(document.Id);
        detailProcessor
            .Setup(p =>
                p.ProcessAsync(
                    document.Id,
                    TenantId,
                    document.SupplierId,
                    document.XmlContent!,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PurchaseReceptionDetailProcessingResult(
                    [recoveredLine],
                    new PurchaseReceptionProcessingOutcome(
                        PurchaseReceptionProcessingStatus.Processed,
                        1,
                        1,
                        null
                    ),
                    "01",
                    "01",
                    null
                )
            );

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().ContainSingle();
        result.Value.ProcessingStatus.Should().Be("PROCESSED");
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Cierre de arquitectura: la reconstrucción queda persistida — la segunda vez NO vuelve a
    // reconstruir, carga directo desde el snapshot ya reparado. ──
    [Fact]
    public async Task Handle_reconstructs_only_once_and_loads_directly_from_the_repaired_snapshot_on_a_second_call()
    {
        var document = SampleDocument(SupplierId);
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [],
            UserId,
            docTypeCode: null,
            sriPaymentMethodCode: null,
            processing: PurchaseReceptionProcessingOutcome.Failed(
                "El parser anterior no pudo interpretar el detalle."
            )
        );
        var (handler, repo, _, _, detailProcessor, _) = BuildHandler();
        // El repositorio simula persistencia real: siempre devuelve la MISMA instancia mutable, así
        // que el efecto de ReprocessDetail() en el primer Handle() es visible en el segundo.
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        var recoveredLine = SampleLine(document.Id);
        detailProcessor
            .Setup(p =>
                p.ProcessAsync(
                    document.Id,
                    TenantId,
                    document.SupplierId,
                    document.XmlContent!,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PurchaseReceptionDetailProcessingResult(
                    [recoveredLine],
                    new PurchaseReceptionProcessingOutcome(
                        PurchaseReceptionProcessingStatus.Processed,
                        1,
                        1,
                        null
                    ),
                    "01",
                    "01",
                    null
                )
            );

        // Primer intento: snapshot Failed -> reconstrucción -> persistencia -> compra.
        var firstResult = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );
        firstResult.IsSuccess.Should().BeTrue(firstResult.Error);
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Processed);
        document.Lines.Should().ContainSingle();

        // Segundo intento: snapshot ya reconstruido -> no vuelve a reconstruir -> carga directa.
        var secondResult = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );
        secondResult.IsSuccess.Should().BeTrue(secondResult.Error);
        secondResult.Value!.Lines.Should().ContainSingle();

        detailProcessor.Verify(
            p =>
                p.ProcessAsync(
                    document.Id,
                    TenantId,
                    document.SupplierId,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Regla: la reconstrucción automática SOLO ocurre en ProcessingStatus.Failed — nunca en
    // Processed ni ProcessedWithWarnings, para no afectar el rendimiento de la carga normal. ──
    [Fact]
    public async Task Handle_never_reconstructs_a_document_that_is_already_processed()
    {
        var document = SampleDocument(SupplierId);
        var line = SampleLine(document.Id);
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, detailProcessor, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        detailProcessor.Verify(
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

    [Fact]
    public async Task Handle_never_reconstructs_a_document_with_processing_warnings()
    {
        var document = SampleDocument(SupplierId);
        var line = SampleLine(document.Id);
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.ProcessedWithWarnings,
                2,
                1,
                "Línea 2: omitida."
            )
        );
        var (handler, repo, _, _, detailProcessor, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        detailProcessor.Verify(
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

    // ── ProcessedWithWarnings: el draft se genera igual con las líneas buenas, con aviso ──
    [Fact]
    public async Task Handle_succeeds_with_a_warning_when_processing_had_partial_failures()
    {
        var document = SampleDocument(SupplierId);
        var line = SampleLine(document.Id);
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "<factura>irrelevante</factura>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.ProcessedWithWarnings,
                2,
                1,
                "Línea 2 (SKU-999): La línea no tiene impuesto IVA. — línea omitida."
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().ContainSingle();
        result.Value.ProcessingStatus.Should().Be("PROCESSED_WITH_WARNINGS");
        result.Value.ProcessingNotes.Should().Contain("SKU-999");
    }

    // ── FLOW-READY-02F.2: fusión en memoria de xml_content re-parseado + Item Matching persistido ──

    // Mismo fixture real de producción que PurchaseXmlDraftParserTests
    // ("Parses_the_real_ArcaContinental_XML_reported_by_the_user_with_IRBPNR_on_both_lines") —
    // factura 029-001-001293714, BEBIDAS ARCACONTINENTAL. Reutilizado tal cual, no reinventado.
    private const string ArcaContinentalXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <factura id="comprobante" version="2.1.0">
          <infoTributaria>
            <ambiente>2</ambiente>
            <tipoEmision>1</tipoEmision>
            <razonSocial>BEBIDAS ARCACONTINENTAL ECUADOR ARCADOR C.L.</razonSocial>
            <nombreComercial>BEBIDAS ARCACONTINENTAL ECUADOR ARCADOR C.L.</nombreComercial>
            <ruc>1792411149001</ruc>
            <claveAcceso>0108202601179241114900120290010012937140129371419</claveAcceso>
            <codDoc>01</codDoc>
            <estab>029</estab>
            <ptoEmi>001</ptoEmi>
            <secuencial>001293714</secuencial>
            <dirMatriz>PANAMERICANA NORTE OE9-166 Y JOSE VITERI KM 15</dirMatriz>
          </infoTributaria>
          <infoFactura>
            <fechaEmision>01/08/2026</fechaEmision>
            <dirEstablecimiento>AV. 16 DE ABRIL , Y CALLE S/N</dirEstablecimiento>
            <contribuyenteEspecial>00082</contribuyenteEspecial>
            <obligadoContabilidad>SI</obligadoContabilidad>
            <tipoIdentificacionComprador>05</tipoIdentificacionComprador>
            <razonSocialComprador>ZHININ ZHININ SEGUNDO FERNANDO - ZHININ ZHININ SEGUNDO FERNANDO</razonSocialComprador>
            <identificacionComprador>0350016432</identificacionComprador>
            <direccionComprador>CA AR                         CENTRO</direccionComprador>
            <totalSinImpuestos>74.39</totalSinImpuestos>
            <totalDescuento>0.00</totalDescuento>
            <totalConImpuestos>
              <totalImpuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><baseImponible>80.84</baseImponible><tarifa>15.00</tarifa><valor>12.13</valor></totalImpuesto>
              <totalImpuesto><codigo>3</codigo><codigoPorcentaje>3053</codigoPorcentaje><baseImponible>35.81</baseImponible><tarifa>0.18</tarifa><valor>6.45</valor></totalImpuesto>
              <totalImpuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><baseImponible>132.00</baseImponible><tarifa>0.02</tarifa><valor>2.64</valor></totalImpuesto>
            </totalConImpuestos>
            <propina>0.00</propina>
            <importeTotal>95.60</importeTotal>
            <moneda>DOLAR</moneda>
            <pagos><pago><formaPago>01</formaPago><total>95.60</total><plazo>8</plazo><unidadTiempo>dias</unidadTiempo></pago></pagos>
            <valorRetIva>0</valorRetIva>
            <valorRetRenta>0</valorRetRenta>
          </infoFactura>
          <detalles>
            <detalle>
              <codigoPrincipal>0580</codigoPrincipal>
              <descripcion>SPRITE HARMONY 1350 PET(12)</descripcion>
              <cantidad>1.000000</cantidad>
              <precioUnitario>10.72130</precioUnitario>
              <descuento>0.00</descuento>
              <precioTotalSinImpuesto>10.72</precioTotalSinImpuesto>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>10.72</baseImponible><valor>1.61</valor></impuesto>
                <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>12.00</baseImponible><valor>0.24</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>12469</codigoPrincipal>
              <descripcion>INCA-KOLA ORGL 900ML PET NR 12</descripcion>
              <cantidad>3.000000</cantidad>
              <precioUnitario>4.32817</precioUnitario>
              <descuento>0.00</descuento>
              <precioTotalSinImpuesto>12.98</precioTotalSinImpuesto>
              <detallesAdicionales>
                <detAdicional nombre="Unidad" valor="3 /  0"/>
                <detAdicional nombre="valor2" valor="0.72"/>
              </detallesAdicionales>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>12.98</baseImponible><valor>1.95</valor></impuesto>
                <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>36.00</baseImponible><valor>0.72</valor></impuesto>
              </impuestos>
            </detalle>
          </detalles>
        </factura>
        """;

    // Sintético (no es el XML real) — dos <detalle> con el MISMO codigoPrincipal y la MISMA
    // descripcion, para forzar un grupo de correlación con count>1 y probar que no se cruza
    // matching/impuestos entre líneas del mismo grupo.
    private const string DuplicateSupplierCodeXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <factura id="comprobante" version="2.1.0">
          <infoTributaria>
            <razonSocial>PROVEEDOR DUPLICADOS S.A.</razonSocial>
            <ruc>1790012345001</ruc>
            <codDoc>01</codDoc>
            <estab>001</estab>
            <ptoEmi>001</ptoEmi>
            <secuencial>000000456</secuencial>
          </infoTributaria>
          <infoFactura>
            <fechaEmision>01/08/2026</fechaEmision>
          </infoFactura>
          <detalles>
            <detalle>
              <codigoPrincipal>999</codigoPrincipal>
              <descripcion>PRODUCTO DUPLICADO</descripcion>
              <cantidad>1.000000</cantidad>
              <precioUnitario>10.00000</precioUnitario>
              <descuento>0.00</descuento>
              <precioTotalSinImpuesto>10.00</precioTotalSinImpuesto>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>10.00</baseImponible><valor>1.50</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>999</codigoPrincipal>
              <descripcion>PRODUCTO DUPLICADO</descripcion>
              <cantidad>2.000000</cantidad>
              <precioUnitario>20.00000</precioUnitario>
              <descuento>0.00</descuento>
              <precioTotalSinImpuesto>40.00</precioTotalSinImpuesto>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>40.00</baseImponible><valor>6.00</valor></impuesto>
              </impuestos>
            </detalle>
          </detalles>
        </factura>
        """;

    // Igual que DuplicateSupplierCodeXml, pero con un TERCER <detalle> del mismo grupo de
    // correlación (codigoPrincipal="999" + misma descripción) — fuerza fresco(3) != persistido(2),
    // el caso concreto que hace caer al merger al fallback "sin fusión" por ambigüedad.
    private const string TripleDuplicateSupplierCodeXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <factura id="comprobante" version="2.1.0">
          <infoTributaria>
            <razonSocial>PROVEEDOR DUPLICADOS S.A.</razonSocial>
            <ruc>1790012345001</ruc>
            <codDoc>01</codDoc>
            <estab>001</estab>
            <ptoEmi>001</ptoEmi>
            <secuencial>000000456</secuencial>
          </infoTributaria>
          <infoFactura>
            <fechaEmision>01/08/2026</fechaEmision>
          </infoFactura>
          <detalles>
            <detalle>
              <codigoPrincipal>999</codigoPrincipal>
              <descripcion>PRODUCTO DUPLICADO</descripcion>
              <cantidad>1.000000</cantidad>
              <precioUnitario>10.00000</precioUnitario>
              <descuento>0.00</descuento>
              <precioTotalSinImpuesto>10.00</precioTotalSinImpuesto>
              <detallesAdicionales>
                <detAdicional nombre="LOTE" valor="AAA"/>
              </detallesAdicionales>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>10.00</baseImponible><valor>1.50</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>999</codigoPrincipal>
              <descripcion>PRODUCTO DUPLICADO</descripcion>
              <cantidad>2.000000</cantidad>
              <precioUnitario>20.00000</precioUnitario>
              <descuento>0.00</descuento>
              <precioTotalSinImpuesto>40.00</precioTotalSinImpuesto>
              <detallesAdicionales>
                <detAdicional nombre="LOTE" valor="BBB"/>
              </detallesAdicionales>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>40.00</baseImponible><valor>6.00</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>999</codigoPrincipal>
              <descripcion>PRODUCTO DUPLICADO</descripcion>
              <cantidad>3.000000</cantidad>
              <precioUnitario>30.00000</precioUnitario>
              <descuento>0.00</descuento>
              <precioTotalSinImpuesto>90.00</precioTotalSinImpuesto>
              <detallesAdicionales>
                <detAdicional nombre="LOTE" valor="CCC"/>
              </detallesAdicionales>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>90.00</baseImponible><valor>13.50</valor></impuesto>
              </impuestos>
            </detalle>
          </detalles>
        </factura>
        """;

    // ── Caso 1: recepción antigua — purchase_reception_line_taxes vacío para INCA-KOLA, pero
    // xml_content sí tiene el detalle completo. El impuesto IRBPNR debe salir del re-parseo. ──
    [Fact]
    public async Task Handle_recovers_missing_line_taxes_from_xml_content_when_the_persisted_snapshot_has_none()
    {
        var document = SampleDocument(SupplierId);
        var incaKolaLine = SampleLine(
            document.Id,
            description: "INCA-KOLA ORGL 900ML PET NR 12",
            supplierCode: "12469",
            taxes: null // recepción antigua: nunca se persistió el detalle de impuestos
        );
        var spriteLine = SampleLine(
            document.Id,
            description: "SPRITE HARMONY 1350 PET(12)",
            supplierCode: "0580",
            taxes: null
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            ArcaContinentalXml,
            DateTime.UtcNow,
            [spriteLine, incaKolaLine],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                2,
                2,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var incaKolaDto = result
            .Value!.Lines.Should()
            .ContainSingle(l => l.SupplierCode == "12469")
            .Subject;
        incaKolaDto.Taxes.Should().HaveCount(2);
        var irbpnr = incaKolaDto.Taxes.Should().ContainSingle(t => t.TaxCode == "5").Subject;
        irbpnr.TaxAmount.Should().Be(0.72m);
        irbpnr.TaxableBase.Should().Be(36.00m);
    }

    // ── Caso 2: preservación de matching — ItemId/MatchStatus ya resueltos por el usuario deben
    // conservarse intactos aunque los datos fiscales vengan frescos del re-parseo del XML. ──
    [Fact]
    public async Task Handle_preserves_resolved_matching_while_refreshing_taxes_from_xml_content()
    {
        var document = SampleDocument(SupplierId);
        var itemId = Guid.NewGuid();
        var incaKolaLine = SampleLine(
            document.Id,
            description: "INCA-KOLA ORGL 900ML PET NR 12",
            supplierCode: "12469",
            itemId: itemId,
            matchStatus: ItemMatchStatus.ManuallyMatched,
            taxes: null
        );
        var spriteLine = SampleLine(
            document.Id,
            description: "SPRITE HARMONY 1350 PET(12)",
            supplierCode: "0580",
            taxes: null
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            ArcaContinentalXml,
            DateTime.UtcNow,
            [spriteLine, incaKolaLine],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                2,
                2,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var incaKolaDto = result
            .Value!.Lines.Should()
            .ContainSingle(l => l.SupplierCode == "12469")
            .Subject;
        incaKolaDto.ItemId.Should().Be(itemId);
        incaKolaDto.ItemMatchStatus.Should().Be("MANUALLY_MATCHED");
        var irbpnr = incaKolaDto.Taxes.Should().ContainSingle(t => t.TaxCode == "5").Subject;
        irbpnr.TaxAmount.Should().Be(0.72m);
    }

    // ── Caso 3: dos líneas persistidas con el mismo SupplierCode/Description (grupo count=2) no se
    // cruzan entre sí — cada una toma el matching y los impuestos frescos que le corresponden por
    // orden de enumeración dentro del grupo. ──
    [Fact]
    public async Task Handle_does_not_cross_match_or_taxes_between_lines_sharing_the_same_correlation_key()
    {
        var document = SampleDocument(SupplierId);
        var firstItemId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();
        var firstLine = SampleLine(
            document.Id,
            description: "PRODUCTO DUPLICADO",
            supplierCode: "999",
            itemId: firstItemId,
            matchStatus: ItemMatchStatus.ManuallyMatched,
            taxes: null
        );
        var secondLine = SampleLine(
            document.Id,
            description: "PRODUCTO DUPLICADO",
            supplierCode: "999",
            itemId: secondItemId,
            matchStatus: ItemMatchStatus.AutoMatched,
            taxes: null
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            DuplicateSupplierCodeXml,
            DateTime.UtcNow,
            [firstLine, secondLine],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                2,
                2,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var lines = result.Value!.Lines;
        lines.Should().HaveCount(2);

        // Cada línea del grupo se fusiona con datos frescos (no se queda con el snapshot vacío) y
        // conserva ÚNICAMENTE su propio ItemId — nunca el del otro miembro del grupo.
        var itemIds = lines.Select(l => l.ItemId).ToList();
        itemIds.Should().Contain(firstItemId);
        itemIds.Should().Contain(secondItemId);

        var firstDto = lines.Single(l => l.ItemId == firstItemId);
        var secondDto = lines.Single(l => l.ItemId == secondItemId);
        // Cada línea debe tener exactamente un impuesto IVA fresco (no ambos vacíos, no duplicados).
        firstDto.Taxes.Should().ContainSingle(t => t.TaxCode == "2");
        secondDto.Taxes.Should().ContainSingle(t => t.TaxCode == "2");
        // Los montos de las dos líneas frescas son distintos (1.50 vs 6.00) — si hubiera cruce, una
        // de las dos líneas repetiría el monto de la otra o quedaría con el snapshot vacío (0 impuestos).
        var taxAmounts = lines.SelectMany(l => l.Taxes).Select(t => t.TaxAmount).OrderBy(v => v).ToList();
        taxAmounts.Should().Equal(1.50m, 6.00m);
    }

    // ── Caso 5: XML no parseable — no debe fallar la operación ni perder las líneas persistidas. ──
    [Fact]
    public async Task Handle_falls_back_to_persisted_lines_when_xml_content_cannot_be_parsed()
    {
        var document = SampleDocument(SupplierId);
        var line = SampleLine(
            document.Id,
            taxes: [("2", "4", 15.00m, 20.00m, 3.00m)]
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            "esto no es un XML valido en absoluto",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var lineDto = result.Value!.Lines.Single();
        lineDto.Description.Should().Be("Producto de prueba");
        lineDto.Taxes.Should().ContainSingle(t => t.TaxCode == "2" && t.TaxAmount == 3.00m);
        document.Lines.Should().ContainSingle();
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Processed);
    }

    // ── Caso 6: caso real INCA-KOLA end-to-end — valores fiscales exactos que el usuario reportó. ──
    [Fact]
    public async Task Handle_produces_the_exact_fiscal_values_for_the_real_IncaKola_line()
    {
        var document = SampleDocument(SupplierId);
        var incaKolaLine = SampleLine(
            document.Id,
            description: "INCA-KOLA ORGL 900ML PET NR 12",
            supplierCode: "12469",
            taxes: null
        );
        var spriteLine = SampleLine(
            document.Id,
            description: "SPRITE HARMONY 1350 PET(12)",
            supplierCode: "0580",
            taxes: null
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            ArcaContinentalXml,
            DateTime.UtcNow,
            [spriteLine, incaKolaLine],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                2,
                2,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var incaKolaDto = result
            .Value!.Lines.Should()
            .ContainSingle(l => l.SupplierCode == "12469")
            .Subject;
        incaKolaDto.VatPercentage.Should().Be(15.00m);
        incaKolaDto.TaxValue.Should().Be(1.95m);
        var vat = incaKolaDto.Taxes.Should().ContainSingle(t => t.TaxCode == "2").Subject;
        vat.TaxableBase.Should().Be(12.98m);
        vat.TaxAmount.Should().Be(1.95m);
        var irbpnr = incaKolaDto.Taxes.Should().ContainSingle(t => t.TaxCode == "5").Subject;
        irbpnr.TaxAmount.Should().Be(0.72m);
        incaKolaDto.TotalLine.Should().Be(15.65m);

        // PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — el mismo caso real también debe mostrar los
        // detAdicional de la línea, sin afectar los valores fiscales verificados arriba.
        incaKolaDto.AdditionalFields.Should().HaveCount(2);
        incaKolaDto.AdditionalFields.Should().Contain(f => f.Name == "Unidad" && f.Value == "3 /  0");
        incaKolaDto.AdditionalFields.Should().Contain(f => f.Name == "valor2" && f.Value == "0.72");
    }

    // ── PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — recepción antigua: la línea persistida nunca tuvo
    // AdditionalFields (no existía la columna cuando se importó), pero xml_content sí trae el detalle
    // completo. El draft debe obtener los datos adicionales del re-parseo, igual que ya hace con
    // Taxes/IRBPNR — sin necesidad de reimportar el XML ni de backfill. ──
    [Fact]
    public async Task Handle_recovers_missing_line_additional_fields_from_xml_content_while_preserving_matching()
    {
        var document = SampleDocument(SupplierId);
        var itemId = Guid.NewGuid();
        var incaKolaLine = SampleLine(
            document.Id,
            description: "INCA-KOLA ORGL 900ML PET NR 12",
            supplierCode: "12469",
            itemId: itemId,
            matchStatus: ItemMatchStatus.ManuallyMatched,
            taxes: null // recepción antigua: ni Taxes ni AdditionalFields se persistieron nunca
        );
        var spriteLine = SampleLine(
            document.Id,
            description: "SPRITE HARMONY 1350 PET(12)",
            supplierCode: "0580",
            taxes: null
        );
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            ArcaContinentalXml,
            DateTime.UtcNow,
            [spriteLine, incaKolaLine],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                2,
                2,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var incaKolaDto = result
            .Value!.Lines.Should()
            .ContainSingle(l => l.SupplierCode == "12469")
            .Subject;

        // Documental: viene del XML fresco re-parseado.
        incaKolaDto.AdditionalFields.Should().HaveCount(2);
        incaKolaDto.AdditionalFields.Should().Contain(f => f.Name == "Unidad" && f.Value == "3 /  0");
        incaKolaDto.AdditionalFields.Should().Contain(f => f.Name == "valor2" && f.Value == "0.72");

        // Operativo: el matching manual ya resuelto nunca se pierde por refrescar lo documental.
        incaKolaDto.ItemId.Should().Be(itemId);
        incaKolaDto.ItemMatchStatus.Should().Be("MANUALLY_MATCHED");

        // SPRITE no trae detallesAdicionales en el XML real.
        var spriteDto = result.Value.Lines.Single(l => l.SupplierCode == "0580");
        spriteDto.AdditionalFields.Should().BeEmpty();
    }

    // ── PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — cuando el merger cae al fallback "sin fusión" por
    // ambigüedad (mismo SupplierCode+Description, conteo N≠M), cada línea persistida debe conservar
    // ÚNICAMENTE sus propios AdditionalFields — nunca los de la otra línea del mismo grupo. ──
    [Fact]
    public async Task Handle_does_not_cross_additional_fields_between_lines_sharing_the_same_correlation_key()
    {
        var document = SampleDocument(SupplierId);
        var firstLine = SampleLine(
            document.Id,
            description: "PRODUCTO DUPLICADO",
            supplierCode: "999",
            taxes: null
        );
        var secondLine = SampleLine(
            document.Id,
            description: "PRODUCTO DUPLICADO",
            supplierCode: "999",
            taxes: null
        );
        // Solo UNA línea persistida trae XML nuevo con 2 <detalle> del mismo grupo (count=2 en ambos
        // lados) — se prueba el otro camino: forzar el fallback agregando una TERCERA línea fresca
        // al XML para que persistido(2) != fresco(3) en ese grupo, cayendo a "sin fusión".
        document.AttachSriAuthorization(
            "1234567890",
            DateTime.UtcNow,
            TripleDuplicateSupplierCodeXml,
            DateTime.UtcNow,
            [firstLine, secondLine],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "01",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                2,
                2,
                null
            )
        );
        var (handler, repo, _, _, _, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new CreatePurchaseReceptionDraftCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var lines = result.Value!.Lines;
        lines.Should().HaveCount(2);
        // Fallback sin fusión: cada línea conserva su propio snapshot persistido (vacío, en este
        // caso, porque nunca se le asignaron AdditionalFields) — nunca los del XML fresco ambiguo.
        lines.Should().OnlyContain(l => l.AdditionalFields.Count == 0);
    }
}
