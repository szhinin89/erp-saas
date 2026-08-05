using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// P0-02 Fase 5 — pruebas de <c>CreatePurchaseReturnDraftHandler</c>/<c>UpdatePurchaseReturnDraftHandler</c>/
/// <c>CancelPurchaseReturnDraftHandler</c>: creación válida, rechazos PR-001/PR-002/PR-003/PR-004,
/// transiciones solo-Draft, idempotencia (§16.2) y Branch Ownership Rule (§5.2).
/// </summary>
public sealed class PurchaseReturnDraftUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    private static PurchaseInvoice ConfirmedInvoice(int lineCount = 1)
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PaymentTermId,
            "Contado",
            1,
            30,
            globalWarehouseId: WarehouseId
        );

        var lines = new List<PurchaseInvoiceDetail>();
        for (var i = 0; i < lineCount; i++)
        {
            lines.Add(
                PurchaseInvoiceDetail.Create(
                    inv.Id,
                    TenantId,
                    $"Producto {i + 1}",
                    quantity: 10,
                    unitPrice: 10.00m,
                    vatCode: "10",
                    uomCode: "UNIT",
                    itemId: Guid.NewGuid(),
                    warehouseId: WarehouseId
                )
            );
        }
        inv.ReplaceLines(lines, UserId);
        inv.Confirm(UserId);
        return inv;
    }

    private static (
        CreatePurchaseReturnDraftHandler handler,
        Mock<IPurchaseReturnRepository> returnRepo,
        Mock<IPurchaseInvoiceRepository> invoiceRepo
    ) BuildCreateHandler()
    {
        var returnRepo = new Mock<IPurchaseReturnRepository>();
        var invoiceRepo = new Mock<IPurchaseInvoiceRepository>();
        var dbEx = new Mock<IDatabaseExceptionTranslator>();
        var t = new Mock<ICurrentTenant>();
        t.SetupGet(x => x.TenantId).Returns(TenantId);
        var c = new Mock<ICurrentCompany>();
        c.SetupGet(x => x.CompanyId).Returns(CompanyId);
        var b = new Mock<ICurrentBranch>();
        b.SetupGet(x => x.BranchId).Returns(BranchId);
        var u = new Mock<ICurrentUser>();
        u.SetupGet(x => x.UserId).Returns(UserId);

        returnRepo
            .Setup(r =>
                r.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, decimal>());

        var handler = new CreatePurchaseReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            dbEx.Object,
            t.Object,
            c.Object,
            b.Object,
            u.Object
        );
        return (handler, returnRepo, invoiceRepo);
    }

    private static CreatePurchaseReturnDraftCommand CommandFor(
        PurchaseInvoice invoice,
        Guid? clientRequestId = null
    ) =>
        new(
            clientRequestId ?? Guid.NewGuid(),
            invoice.Id,
            "Producto en mal estado",
            invoice.Lines.Select(l => new PurchaseReturnDraftLineInput(l.Id, l.Quantity)).ToList()
        );

    // ── Creación válida ────────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_una_linea_es_valido_y_persiste_una_sola_vez()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = ConfirmedInvoice(lineCount: 1);
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PurchaseReturn?)null);

        var result = await handler.Handle(CommandFor(invoice), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(1);
        returnRepo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseReturn>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        returnRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDraft_multiples_lineas_es_valido()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = ConfirmedInvoice(lineCount: 3);
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PurchaseReturn?)null);

        var result = await handler.Handle(CommandFor(invoice), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateDraft_persiste_BranchId_igual_a_ICurrentBranch_del_handler()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = ConfirmedInvoice();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PurchaseReturn?)null);
        PurchaseReturn? added = null;
        returnRepo
            .Setup(r => r.AddAsync(It.IsAny<PurchaseReturn>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseReturn, CancellationToken>((pr, _) => added = pr)
            .Returns(Task.CompletedTask);

        await handler.Handle(CommandFor(invoice), CancellationToken.None);

        added.Should().NotBeNull();
        added!.BranchId.Should().Be(BranchId);
    }

    [Fact]
    public void CreatePurchaseReturnDraftCommand_no_expone_BranchId()
    {
        typeof(CreatePurchaseReturnDraftCommand)
            .GetProperties()
            .Select(p => p.Name)
            .Should()
            .NotContain("BranchId");
    }

    // ── Rechazos PR-001/PR-002/PR-003/PR-004 ─────────────────────────────

    [Fact]
    public async Task CreateDraft_factura_inexistente_retorna_NotFound_PR_001()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoiceId = Guid.NewGuid();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PurchaseReturn?)null);

        var cmd = new CreatePurchaseReturnDraftCommand(
            Guid.NewGuid(),
            invoiceId,
            "Motivo",
            new[] { new PurchaseReturnDraftLineInput(Guid.NewGuid(), 1) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task CreateDraft_factura_no_confirmada_rechaza_PR_002()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000002",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PaymentTermId,
            "Contado",
            1,
            30,
            globalWarehouseId: WarehouseId
        );
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PurchaseReturn?)null);

        var cmd = new CreatePurchaseReturnDraftCommand(
            Guid.NewGuid(),
            invoice.Id,
            "Motivo",
            new[] { new PurchaseReturnDraftLineInput(Guid.NewGuid(), 1) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task CreateDraft_linea_que_no_pertenece_a_la_factura_rechaza_PR_003()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = ConfirmedInvoice();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PurchaseReturn?)null);

        var cmd = new CreatePurchaseReturnDraftCommand(
            Guid.NewGuid(),
            invoice.Id,
            "Motivo",
            new[] { new PurchaseReturnDraftLineInput(Guid.NewGuid(), 1) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        returnRepo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseReturn>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateDraft_cantidad_excede_remanente_rechaza_PR_004()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = ConfirmedInvoice();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PurchaseReturn?)null);

        var line = invoice.Lines.Single();
        var cmd = new CreatePurchaseReturnDraftCommand(
            Guid.NewGuid(),
            invoice.Id,
            "Motivo",
            new[] { new PurchaseReturnDraftLineInput(line.Id, line.Quantity + 1) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        returnRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateDraft_considera_cantidad_ya_devuelta_al_calcular_remanente()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = ConfirmedInvoice();
        var line = invoice.Lines.Single();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PurchaseReturn?)null);
        returnRepo
            .Setup(r =>
                r.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, decimal> { [line.Id] = line.Quantity - 1 });

        var cmd = new CreatePurchaseReturnDraftCommand(
            Guid.NewGuid(),
            invoice.Id,
            "Motivo",
            new[] { new PurchaseReturnDraftLineInput(line.Id, 2) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    // ── Idempotencia (§16.2) ──────────────────────────────────────────────

    [Fact]
    public async Task CreateDraft_mismo_ClientRequestId_y_mismo_contenido_retorna_el_draft_ya_creado_sin_duplicar()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = ConfirmedInvoice();
        var line = invoice.Lines.Single();
        var clientRequestId = Guid.NewGuid();
        var cmd = new CreatePurchaseReturnDraftCommand(
            clientRequestId,
            invoice.Id,
            "Motivo",
            new[] { new PurchaseReturnDraftLineInput(line.Id, 1) }
        );
        var hash = CreatePurchaseReturnDraftHandler.ComputePayloadHash(
            cmd.PurchaseInvoiceId,
            cmd.Reason,
            cmd.Lines
        );
        var existingDraft = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            invoice.Id,
            SupplierId,
            "Motivo",
            new[]
            {
                new PurchaseReturn.DraftLineInput(line.Id, line.ItemId!.Value, 1, WarehouseId),
            },
            UserId,
            clientRequestId,
            hash
        );
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    clientRequestId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(existingDraft);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(existingDraft.Id);
        returnRepo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseReturn>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        invoiceRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateDraft_mismo_ClientRequestId_con_payload_distinto_rechaza_PR_012_sin_tocar_el_original()
    {
        var (handler, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = ConfirmedInvoice();
        var clientRequestId = Guid.NewGuid();
        var existingDraft = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            invoice.Id,
            SupplierId,
            "Motivo original",
            new[]
            {
                new PurchaseReturn.DraftLineInput(
                    invoice.Lines.Single().Id,
                    invoice.Lines.Single().ItemId!.Value,
                    1,
                    WarehouseId
                ),
            },
            UserId,
            clientRequestId,
            "hash-original-distinto"
        );
        returnRepo
            .Setup(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    clientRequestId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(existingDraft);

        var cmd = new CreatePurchaseReturnDraftCommand(
            clientRequestId,
            invoice.Id,
            "Motivo completamente distinto",
            new[] { new PurchaseReturnDraftLineInput(invoice.Lines.Single().Id, 5) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        returnRepo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseReturn>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateDraft_ante_violacion_unica_concurrente_reconsulta_y_retorna_el_ganador_si_el_hash_coincide()
    {
        var (_, returnRepo, invoiceRepo) = BuildCreateHandler();
        var invoice = ConfirmedInvoice();
        var line = invoice.Lines.Single();
        var clientRequestId = Guid.NewGuid();
        var cmd = new CreatePurchaseReturnDraftCommand(
            clientRequestId,
            invoice.Id,
            "Motivo",
            new[] { new PurchaseReturnDraftLineInput(line.Id, 1) }
        );
        var hash = CreatePurchaseReturnDraftHandler.ComputePayloadHash(
            cmd.PurchaseInvoiceId,
            cmd.Reason,
            cmd.Lines
        );
        var winner = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            invoice.Id,
            SupplierId,
            "Motivo",
            new[]
            {
                new PurchaseReturn.DraftLineInput(line.Id, line.ItemId!.Value, 1, WarehouseId),
            },
            UserId,
            clientRequestId,
            hash
        );

        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        var dbEx = new Mock<IDatabaseExceptionTranslator>();
        var info = new DatabaseUniqueViolationInfo(
            "23505",
            "uq_purchase_returns_tenant_create_client_request_id",
            "purchase_returns",
            null
        );
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out info)).Returns(true);

        var callCount = 0;
        returnRepo
            .SetupSequence(r =>
                r.GetByCreateClientRequestIdAsync(
                    TenantId,
                    clientRequestId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PurchaseReturn?)null) // primera búsqueda: no existe todavía
            .ReturnsAsync(winner); // reconsulta tras la violación única: la otra transacción ya ganó
        returnRepo
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ThrowsAsync(new InvalidOperationException("unique violation simulada"));

        var t = new Mock<ICurrentTenant>();
        t.SetupGet(x => x.TenantId).Returns(TenantId);
        var c = new Mock<ICurrentCompany>();
        c.SetupGet(x => x.CompanyId).Returns(CompanyId);
        var b = new Mock<ICurrentBranch>();
        b.SetupGet(x => x.BranchId).Returns(BranchId);
        var u = new Mock<ICurrentUser>();
        u.SetupGet(x => x.UserId).Returns(UserId);
        var handler = new CreatePurchaseReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            dbEx.Object,
            t.Object,
            c.Object,
            b.Object,
            u.Object
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(winner.Id);
        callCount.Should().Be(1);
    }

    // ── Update/Cancel solo permitido en Draft ────────────────────────────

    [Fact]
    public async Task CancelDraft_de_una_devolucion_ya_autorizada_rechaza_PR_009()
    {
        var invoice = ConfirmedInvoice();
        var line = invoice.Lines.Single();
        var purchaseReturn = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            invoice.Id,
            SupplierId,
            "Motivo",
            new[]
            {
                new PurchaseReturn.DraftLineInput(line.Id, line.ItemId!.Value, 1, WarehouseId),
            },
            UserId,
            Guid.NewGuid(),
            "hash"
        );
        var originalLine = new PurchaseReturn.OriginalLineSnapshot(
            line.Quantity,
            line.LineSubtotal,
            line.DiscountAmount,
            line.VatAmount,
            line.IceAmount,
            line.VatCode,
            line.VatRate,
            line.IceCode,
            line.IceRate,
            line.LandedUnitCost
        );
        purchaseReturn.Authorize(
            "001-001-000000001",
            new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot> { [line.Id] = originalLine },
            balanceDueBeforeApplication: 1000m,
            currencyCode: "USD",
            hasIssuedWithholding: false,
            UserId,
            Guid.NewGuid(),
            "hash-authorize"
        );

        var repo = new Mock<IPurchaseReturnRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseReturn);
        var t = new Mock<ICurrentTenant>();
        t.SetupGet(x => x.TenantId).Returns(TenantId);
        var u = new Mock<ICurrentUser>();
        u.SetupGet(x => x.UserId).Returns(UserId);
        var handler = new CancelPurchaseReturnDraftHandler(repo.Object, t.Object, u.Object);

        var result = await handler.Handle(
            new CancelPurchaseReturnDraftCommand(purchaseReturn.Id, Guid.NewGuid(), "Ya no aplica"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelDraft_valido_transiciona_a_Cancelled_y_guarda_una_sola_vez()
    {
        var invoice = ConfirmedInvoice();
        var line = invoice.Lines.Single();
        var purchaseReturn = PurchaseReturn.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            invoice.Id,
            SupplierId,
            "Motivo",
            new[]
            {
                new PurchaseReturn.DraftLineInput(line.Id, line.ItemId!.Value, 1, WarehouseId),
            },
            UserId,
            Guid.NewGuid(),
            "hash"
        );
        var repo = new Mock<IPurchaseReturnRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, purchaseReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseReturn);
        var t = new Mock<ICurrentTenant>();
        t.SetupGet(x => x.TenantId).Returns(TenantId);
        var u = new Mock<ICurrentUser>();
        u.SetupGet(x => x.UserId).Returns(UserId);
        var handler = new CancelPurchaseReturnDraftHandler(repo.Object, t.Object, u.Object);

        var result = await handler.Handle(
            new CancelPurchaseReturnDraftCommand(purchaseReturn.Id, Guid.NewGuid(), "Ya no aplica"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        purchaseReturn
            .Status.Should()
            .Be(Domain.Modules.Purchases.Enums.PurchaseReturnStatus.Cancelled);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
