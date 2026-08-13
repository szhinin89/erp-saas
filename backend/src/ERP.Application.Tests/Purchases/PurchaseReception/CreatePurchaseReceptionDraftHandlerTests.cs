using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using FluentAssertions;
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
        ItemMatchStatus matchStatus = ItemMatchStatus.Pending
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
            matchStatus: matchStatus
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
            tenant.Object,
            user.Object
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
}
