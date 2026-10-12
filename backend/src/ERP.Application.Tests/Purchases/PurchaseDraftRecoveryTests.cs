using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using Moq;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// PURCHASE-DRAFT-RECOVERY — un borrador de compra se guarda incompleto, se cierra y se reabre sin
/// perder resolución de productos, evidencia XML, cuotas manuales ni intención de retención.
/// </summary>
public sealed class PurchaseDraftRecoveryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();
    private static readonly DateOnly IssueDate = new(2026, 9, 1);

    // ── Fixtures ────────────────────────────────────────────────────────

    private static Mock<IBusinessPartnerRepository> SupplierRepo()
    {
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo
            .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                BusinessPartner.Create(TenantId, "04", "1791352688001", 2, "Proveedor", UserId)
            );
        return bpRepo;
    }

    private static Mock<IBusinessPartnerRoleRepository> SupplierRoleRepo()
    {
        var roleRepo = new Mock<IBusinessPartnerRoleRepository>();
        roleRepo
            .Setup(r =>
                r.GetByTypeAsync(
                    SupplierId,
                    Domain.MasterData.Enums.RoleType.Supplier,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                BusinessPartnerRole.Create(
                    TenantId,
                    SupplierId,
                    Domain.MasterData.Enums.RoleType.Supplier,
                    UserId,
                    supplierConfig: SupplierRoleConfig.Create()
                )
            );
        return roleRepo;
    }

    private static Mock<IPaymentTermDefaultResolver> PaymentTermResolver(PaymentTerm? term = null)
    {
        var resolver = new Mock<IPaymentTermDefaultResolver>();
        resolver
            .Setup(r =>
                r.ResolveForPurchaseAsync(
                    SupplierId,
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<PaymentTerm>.Success(
                    term ?? PaymentTerm.Create(TenantId, "CONTADO", "Contado", 1, 0, UserId)
                )
            );
        return resolver;
    }

    private static Mock<PurchaseTaxResolver> TaxResolver()
    {
        var tax = new Mock<PurchaseTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));
        tax.Setup(t => t.GetVatCatalogEntryAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SriTaxCatalogEntry("10", "IVA 15%", 15m, null, SriTaxCalculationType.Percentage)
            );
        return tax;
    }

    private static CreatePurchaseDraftHandler CreateHandler(
        Mock<IPurchaseInvoiceRepository> repo,
        IPurchaseReceptionDocumentRepository? receptionRepo = null
    ) =>
        new(
            repo.Object,
            SupplierRepo().Object,
            SupplierRoleRepo().Object,
            PaymentTermResolver().Object,
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            TaxResolver().Object,
            receptionRepo ?? Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<IExpenseDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>(),
            PrecisionPolicyTestDouble.Mock()
        );

    private static UpdatePurchaseDraftHandler UpdateHandler(
        Mock<IPurchaseInvoiceRepository> repo,
        IPurchaseReceptionDocumentRepository? receptionRepo = null,
        PaymentTerm? term = null
    ) =>
        new(
            repo.Object,
            SupplierRepo().Object,
            PaymentTermResolver(term).Object,
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            TaxResolver().Object,
            receptionRepo ?? Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>(),
            PrecisionPolicyTestDouble.Mock(),
            UnitOfWorkTestDouble.PassThrough().Object
        );

    private static Mock<IPurchaseInvoiceRepository> RepoFor(PurchaseInvoice inv)
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        return repo;
    }

    /// <summary>Documento de recepción verificado con una línea XML 2 × 10.00, IVA 15% = 3.00.</summary>
    private static (PurchaseReceptionDocument Document, PurchaseReceptionLine Line) Reception(
        Guid? matchedItemId = null
    )
    {
        var document = PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1791352688001",
            "Proveedor S.A.",
            SupplierId,
            new string('7', 49),
            "001-001-000000777",
            IssueDate,
            null,
            20m,
            3m,
            23m,
            UserId
        );
        var line = PurchaseReceptionLine.Create(
            document.Id,
            TenantId,
            "Galleta XML",
            2m,
            10m,
            vatCode: "10",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 3m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 20m,
            totalLine: 20m,
            supplierCode: "PROV-001",
            supplierAuxCode: "7861234567890",
            itemId: matchedItemId,
            matchStatus: matchedItemId is null
                ? ItemMatchStatus.Pending
                : ItemMatchStatus.ManuallyMatched,
            taxes: [("2", "10", 15m, 20m, 3m)],
            additionalFields: [("Lote", "L-01", 1)]
        );
        document.AttachSriAuthorization(
            "AUTH-1",
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [line],
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
        return (document, line);
    }

    private static Mock<IPurchaseReceptionDocumentRepository> ReceptionRepo(
        PurchaseReceptionDocument document,
        Guid lineId
    )
    {
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        repo.Setup(r => r.GetByLineIdAsync(TenantId, lineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        repo.Setup(r =>
                r.GetByLineIdWithEvidenceAsync(TenantId, lineId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(document);
        return repo;
    }

    /// <summary>Borrador con una línea manual 1 × 100.00 + IVA 15% = 115.00.</summary>
    private static PurchaseInvoice DraftWithManualLine(
        IReadOnlyList<(int, DateOnly, decimal, string?)>? schedule = null
    )
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor",
            "1791352688001",
            "01",
            "001-001-000000001",
            IssueDate,
            UserId,
            PtId,
            "Contado",
            1,
            0
        );
        var line = PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Manual", 1m, 100m, "10", "UNIT");
        line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
        inv.ReplaceLines([line], UserId);
        if (schedule is not null)
            inv.ReplacePaymentSchedule(schedule);
        return inv;
    }

    private static UpdatePurchaseDraftCommand UpdateCommand(
        PurchaseInvoice inv,
        List<PurchaseLineInput> lines,
        Guid? paymentTermId = null,
        List<ConfirmScheduleInput>? schedule = null,
        PurchaseRetentionIntentDraftDto? retentionIntent = null
    ) =>
        new(
            inv.Id,
            SupplierId,
            "01",
            inv.InvoiceNumber,
            IssueDate,
            lines,
            PaymentTermId: paymentTermId,
            Schedule: schedule,
            RetentionIntent: retentionIntent
        );

    // ── B1: guardar con pendientes ──────────────────────────────────────

    [Fact]
    public async Task Guardar_borrador_con_linea_XML_pendiente_de_producto_se_persiste()
    {
        var (document, receptionLine) = Reception();
        var repo = new Mock<IPurchaseInvoiceRepository>();
        PurchaseInvoice? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseInvoice, CancellationToken>((invoice, _) => saved = invoice)
            .Returns(Task.CompletedTask);

        var result = await CreateHandler(repo, ReceptionRepo(document, receptionLine.Id).Object)
            .Handle(
                new CreatePurchaseDraftCommand(
                    SupplierId,
                    "01",
                    "001-001-000000777",
                    IssueDate,
                    [
                        new PurchaseLineInput(
                            null,
                            "Galleta XML",
                            2m,
                            10m,
                            "10",
                            PurchaseReceptionLineId: receptionLine.Id
                        ),
                    ]
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        saved!.Lines.Single().ItemId.Should().BeNull();
        saved.Lines.Single().PurchaseReceptionLineId.Should().Be(receptionLine.Id);
    }

    // ── F1 + D1: reabrir con evidencia XML y match aprendido ────────────

    [Fact]
    public async Task Reabrir_borrador_expone_XmlSource_original_y_match_aprendido_sin_tocar_la_compra()
    {
        var learnedItemId = Guid.NewGuid();
        var packagingLevelId = Guid.NewGuid();
        var (document, receptionLine) = Reception(matchedItemId: learnedItemId);
        var inv = DraftWithManualLine();
        var pendingLine = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Galleta XML",
            2m,
            10m,
            "10",
            "UNIT",
            purchaseReceptionLineId: receptionLine.Id
        );
        inv.ReplaceLines([pendingLine], UserId);

        var itemRepo = new Mock<IItemRepository>();
        itemRepo
            .Setup(r =>
                r.GetSupplierCodeMatchAsync(
                    SupplierId,
                    "PROV-001",
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ItemSupplierCodeMatch(learnedItemId, packagingLevelId, "CAJA", 12m, "UNIT"));

        var result = await new GetPurchaseByIdHandler(
            RepoFor(inv).Object,
            ReceptionRepo(document, receptionLine.Id).Object,
            itemRepo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId)
        ).Handle(new GetPurchaseByIdQuery(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines.Single();
        line.ItemId.Should().BeNull("la resolución de la compra nunca se sobrescribe al leer");
        var xml = line.XmlSource!;
        xml.Quantity.Should().Be(2m);
        xml.UnitPrice.Should().Be(10m);
        xml.DiscountPct.Should().Be(0m);
        xml.SupplierCode.Should().Be("PROV-001");
        xml.SupplierAuxCode.Should().Be("7861234567890");
        xml.Taxes.Should().ContainSingle(t => t.TaxCode == "2" && t.TaxAmount == 3m);
        xml.AdditionalFields.Should().ContainSingle(f => f.Name == "Lote" && f.Value == "L-01");
        xml.ItemId.Should().Be(learnedItemId);
        xml.ItemMatchStatus.Should().Be("MANUALLY_MATCHED");
        xml.PackagingLevelId.Should().Be(packagingLevelId);
        xml.ConversionFactor.Should().Be(12m);
    }

    // ── B2: editar → guardar → reabrir → volver al XML original ─────────

    [Fact]
    public async Task Volver_exactamente_al_XML_original_recupera_los_impuestos_del_XML()
    {
        var (document, receptionLine) = Reception();
        var inv = DraftWithManualLine();
        var repo = RepoFor(inv);
        var receptionRepo = ReceptionRepo(document, receptionLine.Id).Object;

        PurchaseLineInput XmlLine(decimal quantity) =>
            new(null, "Galleta XML", quantity, 10m, "10", PurchaseReceptionLineId: receptionLine.Id);

        var edited = await UpdateHandler(repo, receptionRepo)
            .Handle(UpdateCommand(inv, [XmlLine(3m)]), CancellationToken.None);
        edited.IsSuccess.Should().BeTrue(edited.Error);
        edited.Value!.Lines.Single().Taxes.Single(t => t.TaxCode == "2").Source
            .Should().Be("Calculated");
        edited.Value.Lines.Single().VatAmount.Should().Be(4.50m);

        var restored = await UpdateHandler(repo, receptionRepo)
            .Handle(UpdateCommand(inv, [XmlLine(2m)]), CancellationToken.None);
        restored.IsSuccess.Should().BeTrue(restored.Error);
        var vat = restored.Value!.Lines.Single().Taxes.Single(t => t.TaxCode == "2");
        vat.Source.Should().Be("Xml");
        vat.TaxAmount.Should().Be(3m);
        restored.Value.Lines.Single().VatAmount.Should().Be(3m);
    }

    // ── F2: cuotas manuales ─────────────────────────────────────────────

    [Fact]
    public async Task Cuotas_manuales_enviadas_al_crear_quedan_persistidas_en_el_borrador()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, IssueDate.AddDays(15), 40m, "Anticipo"),
            new(2, IssueDate.AddDays(45), 75m),
        };

        var result = await CreateHandler(repo)
            .Handle(
                new CreatePurchaseDraftCommand(
                    SupplierId,
                    "01",
                    "001-001-000000001",
                    IssueDate,
                    [new PurchaseLineInput(null, "Manual", 1m, 100m, "10")],
                    Schedule: schedule
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PaymentSchedules.Select(s => (s.InstallmentNumber, s.Amount, s.Notes))
            .Should().Equal((1, 40m, "Anticipo"), (2, 75m, (string?)null));
    }

    [Fact]
    public async Task Cuotas_guardadas_que_ya_no_cuadran_con_el_nuevo_total_se_conservan_tal_cual()
    {
        var inv = DraftWithManualLine([(1, IssueDate.AddDays(30), 115m, "Única")]);
        var repo = RepoFor(inv);

        var result = await UpdateHandler(repo)
            .Handle(
                UpdateCommand(inv, [new PurchaseLineInput(null, "Manual", 2m, 100m, "10")]),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.GrandTotal.Should().Be(230m);
        result.Value.PaymentSchedules.Should().ContainSingle(s => s.Amount == 115m && s.Notes == "Única");
        inv.PaymentScheduleMatchesTotal().Should().BeFalse("Confirmar debe exigir corregirlas");
        repo.Verify(
            r => r.ClearScheduleTrackingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Cuotas_manuales_enviadas_que_no_cuadran_se_guardan_tal_cual_sin_regenerarlas()
    {
        var inv = DraftWithManualLine([(1, IssueDate.AddDays(30), 115m, null)]);
        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, IssueDate.AddDays(15), 50m, "Anticipo"),
            new(2, IssueDate.AddDays(45), 50m),
        };

        var result = await UpdateHandler(RepoFor(inv))
            .Handle(
                UpdateCommand(
                    inv,
                    [new PurchaseLineInput(null, "Manual", 1m, 100m, "10")],
                    schedule: schedule
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PaymentSchedules.Select(s => (s.InstallmentNumber, s.Amount))
            .Should().Equal((1, 50m), (2, 50m));
    }

    [Fact]
    public async Task Cuotas_estructuralmente_invalidas_se_rechazan_al_guardar()
    {
        var inv = DraftWithManualLine([(1, IssueDate.AddDays(30), 115m, null)]);

        var result = await UpdateHandler(RepoFor(inv))
            .Handle(
                UpdateCommand(
                    inv,
                    [new PurchaseLineInput(null, "Manual", 1m, 100m, "10")],
                    schedule: [new(1, IssueDate.AddDays(10), 0m), new(1, IssueDate.AddDays(20), 115m)]
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("monto debe ser mayor a cero");
    }

    [Fact]
    public async Task Cuotas_guardadas_validas_se_conservan_si_no_llega_cronograma_nuevo()
    {
        var inv = DraftWithManualLine([(1, IssueDate.AddDays(30), 115m, "Única")]);
        var repo = RepoFor(inv);

        var result = await UpdateHandler(repo)
            .Handle(
                UpdateCommand(inv, [new PurchaseLineInput(null, "Manual editada", 1m, 100m, "10")]),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PaymentSchedules.Should().ContainSingle(s => s.Amount == 115m && s.Notes == "Única");
        repo.Verify(
            r => r.ClearScheduleTrackingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Cambiar_condicion_de_pago_conserva_las_cuotas_guardadas_sin_regenerarlas()
    {
        var inv = DraftWithManualLine([(1, IssueDate.AddDays(30), 115m, null)]);
        var newTerm = PaymentTerm.Create(TenantId, "CRED30", "Crédito 30", 2, 30, UserId);

        var result = await UpdateHandler(RepoFor(inv), term: newTerm)
            .Handle(
                UpdateCommand(
                    inv,
                    [new PurchaseLineInput(null, "Manual", 1m, 100m, "10")],
                    paymentTermId: newTerm.Id
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.PaymentTermId.Should().Be(newTerm.Id);
        result.Value!.PaymentSchedules.Should().ContainSingle(s => s.Amount == 115m);
    }

    [Fact]
    public async Task Enviar_cronograma_vacio_descarta_las_cuotas_manuales()
    {
        var inv = DraftWithManualLine([(1, IssueDate.AddDays(30), 115m, null)]);
        var repo = RepoFor(inv);

        var result = await UpdateHandler(repo)
            .Handle(
                UpdateCommand(inv, [new PurchaseLineInput(null, "Manual", 2m, 100m, "10")], schedule: []),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PaymentSchedules.Should().BeEmpty();
        repo.Verify(
            r => r.ClearScheduleTrackingAsync(inv.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // ── F3: intención de retención ──────────────────────────────────────

    [Fact]
    public async Task Intencion_de_retencion_se_persiste_y_sobrevive_a_un_update_que_no_la_envia()
    {
        var emissionPointId = Guid.NewGuid();
        var inv = DraftWithManualLine();
        var repo = RepoFor(inv);
        var intent = new PurchaseRetentionIntentDraftDto(true, emissionPointId, IssueDate.AddDays(2));

        var saved = await UpdateHandler(repo)
            .Handle(
                UpdateCommand(inv, [new PurchaseLineInput(null, "Manual", 1m, 100m, "10")], retentionIntent: intent),
                CancellationToken.None
            );
        saved.IsSuccess.Should().BeTrue(saved.Error);
        saved.Value!.RetentionIntent.Should().Be(intent);

        var untouched = await UpdateHandler(repo)
            .Handle(
                UpdateCommand(inv, [new PurchaseLineInput(null, "Manual", 1m, 100m, "10")]),
                CancellationToken.None
            );
        untouched.Value!.RetentionIntent.Should().Be(intent);
    }
}
