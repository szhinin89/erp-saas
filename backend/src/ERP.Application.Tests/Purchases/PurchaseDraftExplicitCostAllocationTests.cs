using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using Moq;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// Create/UpdatePurchaseDraftHandler: normalización de líneas y, desde
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01, conservación del Id de línea y de sus distribuciones de
/// costos al guardar (flete/otros costos ya no viajan por línea).
/// </summary>
public sealed class PurchaseDraftExplicitCostAllocationTests
{
    [Theory]
    // ZH-TEMPORAL-CONTRACT-02: mismo instante con offset explícito (16:50 Ecuador = 21:50Z).
    [InlineData("2026-09-03T16:50:00-05:00")]
    [InlineData("2026-09-03T21:50:00Z")]
    [InlineData(null)]
    public async Task CreateDraft_normalizes_reception_date_before_save_and_allows_manual_purchase(
        string? date
    )
    {
        var authorizationDate = date is null
            ? (DateTime?)null
            : System.Text.Json.JsonSerializer.Deserialize<DateTime>($"\"{date}\"");
        var repo = new Mock<IPurchaseInvoiceRepository>();
        PurchaseInvoice? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseInvoice, CancellationToken>((invoice, _) => saved = invoice)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                saved.Should().NotBeNull();
                if (authorizationDate.HasValue)
                    saved!.AuthorizationDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
            })
            .Returns(Task.CompletedTask);
        var command = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            new DateOnly(2026, 9, 3),
            [new PurchaseLineInput(null, "Producto", 1m, 100m, "10")],
            AuthorizationDate: authorizationDate
        );

        var result = await BuildCreateHandler(repo).Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        saved!
            .AuthorizationDate.Should()
            .Be(
                authorizationDate is null
                    ? null
                    : new DateTime(2026, 9, 3, 21, 50, 0, DateTimeKind.Utc)
            );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();

    private static Mock<IBusinessPartnerRepository> BuildActiveSupplierRepo()
    {
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo
            .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                BusinessPartner.Create(TenantId, "04", "1791352688001", 2, "Proveedor", UserId)
            );
        return bpRepo;
    }

    private static Mock<IBusinessPartnerRoleRepository> BuildSupplierRoleRepo()
    {
        var roleRepo = new Mock<IBusinessPartnerRoleRepository>();
        var config = SupplierRoleConfig.Create();
        var role = BusinessPartnerRole.Create(
            TenantId,
            SupplierId,
            Domain.MasterData.Enums.RoleType.Supplier,
            UserId,
            supplierConfig: config
        );
        roleRepo
            .Setup(r =>
                r.GetByTypeAsync(
                    SupplierId,
                    Domain.MasterData.Enums.RoleType.Supplier,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(role);
        return roleRepo;
    }

    private static Mock<IPaymentTermDefaultResolver> BuildPaymentTermResolver()
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
                    PaymentTerm.Create(TenantId, "CONTADO", "Contado", 1, 0, UserId)
                )
            );
        return resolver;
    }

    private static Mock<PurchaseTaxResolver> BuildTaxResolver()
    {
        var tax = new Mock<PurchaseTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));
        tax.Setup(t => t.GetVatCatalogEntryAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SriTaxCatalogEntry("10", "IVA 15%", 15m, null, SriTaxCalculationType.Percentage)
            );
        tax.Setup(t => t.GetIrbpnrCatalogEntryAsync("5001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SriTaxCatalogEntry("5001", "IRBPNR", null, 0.02m, SriTaxCalculationType.Specific)
            );
        return tax;
    }

    private static CreatePurchaseDraftHandler BuildCreateHandler(
        Mock<IPurchaseInvoiceRepository> repo,
        ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider? precision = null,
        IPurchaseReceptionDocumentRepository? receptionRepo = null
    ) =>
        new(
            repo.Object,
            BuildActiveSupplierRepo().Object,
            BuildSupplierRoleRepo().Object,
            BuildPaymentTermResolver().Object,
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            BuildTaxResolver().Object,
            receptionRepo ?? Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<IExpenseDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>(),
            precision ?? PrecisionPolicyTestDouble.Mock()
        );

    private static UpdatePurchaseDraftHandler BuildUpdateHandler(
        Mock<IPurchaseInvoiceRepository> repo
    ) =>
        new(
            repo.Object,
            BuildActiveSupplierRepo().Object,
            BuildPaymentTermResolver().Object,
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            BuildTaxResolver().Object,
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>(),
            PrecisionPolicyTestDouble.Mock(),
            UnitOfWorkTestDouble.PassThrough().Object
        );

    private static PurchaseInvoice CreateExistingDraft()
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
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            0
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto viejo",
            1m,
            50m,
            "10",
            "UNIT",
            itemId: Guid.NewGuid()
        );
        inv.ReplaceLines([line], UserId);
        return inv;
    }

    // ── PURCHASE-COST-DISTRIBUTION-CANONICAL-01: el guardado conserva Ids y distribuciones ──

    private static (PurchaseInvoice Invoice, Mock<IPurchaseInvoiceRepository> Repo) ExistingDraftWithFreight()
    {
        var inv = CreateExistingDraft();
        inv.ReplaceCostDistribution(
            new PurchaseCostDistributionChange(Guid.NewGuid(), 0, PurchaseCostType.Freight, 10m,
                [inv.Lines[0].Id], null),
            new Dictionary<Guid, ERP.Domain.Modules.Items.Entities.ItemNature>
            {
                [inv.Lines[0].ItemId!.Value] = ERP.Domain.Modules.Items.Entities.ItemNature.Product,
            }, UserId);
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        return (inv, repo);
    }

    private static UpdatePurchaseDraftCommand UpdateWith(PurchaseInvoice inv, params PurchaseLineInput[] lines) =>
        new(inv.Id, SupplierId, "01", "001-001-000000001", DateOnly.FromDateTime(DateTime.UtcNow), [.. lines]);

    [Fact]
    public async Task UpdateDraft_conserva_el_Id_de_linea_y_reprorratea_su_distribucion()
    {
        var (inv, repo) = ExistingDraftWithFreight();
        var lineId = inv.Lines[0].Id;

        var result = await BuildUpdateHandler(repo).Handle(UpdateWith(inv,
            new PurchaseLineInput(null, "Producto editado", 2m, 50m, "10", LineId: lineId),
            new PurchaseLineInput(null, "Producto nuevo", 1m, 30m, "10")), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].Id.Should().Be(lineId);
        result.Value.Lines[0].FreightAllocated.Should().Be(10m);
        result.Value.Lines[1].FreightAllocated.Should().Be(0m);
        result.Value.CostDistributions.Should().ContainSingle()
            .Which.Allocations.Should().ContainSingle(a => a.LineId == lineId);
        repo.Verify(r => r.ReattachCostDistributionsAsAdded(inv), Times.Once);
    }

    [Fact]
    public async Task UpdateDraft_bloquea_eliminar_una_linea_con_distribucion()
    {
        var (inv, repo) = ExistingDraftWithFreight();

        var result = await BuildUpdateHandler(repo).Handle(UpdateWith(inv,
            new PurchaseLineInput(null, "Otra línea", 1m, 50m, "10")), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("distribución de costos");
        repo.Verify(r => r.ReattachCostDistributionsAsAdded(It.IsAny<PurchaseInvoice>()), Times.Never);
    }

    [Theory]
    [InlineData("ajena")]
    [InlineData("duplicada")]
    public async Task UpdateDraft_rechaza_LineId_ajeno_o_duplicado(string scenario)
    {
        var (inv, repo) = ExistingDraftWithFreight();
        var lineId = scenario == "ajena" ? Guid.NewGuid() : inv.Lines[0].Id;

        var result = await BuildUpdateHandler(repo).Handle(UpdateWith(inv,
            new PurchaseLineInput(null, "A", 1m, 50m, "10", LineId: lineId),
            new PurchaseLineInput(null, "B", 1m, 50m, "10", LineId: scenario == "duplicada" ? lineId : null)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repo.Verify(r => r.RemoveLinesByInvoiceAsync(It.IsAny<Guid>(),
            It.IsAny<IEnumerable<PurchaseInvoiceDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ERP-PRECISION-OPERATIONAL-05B1: purchaseUnitPriceDecimals gobierna el precio operativo ──

    private static ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider PurchasePolicy(
        int decimals
    )
    {
        var mock = new Mock<ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider>();
        mock.Setup(p => p.GetEffectiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PrecisionPolicyTestDouble.DefaultDto() with
                {
                    PurchaseUnitPriceDecimals = decimals,
                }
            );
        return mock.Object;
    }

    private static async Task<PurchaseInvoice> CreateWithLineAsync(
        int decimals,
        PurchaseLineInput line,
        IPurchaseReceptionDocumentRepository? receptionRepo = null
    )
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        PurchaseInvoice? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseInvoice, CancellationToken>((invoice, _) => saved = invoice)
            .Returns(Task.CompletedTask);
        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [line]
        );

        var result = await BuildCreateHandler(repo, PurchasePolicy(decimals), receptionRepo)
            .Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        return saved!;
    }

    [Theory]
    [InlineData(4, 1.2346)]
    [InlineData(2, 1.23)]
    [InlineData(10, 1.23456789)]
    public async Task Linea_manual_normaliza_el_precio_unitario_a_purchaseUnitPriceDecimals(
        int decimals,
        double expected
    )
    {
        var saved = await CreateWithLineAsync(
            decimals,
            new PurchaseLineInput(null, "Manual", 1m, 1.23456789m, "10")
        );

        saved.Lines.Single().UnitPrice.Should().Be((decimal)expected);
    }

    // Documento de recepción (snapshot fuente inmutable del XML) con una línea de precio original conocido.
    private static (IPurchaseReceptionDocumentRepository Repo, Guid LineId) ReceptionWithLine(
        decimal xmlUnitPrice,
        decimal quantity = 1m,
        decimal discountPct = 0m,
        decimal discount = 0m
    )
    {
        var document =
            ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionDocument.Create(
                TenantId,
                CompanyId,
                BranchId,
                ERP.Domain
                    .Modules
                    .Purchases
                    .PurchaseReception
                    .Enums
                    .PurchaseReceptionSourceDocType
                    .Invoice,
                "1791352688001",
                "Proveedor S.A.",
                Guid.NewGuid(),
                new string('1', 49),
                "001-001-000000001",
                new DateOnly(2026, 7, 1),
                null,
                10m,
                1.5m,
                11.5m,
                UserId
            );
        var line =
            ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionLine.Create(
                document.Id,
                TenantId,
                "Línea XML",
                quantity,
                xmlUnitPrice,
                vatCode: "10",
                taxCode: "2",
                vatPercentage: 15m,
                taxValue: 0.15m,
                discountPct: discountPct,
                discount: discount,
                lineSubtotal: quantity * xmlUnitPrice - discount,
                totalLine: quantity * xmlUnitPrice - discount
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
            processing: new ERP.Domain.Modules.Purchases.PurchaseReception.Models.PurchaseReceptionProcessingOutcome(
                ERP.Domain
                    .Modules
                    .Purchases
                    .PurchaseReception
                    .Enums
                    .PurchaseReceptionProcessingStatus
                    .Processed,
                1,
                1,
                null
            )
        );
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        repo.Setup(r => r.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        return (repo.Object, line.Id);
    }

    [Fact]
    public async Task Linea_XML_sin_editar_conserva_exactamente_el_precio_original()
    {
        var (repo, lineId) = ReceptionWithLine(1.234567m);

        var saved = await CreateWithLineAsync(
            2,
            new PurchaseLineInput(
                null,
                "XML",
                1m,
                1.234567m,
                "10",
                PurchaseReceptionLineId: lineId
            ),
            repo
        );

        saved.Lines.Single().UnitPrice.Should().Be(1.234567m);
    }

    [Fact]
    public async Task Linea_XML_editada_aplica_purchaseUnitPriceDecimals()
    {
        var (repo, lineId) = ReceptionWithLine(1.234567m);

        var saved = await CreateWithLineAsync(
            4,
            new PurchaseLineInput(
                null,
                "XML editada",
                1m,
                2.3456789m,
                "10",
                PurchaseReceptionLineId: lineId
            ),
            repo
        );

        saved.Lines.Single().UnitPrice.Should().Be(2.3457m);
    }

    [Fact]
    public async Task Linea_XML_editada_a_un_valor_que_ya_cumple_la_escala_no_se_altera()
    {
        var (repo, lineId) = ReceptionWithLine(1.234567m);

        // Editado a 1.2346 (= redondeo efectivo del original a 4): queda exactamente en 1.2346.
        var saved = await CreateWithLineAsync(
            4,
            new PurchaseLineInput(
                null,
                "XML editada",
                1m,
                1.2346m,
                "10",
                PurchaseReceptionLineId: lineId
            ),
            repo
        );

        saved.Lines.Single().UnitPrice.Should().Be(1.2346m);
    }

    [Fact]
    public async Task Linea_XML_cuyo_original_ya_no_se_resuelve_se_trata_como_editada_y_se_normaliza()
    {
        var saved = await CreateWithLineAsync(
            2,
            new PurchaseLineInput(
                null,
                "XML",
                1m,
                1.234567m,
                "10",
                PurchaseReceptionLineId: Guid.NewGuid()
            )
        );

        saved.Lines.Single().UnitPrice.Should().Be(1.23m);
    }

    // ── ERP-PRECISION-OPERATIONAL-05B3: Update de borrador ─────────────────────────────────

    private static async Task<PurchaseInvoice> UpdateWithLineAsync(
        int decimals,
        PurchaseLineInput line,
        IPurchaseReceptionDocumentRepository? receptionRepo = null
    )
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor",
            "1791352688001",
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            0
        );
        invoice.ReplaceLines(
            [PurchaseInvoiceDetail.Create(invoice.Id, TenantId, "Original", 1m, 1m, "10", "UNIT")],
            UserId
        );

        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var handler = new UpdatePurchaseDraftHandler(
            repo.Object,
            BuildActiveSupplierRepo().Object,
            Mock.Of<IPaymentTermDefaultResolver>(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            BuildTaxResolver().Object,
            receptionRepo ?? Mock.Of<IPurchaseReceptionDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            Mock.Of<IDatabaseExceptionTranslator>(),
            PurchasePolicy(decimals),
            UnitOfWorkTestDouble.PassThrough().Object
        );

        var result = await handler.Handle(
            new UpdatePurchaseDraftCommand(
                invoice.Id,
                SupplierId,
                "01",
                "001-001-000000001",
                DateOnly.FromDateTime(DateTime.UtcNow),
                [line]
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        return invoice;
    }

    [Fact]
    public async Task Update_linea_manual_editada_normaliza_a_purchaseUnitPriceDecimals()
    {
        var invoice = await UpdateWithLineAsync(
            4,
            new PurchaseLineInput(null, "Manual", 1m, 2.345678m, "10")
        );

        invoice.Lines.Single().UnitPrice.Should().Be(2.3457m);
    }

    [Fact]
    public async Task Update_linea_XML_editada_normaliza_a_purchaseUnitPriceDecimals()
    {
        var (repo, lineId) = ReceptionWithLine(1.234567m);

        var invoice = await UpdateWithLineAsync(
            4,
            new PurchaseLineInput(
                null,
                "XML editada",
                1m,
                2.3456789m,
                "10",
                PurchaseReceptionLineId: lineId
            ),
            repo
        );

        invoice.Lines.Single().UnitPrice.Should().Be(2.3457m);
    }

    // ── ZH-PURCHASES-XML-DISCOUNT-FIX-01: contrato del descuento XML en el borrador ─────────
    // Caso real QA: 1000 × 100 con <descuento>1234.56</descuento>. El parser deriva DiscountPct=1.23
    // (redondeado a 2); recalcular desde ese % daría 1230.00. Sin editar, el monto XML es la fuente.
    // IVA "10" → 15% (BuildTaxResolver). Base = 98765.44 = precioTotalSinImpuesto del XML.

    [Fact]
    public async Task Create_linea_XML_sin_editar_conserva_descuento_exacto_y_calcula_IVA_sobre_la_base_XML()
    {
        var (repo, lineId) = ReceptionWithLine(
            100m,
            quantity: 1000m,
            discountPct: 1.23m,
            discount: 1234.56m
        );

        var saved = await CreateWithLineAsync(
            4,
            new PurchaseLineInput(
                null,
                "XML",
                1000m,
                100m,
                "10",
                DiscountPct: 1.23m,
                PurchaseReceptionLineId: lineId
            ),
            repo
        );

        var line = saved.Lines.Single();
        line.DiscountAmount.Should().Be(1234.56m);
        line.TaxableBase.Should().Be(98765.44m);
        line.VatAmount.Should().Be(14814.82m);
        line.TaxInclusiveTotal.Should().Be(113580.26m);
    }

    [Fact]
    public async Task Update_linea_XML_sin_editar_conserva_descuento_exacto_y_calcula_IVA_sobre_la_base_XML()
    {
        var (repo, lineId) = ReceptionWithLine(
            100m,
            quantity: 1000m,
            discountPct: 1.23m,
            discount: 1234.56m
        );

        var invoice = await UpdateWithLineAsync(
            4,
            new PurchaseLineInput(
                null,
                "XML",
                1000m,
                100m,
                "10",
                DiscountPct: 1.23m,
                PurchaseReceptionLineId: lineId
            ),
            repo
        );

        var line = invoice.Lines.Single();
        line.DiscountAmount.Should().Be(1234.56m);
        line.TaxableBase.Should().Be(98765.44m);
        line.VatAmount.Should().Be(14814.82m);
        line.TaxInclusiveTotal.Should().Be(113580.26m);
    }

    [Fact]
    public async Task Linea_XML_con_cantidad_editada_recalcula_el_descuento_desde_el_porcentaje()
    {
        var (repo, lineId) = ReceptionWithLine(
            100m,
            quantity: 1000m,
            discountPct: 1.23m,
            discount: 1234.56m
        );

        var saved = await CreateWithLineAsync(
            4,
            new PurchaseLineInput(
                null,
                "XML editada",
                500m,
                100m,
                "10",
                DiscountPct: 1.23m,
                PurchaseReceptionLineId: lineId
            ),
            repo
        );

        var line = saved.Lines.Single();
        line.DiscountAmount.Should().Be(615.00m);
        line.TaxableBase.Should().Be(49385.00m);
        line.VatAmount.Should().Be(7407.75m);
        line.TaxInclusiveTotal.Should().Be(56792.75m);
    }

    [Fact]
    public async Task Linea_XML_con_descuento_cero_no_aplica_descuento()
    {
        var (repo, lineId) = ReceptionWithLine(100m, quantity: 1000m);

        var saved = await CreateWithLineAsync(
            4,
            new PurchaseLineInput(null, "XML", 1000m, 100m, "10", PurchaseReceptionLineId: lineId),
            repo
        );

        var line = saved.Lines.Single();
        line.DiscountAmount.Should().Be(0m);
        line.TaxableBase.Should().Be(100000m);
        line.VatAmount.Should().Be(15000m);
        line.TaxInclusiveTotal.Should().Be(115000m);
    }

    [Fact]
    public async Task Linea_manual_sin_descuento_calcula_IVA_sobre_el_subtotal()
    {
        var saved = await CreateWithLineAsync(
            4,
            new PurchaseLineInput(null, "Manual", 2m, 50m, "10")
        );

        var line = saved.Lines.Single();
        line.DiscountAmount.Should().Be(0m);
        line.TaxableBase.Should().Be(100m);
        line.VatAmount.Should().Be(15m);
        line.TaxInclusiveTotal.Should().Be(115m);
    }

    // SSOT purchases.md §2 (V1): editar cantidad/precio conserva los importes fiscales.
    // Este fixture prueba el guardado del borrador, no acredita conciliación al confirmar:
    // su IVA declarado difiere de base × tarifa y su XML no contiene cabecera fiscal.
    // XML: 3 × 1.11 = 3.33, IVA declarado 0.49 (≠ 0.50 por tarifa) + IRBPNR 0.06 (3 × 0.02).
    private static (IPurchaseReceptionDocumentRepository Repo, Guid LineId) ReceptionWithTaxedLine()
    {
        var document =
            ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionDocument.Create(
                TenantId,
                CompanyId,
                BranchId,
                ERP.Domain.Modules.Purchases.PurchaseReception.Enums.PurchaseReceptionSourceDocType.Invoice,
                "1791352688001",
                "Proveedor S.A.",
                Guid.NewGuid(),
                new string('2', 49),
                "001-001-000000002",
                new DateOnly(2026, 7, 1),
                null,
                3.33m,
                0.49m,
                3.88m,
                UserId
            );
        var line =
            ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionLine.Create(
                document.Id,
                TenantId,
                "Agua 500ml",
                3m,
                1.11m,
                vatCode: "10",
                taxCode: "2",
                vatPercentage: 15m,
                taxValue: 0.49m,
                discountPct: 0m,
                discount: 0m,
                lineSubtotal: 3.33m,
                totalLine: 3.88m,
                taxes:
                [
                    ("2", "10", 15m, 3.33m, 0.49m),
                    ("5", "5001", 0.02m, 3m, 0.06m),
                ]
            );
        document.AttachSriAuthorization(
            "AUTH-2",
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: new ERP.Domain.Modules.Purchases.PurchaseReception.Models.PurchaseReceptionProcessingOutcome(
                ERP.Domain.Modules.Purchases.PurchaseReception.Enums.PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        repo.Setup(r => r.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        return (repo.Object, line.Id);
    }

    [Fact]
    public async Task Linea_XML_sin_editar_conserva_IVA_e_IRBPNR_del_comprobante()
    {
        var (repo, lineId) = ReceptionWithTaxedLine();

        var saved = await CreateWithLineAsync(
            2,
            new PurchaseLineInput(null, "XML", 3m, 1.11m, "10", PurchaseReceptionLineId: lineId),
            repo
        );

        var line = saved.Lines.Single();
        line.VatAmount.Should().Be(0.49m);
        line.XmlVatAmount.Should().Be(0.49m);
        line.IrbpnrAmount.Should().Be(0.06m);
    }

    [Fact]
    public async Task Linea_XML_con_cantidad_editada_conserva_IVA_e_IRBPNR_fiscales()
    {
        var (repo, lineId) = ReceptionWithTaxedLine();

        var saved = await CreateWithLineAsync(
            2,
            new PurchaseLineInput(null, "XML", 4m, 1.11m, "10", PurchaseReceptionLineId: lineId),
            repo
        );

        var line = saved.Lines.Single();
        line.Quantity.Should().Be(4m);
        line.OperationalCostBase.Should().Be(4.44m);
        line.TaxableBase.Should().Be(4.44m);
        line.VatAmount.Should().Be(0.49m);
        line.XmlVatAmount.Should().Be(0.49m);
        line.IrbpnrAmount.Should().Be(0.06m);
        line.Taxes.Should().OnlyContain(t => t.Source == ERP.Domain.Modules.Purchases.Enums.PurchaseTaxSource.Xml);
        line.Taxes.Single(t => t.TaxCode == "5").TaxableBase.Should().Be(3m);
    }

    [Fact]
    public async Task Update_linea_XML_con_precio_editado_conserva_IVA_fiscal()
    {
        var (repo, lineId) = ReceptionWithTaxedLine();

        var invoice = await UpdateWithLineAsync(
            2,
            new PurchaseLineInput(null, "XML", 3m, 2m, "10", PurchaseReceptionLineId: lineId),
            repo
        );

        var line = invoice.Lines.Single();
        line.UnitPrice.Should().Be(2m);
        line.OperationalCostBase.Should().Be(6m);
        line.TaxableBase.Should().Be(6m);
        line.VatAmount.Should().Be(0.49m);
        line.XmlVatAmount.Should().Be(0.49m);
        line.IrbpnrAmount.Should().Be(0.06m);
        line.Taxes.Should().OnlyContain(t => t.Source == ERP.Domain.Modules.Purchases.Enums.PurchaseTaxSource.Xml);
    }
}
