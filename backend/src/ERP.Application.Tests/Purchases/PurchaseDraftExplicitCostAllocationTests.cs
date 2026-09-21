using ERP.Application.Tests.TestSupport;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using FluentAssertions;
using Moq;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// PURCHASE-DISTRIBUTE-COST-BEFORE-SAVE-01 — cuando el cliente envía FreightAllocated/
/// OtherCostsAllocated explícitos por línea (modal "Distribuir flete/gasto" aplicado ANTES de
/// guardar), Create/UpdatePurchaseDraftHandler deben usarlos tal cual y NO reprorratear con
/// DistributeCosts (que ignora la selección manual de líneas incluidas/excluidas).
/// </summary>
public sealed class PurchaseDraftExplicitCostAllocationTests
{
    [Theory]
    [InlineData("2026-09-03T21:50")]
    [InlineData("2026-09-03T21:50:00Z")]
    [InlineData(null)]
    public async Task CreateDraft_normalizes_reception_date_before_save_and_allows_manual_purchase(string? date)
    {
        var authorizationDate = date is null ? (DateTime?)null
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
        var command = new CreatePurchaseDraftCommand(SupplierId, "01", "001-001-000000001",
            new DateOnly(2026, 9, 3), [new PurchaseLineInput(null, "Producto", 1m, 100m, "10")],
            AuthorizationDate: authorizationDate);

        var result = await BuildCreateHandler(repo).Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        saved!.AuthorizationDate.Should().Be(authorizationDate is null ? null
            : new DateTime(2026, 9, 3, 21, 50, 0, DateTimeKind.Utc));
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
                r.GetByTypeAsync(SupplierId, Domain.MasterData.Enums.RoleType.Supplier, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(role);
        return roleRepo;
    }

    private static Mock<IPaymentTermDefaultResolver> BuildPaymentTermResolver()
    {
        var resolver = new Mock<IPaymentTermDefaultResolver>();
        resolver
            .Setup(r => r.ResolveForPurchaseAsync(SupplierId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.Success(PaymentTerm.Create(TenantId, "CONTADO", "Contado", 1, 0, UserId)));
        return resolver;
    }

    private static Mock<PurchaseTaxResolver> BuildTaxResolver()
    {
        var tax = new Mock<PurchaseTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));
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

    [Fact]
    public async Task CreateDraft_con_FreightAllocated_explicito_por_linea_lo_persiste_tal_cual()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var handler = BuildCreateHandler(repo);

        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PurchaseLineInput(null, "Producto A", 1m, 100m, "10", FreightAllocated: 10m),
                new PurchaseLineInput(null, "Producto B", 1m, 300m, "10", FreightAllocated: 30m),
            ]
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var lines = result.Value!.Lines;
        lines.Should().HaveCount(2);
        lines[0].FreightAllocated.Should().Be(10m);
        lines[1].FreightAllocated.Should().Be(30m);
        result.Value.TotalFreight.Should().Be(40m);
    }

    [Fact]
    public async Task CreateDraft_con_asignacion_explicita_solo_en_una_linea_no_toca_la_otra()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var handler = BuildCreateHandler(repo);

        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PurchaseLineInput(null, "Producto A", 1m, 100m, "10", FreightAllocated: 15m),
                new PurchaseLineInput(null, "Producto B (excluida)", 1m, 100m, "10"),
            ]
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].FreightAllocated.Should().Be(15m);
        result.Value.Lines[1].FreightAllocated.Should().Be(0m);
    }

    [Fact]
    public async Task CreateDraft_sin_asignacion_explicita_sigue_usando_DistributeCosts_por_FreightCost()
    {
        // Regresión: si ninguna línea trae FreightAllocated explícito, se preserva el comportamiento
        // histórico (reprorratea cmd.FreightCost proporcionalmente entre TODAS las líneas).
        var repo = new Mock<IPurchaseInvoiceRepository>();
        var handler = BuildCreateHandler(repo);

        var cmd = new CreatePurchaseDraftCommand(
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PurchaseLineInput(null, "Producto A", 1m, 100m, "10"),
                new PurchaseLineInput(null, "Producto B", 1m, 300m, "10"),
            ],
            FreightCost: 40m
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].FreightAllocated.Should().Be(10m);
        result.Value.Lines[1].FreightAllocated.Should().Be(30m);
    }

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
            PrecisionPolicyTestDouble.Mock()
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
        var line = PurchaseInvoiceDetail.Create(inv.Id, TenantId, "Producto viejo", 1m, 50m, "10", "UNIT");
        inv.ReplaceLines([line], UserId);
        return inv;
    }

    [Fact]
    public async Task UpdateDraft_con_FreightAllocated_explicito_por_linea_lo_persiste_tal_cual()
    {
        var inv = CreateExistingDraft();
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inv);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var handler = BuildUpdateHandler(repo);

        var cmd = new UpdatePurchaseDraftCommand(
            inv.Id,
            SupplierId,
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PurchaseLineInput(null, "Producto A", 1m, 100m, "10", OtherCostsAllocated: 8m),
                new PurchaseLineInput(null, "Producto B", 1m, 400m, "10", OtherCostsAllocated: 32m),
            ]
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].OtherCostsAllocated.Should().Be(8m);
        result.Value.Lines[1].OtherCostsAllocated.Should().Be(32m);
        result.Value.TotalOtherCosts.Should().Be(40m);
    }

    // ── ERP-PRECISION-OPERATIONAL-05B1: purchaseUnitPriceDecimals gobierna el precio operativo ──

    private static ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider PurchasePolicy(int decimals)
    {
        var mock = new Mock<ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider>();
        mock.Setup(p => p.GetEffectiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrecisionPolicyTestDouble.DefaultDto() with { PurchaseUnitPriceDecimals = decimals });
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

        var result = await BuildCreateHandler(repo, PurchasePolicy(decimals), receptionRepo).Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        return saved!;
    }

    [Theory]
    [InlineData(4, 1.2346)]
    [InlineData(2, 1.23)]
    [InlineData(10, 1.23456789)]
    public async Task Linea_manual_normaliza_el_precio_unitario_a_purchaseUnitPriceDecimals(int decimals, double expected)
    {
        var saved = await CreateWithLineAsync(decimals, new PurchaseLineInput(null, "Manual", 1m, 1.23456789m, "10"));

        saved.Lines.Single().UnitPrice.Should().Be((decimal)expected);
    }

    // Documento de recepción (snapshot fuente inmutable del XML) con una línea de precio original conocido.
    private static (IPurchaseReceptionDocumentRepository Repo, Guid LineId) ReceptionWithLine(decimal xmlUnitPrice)
    {
        var document = ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            ERP.Domain.Modules.Purchases.PurchaseReception.Enums.PurchaseReceptionSourceDocType.Invoice,
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
        var line = ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionLine.Create(
            document.Id,
            TenantId,
            "Línea XML",
            1m,
            xmlUnitPrice,
            vatCode: "10",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.15m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: xmlUnitPrice,
            totalLine: xmlUnitPrice
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
                ERP.Domain.Modules.Purchases.PurchaseReception.Enums.PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        repo.Setup(r => r.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        return (repo.Object, line.Id);
    }

    [Fact]
    public async Task Linea_XML_sin_editar_conserva_exactamente_el_precio_original()
    {
        var (repo, lineId) = ReceptionWithLine(1.234567m);

        var saved = await CreateWithLineAsync(
            2,
            new PurchaseLineInput(null, "XML", 1m, 1.234567m, "10", PurchaseReceptionLineId: lineId),
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
            new PurchaseLineInput(null, "XML editada", 1m, 2.3456789m, "10", PurchaseReceptionLineId: lineId),
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
            new PurchaseLineInput(null, "XML editada", 1m, 1.2346m, "10", PurchaseReceptionLineId: lineId),
            repo
        );

        saved.Lines.Single().UnitPrice.Should().Be(1.2346m);
    }

    [Fact]
    public async Task Linea_XML_cuyo_original_ya_no_se_resuelve_se_trata_como_editada_y_se_normaliza()
    {
        var saved = await CreateWithLineAsync(
            2,
            new PurchaseLineInput(null, "XML", 1m, 1.234567m, "10", PurchaseReceptionLineId: Guid.NewGuid())
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
        repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);

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
            PurchasePolicy(decimals)
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
        var invoice = await UpdateWithLineAsync(4, new PurchaseLineInput(null, "Manual", 1m, 2.345678m, "10"));

        invoice.Lines.Single().UnitPrice.Should().Be(2.3457m);
    }

    [Fact]
    public async Task Update_linea_XML_editada_normaliza_a_purchaseUnitPriceDecimals()
    {
        var (repo, lineId) = ReceptionWithLine(1.234567m);

        var invoice = await UpdateWithLineAsync(
            4,
            new PurchaseLineInput(null, "XML editada", 1m, 2.3456789m, "10", PurchaseReceptionLineId: lineId),
            repo
        );

        invoice.Lines.Single().UnitPrice.Should().Be(2.3457m);
    }
}
