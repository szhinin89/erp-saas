using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// P0-02 Fase 10 — los 9 casos de invariantes cruzadas de §5.1 (diseño), cada uno con datos reales
/// de extremo a extremo contra PostgreSQL real: PI-CANC-01/02, guard de pago sobre CxP cancelled,
/// SC-002/SC-014, PR-011 (crédito aplicado/reembolsado), permitido tras NC registrada (§5.1 caso
/// 8, FiscalStatus congelado), y cancelación concurrente factura/devolución serializada por Lock A
/// (§5.1 caso 9). Usa <c>NoOpPublisher</c> — estos casos verifican reglas de negocio y locks, no
/// el resultado contable/de auditoría (ya cubierto por otros archivos de Fase 6/7/8).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseReturnCrossInvariantTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_return_cross_invariant_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private Guid _warehouseId;
    private Guid _itemId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = Branch.Create(
            tenantId: _tenantId,
            name: "Matriz",
            address: "Av. Principal 123",
            code: "B01",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: _userId,
            companyId: _companyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        _branchId = branch.Id;

        var supplier = BusinessPartner.Create(
            _tenantId,
            "05",
            "1710034065",
            1,
            "Proveedor Test",
            _userId
        );
        db.BusinessPartners.Add(supplier);
        var paymentTerm = PaymentTerm.Create(
            _tenantId,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        db.Add(paymentTerm);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;

        var warehouse = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(
            _tenantId,
            _branchId,
            "Bodega Principal",
            "BOD-01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _userId,
            _companyId,
            isMain: true
        );
        db.Add(warehouse);
        await db.SaveChangesAsync();
        _warehouseId = warehouse.Id;

        var itemType = ERP.Domain.Modules.Items.Entities.ItemTypeDefinition.Create(
            _tenantId,
            "MERCH",
            "Mercadería",
            1,
            _userId
        );
        db.Set<ERP.Domain.Modules.Items.Entities.ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = ERP.Domain.Modules.Items.Entities.Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Test",
            description: "Producto Test",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ERP.Domain.Modules.Items.ValueObjects.ItemTaxConfig.Create(
                saleVatCode: "10",
                purchaseVatCode: "10"
            ),
            saleConfig: ERP.Domain.Modules.Items.ValueObjects.ItemSaleConfig.Create(
                isForSale: true
            ),
            stockConfig: ERP.Domain.Modules.Items.ValueObjects.ItemStockConfig.Create(
                tracksStock: true
            ),
            createdBy: _userId
        );
        db.Set<ERP.Domain.Modules.Items.Entities.Item>().Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );
    }

    // ── Seeding ────────────────────────────────────────────────────────────

    private sealed record SeededInvoice(Guid InvoiceId, Guid PayableId);

    /// <summary>Factura confirmada + CxP, sin devolución — para los casos que solo necesitan un destino de aplicación/pago.</summary>
    private async Task<SeededInvoice> SeedPlainInvoiceAsync(decimal amount)
    {
        await using var db = CreateContext();
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1791352688001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999):D6}",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto 1",
            quantity: 1m,
            unitPrice: amount,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, _userId);
        inv.Confirm(_userId);

        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            inv.Id,
            "01",
            inv.InvoiceNumber,
            inv.IssueDate,
            inv.IssueDate,
            _userId
        );
        payable.AddInstallment(1, inv.IssueDate.AddDays(30), inv.ConfirmedGrandTotal ?? amount);

        db.PurchaseInvoices.Add(inv);
        db.Set<AccountsPayable>().Add(payable);
        await db.SaveChangesAsync();
        return new SeededInvoice(inv.Id, payable.Id);
    }

    private sealed record SeededReturn(
        Guid InvoiceId,
        Guid PayableId,
        Guid ReturnId,
        Guid? CreditId
    );

    /// <summary>Factura confirmada + CxP + PurchaseReturn Authorized (crea SupplierCredit si <paramref name="paidAmount"/> cubre el total).</summary>
    private async Task<SeededReturn> SeedAuthorizedReturnAsync(
        decimal grandTotal,
        decimal paidAmount
    )
    {
        await using var db = CreateContext();
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1791352688001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999):D6}",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto 1",
            quantity: 1m,
            unitPrice: grandTotal,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, _userId);
        inv.Confirm(_userId);
        var confirmedLine = inv.Lines.Single();

        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            inv.Id,
            "01",
            inv.InvoiceNumber,
            inv.IssueDate,
            inv.IssueDate,
            _userId
        );
        payable.AddInstallment(1, inv.IssueDate.AddDays(30), grandTotal);
        if (paidAmount > 0)
            payable.RegisterPayment(paidAmount, _userId);

        var ret = PurchaseReturn.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            inv.Id,
            _supplierId,
            "Producto defectuoso",
            new[]
            {
                new PurchaseReturn.DraftLineInput(confirmedLine.Id, _itemId, 1m, _warehouseId),
            },
            _userId,
            Guid.NewGuid(),
            "hash-draft"
        );
        var snapshot = new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
        {
            [confirmedLine.Id] = new PurchaseReturn.OriginalLineSnapshot(
                confirmedLine.Quantity,
                confirmedLine.LineSubtotal,
                confirmedLine.DiscountAmount,
                confirmedLine.VatAmount,
                confirmedLine.IceAmount,
                confirmedLine.VatCode,
                confirmedLine.VatRate,
                confirmedLine.IceCode,
                confirmedLine.IceRate,
                confirmedLine.LandedUnitCost
            ),
        };
        var credit = ret.Authorize(
            Random.Shared.Next(1, 99999999).ToString("D8"),
            snapshot,
            balanceDueBeforeApplication: payable.OutstandingAmount,
            inv.CurrencyCode,
            hasIssuedWithholding: false,
            _userId,
            Guid.NewGuid(),
            "hash-authorize"
        );
        if (ret.AppliedToPayableAmount is > 0m)
            payable.ApplyReturnCredit(ret.AppliedToPayableAmount.Value, _userId);

        db.PurchaseInvoices.Add(inv);
        db.Set<AccountsPayable>().Add(payable);
        db.PurchaseReturns.Add(ret);
        if (credit is not null)
            db.Set<SupplierCredit>().Add(credit);
        await db.SaveChangesAsync();

        return new SeededReturn(inv.Id, payable.Id, ret.Id, credit?.Id);
    }

    // ── Handler builders ─────────────────────────────────────────────────

    private CancelPurchaseHandler BuildCancelPurchaseHandler(ErpDbContext db) =>
        new(
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new AccountsPayableRepository(db),
            new StockRepository(
                db,
                new FixedCurrentCompany(() => _companyId),
                new RealDatabaseExceptionTranslator()
            ),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            NullLogger<CancelPurchaseHandler>.Instance,
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentUser(_userId)
        );

    private CancelPurchaseReturnHandler BuildCancelPurchaseReturnHandler(ErpDbContext db) =>
        new(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new AccountsPayableRepository(db),
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new StockRepository(
                db,
                new FixedCurrentCompany(() => _companyId),
                new RealDatabaseExceptionTranslator()
            ),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private ApplySupplierCreditHandler BuildApplyHandler(ErpDbContext db) =>
        new(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new AccountsPayableRepository(db),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private ReverseSupplierCreditApplicationHandler BuildReverseApplyHandler(ErpDbContext db) =>
        new(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new AccountsPayableRepository(db),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private RegisterAndLinkSupplierCreditNoteHandler BuildLinkCreditNoteHandler(ErpDbContext db) =>
        new(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private RegisterPaymentCommandHandler BuildRegisterPaymentHandler(ErpDbContext db) =>
        new(
            new PaymentRepository(db),
            new AccountsPayableRepository(db),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new CompanyFinancialDestinationRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentUser(_userId)
        );

    // ── Caso 1 — PI-CANC-01 ───────────────────────────────────────────────

    [Fact]
    public async Task Caso1_Cancelar_factura_con_devolucion_Authorized_asociada_bloqueado_PI_CANC_01()
    {
        var seeded = await SeedAuthorizedReturnAsync(grandTotal: 100m, paidAmount: 0m);

        await using var db = CreateContext();
        var handler = BuildCancelPurchaseHandler(db);
        var result = await handler.Handle(
            new CancelPurchaseCommand(seeded.InvoiceId, "Intento de anulación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("devolución");

        await using var verify = CreateContext();
        var invoice = await verify
            .PurchaseInvoices.AsNoTracking()
            .FirstAsync(x => x.Id == seeded.InvoiceId);
        invoice.Status.Should().Be(Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
    }

    // ── Caso 2 — PI-CANC-02 ───────────────────────────────────────────────

    [Fact]
    public async Task Caso2_Cancelar_factura_cuya_CxP_recibio_aplicacion_de_credito_bloqueado_PI_CANC_02()
    {
        var source = await SeedAuthorizedReturnAsync(grandTotal: 100m, paidAmount: 100m); // fuerza SupplierCredit
        source.CreditId.Should().NotBeNull();
        var target = await SeedPlainInvoiceAsync(50m);

        await using (var db = CreateContext())
        {
            var applyHandler = BuildApplyHandler(db);
            var applyResult = await applyHandler.Handle(
                new ApplySupplierCreditCommand(
                    source.CreditId!.Value,
                    target.PayableId,
                    50m,
                    Guid.NewGuid()
                ),
                CancellationToken.None
            );
            applyResult.IsSuccess.Should().BeTrue(applyResult.Error);
        }

        await using var db2 = CreateContext();
        var cancelHandler = BuildCancelPurchaseHandler(db2);
        var result = await cancelHandler.Handle(
            new CancelPurchaseCommand(target.InvoiceId, "Intento de anulación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("crédito");
    }

    // ── Caso 3 — pagar CxP cancelled bloqueado (guard ya existente, ahora bajo lock) ──

    [Fact]
    public async Task Caso3_Pagar_CxP_cancelled_bloqueado()
    {
        var target = await SeedPlainInvoiceAsync(80m);

        await using (var db = CreateContext())
        {
            var cancelHandler = BuildCancelPurchaseHandler(db);
            var cancelResult = await cancelHandler.Handle(
                new CancelPurchaseCommand(target.InvoiceId, "Anulación previa"),
                CancellationToken.None
            );
            cancelResult.IsSuccess.Should().BeTrue(cancelResult.Error);
        }

        await using var db2 = CreateContext();
        var paymentHandler = BuildRegisterPaymentHandler(db2);
        var result = await paymentHandler.Handle(
            new RegisterPaymentCommand(
                _supplierId,
                80m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                null,
                new[] { new PaymentApplicationLineInput(target.PayableId, null, 80m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    // ── Caso 4 — SC-002 ───────────────────────────────────────────────────

    [Fact]
    public async Task Caso4_Aplicar_credito_sobre_CxP_cancelled_bloqueado_SC_002()
    {
        var source = await SeedAuthorizedReturnAsync(grandTotal: 100m, paidAmount: 100m);
        source.CreditId.Should().NotBeNull();
        var target = await SeedPlainInvoiceAsync(50m);

        await using (var db = CreateContext())
        {
            var cancelHandler = BuildCancelPurchaseHandler(db);
            var cancelResult = await cancelHandler.Handle(
                new CancelPurchaseCommand(target.InvoiceId, "Anulación previa"),
                CancellationToken.None
            );
            cancelResult.IsSuccess.Should().BeTrue(cancelResult.Error);
        }

        await using var db2 = CreateContext();
        var applyHandler = BuildApplyHandler(db2);
        var result = await applyHandler.Handle(
            new ApplySupplierCreditCommand(
                source.CreditId!.Value,
                target.PayableId,
                50m,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    // ── Caso 5 — SC-014 (defensa en profundidad, callejón sin salida) ────

    [Fact]
    public async Task Caso5_Revertir_aplicacion_tras_cancelar_la_CxP_destino_bloqueado_SC_014()
    {
        var source = await SeedAuthorizedReturnAsync(grandTotal: 100m, paidAmount: 100m);
        source.CreditId.Should().NotBeNull();
        var target = await SeedPlainInvoiceAsync(50m);

        Guid movementId;
        await using (var db = CreateContext())
        {
            var applyHandler = BuildApplyHandler(db);
            var applyResult = await applyHandler.Handle(
                new ApplySupplierCreditCommand(
                    source.CreditId!.Value,
                    target.PayableId,
                    50m,
                    Guid.NewGuid()
                ),
                CancellationToken.None
            );
            applyResult.IsSuccess.Should().BeTrue(applyResult.Error);
            movementId = applyResult.Value!.Movements.Last().Id;
        }

        // PI-CANC-02 impide llegar a este estado por el flujo normal de cancelación de factura —
        // se fuerza directamente a nivel de dominio para probar la defensa en profundidad de
        // SC-014 (§5.1 caso 5, diseño: "callejón sin salida documentado").
        await using (var db = CreateContext())
        {
            var payable = await db
                .Set<AccountsPayable>()
                .Include(x => x.Installments)
                .FirstAsync(x => x.Id == target.PayableId);
            payable.Cancel(_userId);
            await db.SaveChangesAsync();
        }

        await using var db2 = CreateContext();
        var reverseHandler = BuildReverseApplyHandler(db2);
        var result = await reverseHandler.Handle(
            new ReverseSupplierCreditApplicationCommand(
                source.CreditId!.Value,
                movementId,
                target.PayableId,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();

        await using var verify = CreateContext();
        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(x => x.Id == source.CreditId!.Value);
        credit
            .AvailableAmount.Should()
            .Be(50m, "el crédito no debe cambiar mientras el destino permanezca cancelled");
    }

    // ── Caso 6 — PR-011 (crédito aplicado) ────────────────────────────────

    [Fact]
    public async Task Caso6_Cancelar_devolucion_con_credito_aplicado_bloqueado_PR_011()
    {
        var source = await SeedAuthorizedReturnAsync(grandTotal: 100m, paidAmount: 100m);
        source.CreditId.Should().NotBeNull();
        var target = await SeedPlainInvoiceAsync(30m);

        await using (var db = CreateContext())
        {
            var applyHandler = BuildApplyHandler(db);
            var applyResult = await applyHandler.Handle(
                new ApplySupplierCreditCommand(
                    source.CreditId!.Value,
                    target.PayableId,
                    30m,
                    Guid.NewGuid()
                ),
                CancellationToken.None
            );
            applyResult.IsSuccess.Should().BeTrue(applyResult.Error);
        }

        await using var db2 = CreateContext();
        var cancelReturnHandler = BuildCancelPurchaseReturnHandler(db2);
        var result = await cancelReturnHandler.Handle(
            new CancelPurchaseReturnCommand(
                source.ReturnId,
                "Intento de anulación",
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("crédito");

        await using var verify = CreateContext();
        var ret = await verify
            .PurchaseReturns.AsNoTracking()
            .FirstAsync(x => x.Id == source.ReturnId);
        ret.Status.Should().Be(PurchaseReturnStatus.Authorized);
    }

    // ── Caso 7 — PR-011 (crédito reembolsado, misma fórmula) ─────────────

    [Fact]
    public async Task Caso7_Cancelar_devolucion_con_credito_reembolsado_bloqueado_PR_011()
    {
        var source = await SeedAuthorizedReturnAsync(grandTotal: 100m, paidAmount: 100m);
        source.CreditId.Should().NotBeNull();

        // RegisterRefund vía dominio directo — la orquestación financiera completa del reembolso
        // (destino/cuenta/caja) ya está cubierta exhaustivamente por SupplierCreditRefundConcurrencyTests
        // (Fase 8); aquí solo se necesita AvailableAmount < OriginalAmount para probar que PR-011
        // también detecta reembolsos, no solo aplicaciones (§5.1 caso 7, misma fórmula que caso 6).
        await using (var db = CreateContext())
        {
            var credit = await db.Set<SupplierCredit>()
                .FirstAsync(x => x.Id == source.CreditId!.Value);
            credit.RegisterRefund(20m, _userId, Guid.NewGuid(), "refund-hash-test");
            await db.SaveChangesAsync();
        }

        await using var db2 = CreateContext();
        var cancelReturnHandler = BuildCancelPurchaseReturnHandler(db2);
        var result = await cancelReturnHandler.Handle(
            new CancelPurchaseReturnCommand(
                source.ReturnId,
                "Intento de anulación",
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("crédito");
    }

    // ── Caso 8 — permitido tras NC registrada, FiscalStatus congelado ────

    [Fact]
    public async Task Caso8_Cancelar_devolucion_despues_de_registrar_NC_permitido_FiscalStatus_congelado()
    {
        var source = await SeedAuthorizedReturnAsync(grandTotal: 100m, paidAmount: 0m);

        await using (var db = CreateContext())
        {
            var linkHandler = BuildLinkCreditNoteHandler(db);
            var linkResult = await linkHandler.Handle(
                new RegisterAndLinkSupplierCreditNoteCommand(
                    source.ReturnId,
                    $"AK-{Guid.NewGuid():N}",
                    "1791352688001",
                    "Proveedor Test",
                    $"001-001-{Random.Shared.Next(100000, 999999):D6}",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    100m,
                    0m,
                    100m,
                    "USD",
                    Guid.NewGuid()
                ),
                CancellationToken.None
            );
            linkResult.IsSuccess.Should().BeTrue(linkResult.Error);
        }

        await using var db2 = CreateContext();
        var cancelReturnHandler = BuildCancelPurchaseReturnHandler(db2);
        var result = await cancelReturnHandler.Handle(
            new CancelPurchaseReturnCommand(source.ReturnId, "Anulación tras NC", Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);

        await using var verify = CreateContext();
        var ret = await verify
            .PurchaseReturns.AsNoTracking()
            .FirstAsync(x => x.Id == source.ReturnId);
        ret.Status.Should().Be(PurchaseReturnStatus.Cancelled);
        ret.FiscalStatus.Should()
            .Be(
                Domain
                    .Modules
                    .Purchases
                    .Enums
                    .PurchaseReturnFiscalStatus
                    .SupplierCreditNoteRegistered,
                "el vínculo de NC es puramente documental (§3.14/§5.1 caso 8) — el estado fiscal queda congelado, nunca se sobrescribe"
            );
    }

    // ── Caso 9 — cancelación concurrente factura/devolución, serializada por Lock A ──

    [Fact]
    public async Task Caso9_Cancelacion_concurrente_de_factura_y_devolucion_Authorized_nunca_estado_inconsistente()
    {
        var seeded = await SeedAuthorizedReturnAsync(grandTotal: 100m, paidAmount: 0m);

        var task1 = Task.Run(async () =>
        {
            await using var db = CreateContext();
            var handler = BuildCancelPurchaseHandler(db);
            return await handler.Handle(
                new CancelPurchaseCommand(seeded.InvoiceId, "Cancelación de factura"),
                CancellationToken.None
            );
        });
        var task2 = Task.Run(async () =>
        {
            await using var db = CreateContext();
            var handler = BuildCancelPurchaseReturnHandler(db);
            return await handler.Handle(
                new CancelPurchaseReturnCommand(
                    seeded.ReturnId,
                    "Cancelación de devolución",
                    Guid.NewGuid()
                ),
                CancellationToken.None
            );
        });
        await Task.WhenAll(task1, task2);

        await using var verify = CreateContext();
        var invoice = await verify
            .PurchaseInvoices.AsNoTracking()
            .FirstAsync(x => x.Id == seeded.InvoiceId);
        var ret = await verify
            .PurchaseReturns.AsNoTracking()
            .FirstAsync(x => x.Id == seeded.ReturnId);

        // Invariante central de PI-CANC-01 (§5.1 caso 1): jamás la factura queda Cancelled
        // mientras su devolución permanece Authorized — el escenario huérfano que el lock A existe
        // para prevenir, sin importar el orden real de adquisición.
        var invalidCombination =
            invoice.Status == Domain.Modules.Purchases.Enums.PurchaseStatus.Cancelled
            && ret.Status == PurchaseReturnStatus.Authorized;
        invalidCombination.Should().BeFalse();

        // La devolución siempre termina Cancelled en ambos órdenes deterministas documentados.
        ret.Status.Should().Be(PurchaseReturnStatus.Cancelled);
    }

    // ── Test doubles mínimos ─────────────────────────────────────────────

    private sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId();
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId();
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }

    private sealed class RealDatabaseExceptionTranslator : IDatabaseExceptionTranslator
    {
        public bool TryGetUniqueViolation(Exception exception, out DatabaseUniqueViolationInfo info)
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is Npgsql.PostgresException pg && pg.SqlState == "23505")
                {
                    info = new DatabaseUniqueViolationInfo(
                        pg.SqlState,
                        pg.ConstraintName,
                        pg.TableName,
                        pg.MessageText
                    );
                    return true;
                }
            }
            info = null!;
            return false;
        }
    }
}
