using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// P0-02 Fase 7 — pruebas PostgreSQL reales de <c>ApplySupplierCreditHandler</c>/
/// <c>ReverseSupplierCreditApplicationHandler</c> (§10 Fase 7, diseño §15.4/§16.2ter/§19.1):
/// orden de locks A→B, sobreaplicación bajo Lock B, idempotencia, §5.1 casos 4/5, y traducción
/// evento→PostingFact de ambos translators contra el Posting Engine real.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ApplySupplierCreditConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_apply_supplier_credit_test")
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
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
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

        var supplier = BusinessPartner.Create(_tenantId, "05", "1710034065", 1, "Proveedor Test", _userId);
        var paymentTerm = PaymentTerm.Create(
            _tenantId,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        db.BusinessPartners.Add(supplier);
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
            saleConfig: ERP.Domain.Modules.Items.ValueObjects.ItemSaleConfig.Create(isForSale: true),
            stockConfig: ERP.Domain.Modules.Items.ValueObjects.ItemStockConfig.Create(tracksStock: true),
            createdBy: _userId
        );
        db.Set<ERP.Domain.Modules.Items.Entities.Item>().Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(MediatR.IPublisher? publisher = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            // ADR-020 (FROZEN, Entity Tracking) — obligatorio: SupplierCredit.ApplyToPayable/
            // ReverseApplication agregan un SupplierCreditMovement nuevo (Guid.NewGuid() en la
            // factory de dominio) a la colección Movements de un agregado ya trackeado por
            // Include() — sin este interceptor, EF Core clasifica mal la entidad hija como
            // Modified (OriginalValue==CurrentValue) y produce un UPDATE no-op que afecta 0 filas
            // → DbUpdateConcurrencyException espuria, incluso sin concurrencia real.
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => _tenantId),
            publisher ?? new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );
    }

    private PurchaseInvoice BuildConfirmedInvoice(decimal quantity, decimal unitPrice, string invoiceNumber)
    {
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            invoiceNumber,
            DateOnly.FromDateTime(DateTime.UtcNow),
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
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, _userId);
        inv.Confirm(_userId);
        return inv;
    }

    /// <summary>
    /// Crea un SupplierCredit real con AvailableAmount == creditAmount: factura origen ya pagada
    /// en su totalidad (BalanceDueBeforeApplication=0) antes de Authorize, de modo que el 100% del
    /// GrandTotal de la devolución se convierte en excedente (SupplierCredit), sin necesidad de
    /// StockRepository/Item/Warehouse (Authorize() de dominio no toca inventario — eso lo hace el
    /// handler de Fase 6, fuera del alcance de esta prueba).
    /// </summary>
    private async Task<Guid> SeedSupplierCreditAsync(decimal creditAmount)
    {
        await using var db = CreateContext();
        var sourceInv = BuildConfirmedInvoice(1m, creditAmount, $"001-001-{Random.Shared.Next(100000, 999999)}");
        var sourcePayable = PurchasePayable.Create(
            _tenantId,
            _companyId,
            sourceInv.Id,
            _supplierId,
            sourceInv.GrandTotal,
            _userId
        );
        sourcePayable.RegisterPayment(sourcePayable.TotalAmount, _userId);

        var ret = PurchaseReturn.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            sourceInv.Id,
            _supplierId,
            "Producto defectuoso",
            new[]
            {
                new PurchaseReturn.DraftLineInput(sourceInv.Lines[0].Id, _itemId, 1m, _warehouseId),
            },
            _userId,
            Guid.NewGuid(),
            "hash-draft"
        );

        var original = sourceInv.Lines[0];
        var originalLinesByDetailId = new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
        {
            [original.Id] = new PurchaseReturn.OriginalLineSnapshot(
                original.Quantity,
                original.LineSubtotal,
                original.DiscountAmount,
                original.VatAmount,
                original.IceAmount,
                original.VatCode,
                original.VatRate,
                original.IceCode,
                original.IceRate,
                original.LandedUnitCost
            ),
        };

        var credit = ret.Authorize(
            Random.Shared.Next(1, 99999999).ToString("D8"),
            originalLinesByDetailId,
            balanceDueBeforeApplication: 0m,
            sourceInv.CurrencyCode,
            hasIssuedWithholding: false,
            _userId,
            Guid.NewGuid(),
            "hash-authorize"
        );

        db.PurchaseInvoices.Add(sourceInv);
        db.Set<PurchasePayable>().Add(sourcePayable);
        db.PurchaseReturns.Add(ret);
        db.Set<SupplierCredit>().Add(credit!);
        await db.SaveChangesAsync();

        return credit!.Id;
    }

    private async Task<Guid> SeedTargetPayableAsync(decimal totalAmount, bool cancelled = false)
    {
        await using var db = CreateContext();
        var inv = BuildConfirmedInvoice(1m, totalAmount, $"001-001-{Random.Shared.Next(100000, 999999)}");
        var payable = PurchasePayable.Create(_tenantId, _companyId, inv.Id, _supplierId, totalAmount, _userId);
        if (cancelled)
            payable.CancelPayable();
        db.PurchaseInvoices.Add(inv);
        db.Set<PurchasePayable>().Add(payable);
        await db.SaveChangesAsync();
        return payable.Id;
    }

    // ── 1. Dos aplicaciones simultáneas del mismo crédito ────────────────

    [Fact]
    public async Task Dos_aplicaciones_simultaneas_sobre_el_mismo_credito_LockB_serializa_y_revalida_disponible()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payable1 = await SeedTargetPayableAsync(200m);
        var payable2 = await SeedTargetPayableAsync(200m);

        var t1 = ExecuteApplyRealAsync(creditId, payable1, 70m, Guid.NewGuid());
        var t2 = ExecuteApplyRealAsync(creditId, payable2, 70m, Guid.NewGuid());
        var results = await Task.WhenAll(t1, t2);

        // AvailableAmount=100: 70+70=140 excede — una tiene éxito, la otra SC-003 determinista.
        results.Count(r => r.Success).Should().Be(1);
        results.Count(r => !r.Success).Should().Be(1);
        results.First(r => !r.Success).Error.Should().Contain("excede");

        await using var verify = CreateContext();
        var credit = await verify.Set<SupplierCredit>().AsNoTracking().FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(30m);
        credit.AvailableAmount.Should().BeInRange(0m, credit.OriginalAmount);
    }

    // ── 2. Orden de locks A→B sin deadlock ────────────────────────────────

    [Fact]
    public async Task Orden_de_locks_A_a_B_no_produce_deadlock_con_operaciones_cruzadas_concurrentes()
    {
        var credit1 = await SeedSupplierCreditAsync(100m);
        var credit2 = await SeedSupplierCreditAsync(100m);
        var payable1 = await SeedTargetPayableAsync(200m);
        var payable2 = await SeedTargetPayableAsync(200m);

        // Ambas operaciones adquieren Lock A (destino) antes que Lock B (crédito) — mismo orden
        // fijo en las dos, incluso cuando los destinos/créditos están cruzados, nunca debe
        // producirse un deadlock real de PostgreSQL.
        var t1 = ExecuteApplyRealAsync(credit1, payable1, 50m, Guid.NewGuid());
        var t2 = ExecuteApplyRealAsync(credit2, payable2, 50m, Guid.NewGuid());

        var act = async () => await Task.WhenAll(t1, t2);
        await act.Should().NotThrowAsync(because: "el orden fijo A→B debe evitar cualquier deadlock");

        var results = await Task.WhenAll(t1, t2);
        results.Should().OnlyContain(r => r.Success);
    }

    // ── 3. Idempotencia — 4 escenarios de §16.2ter ────────────────────────

    [Fact]
    public async Task Idempotencia_mismo_CRI_mismo_payload_concurrente_produce_un_solo_efecto()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payableId = await SeedTargetPayableAsync(200m);
        var cri = Guid.NewGuid();

        var t1 = ExecuteApplyRealAsync(creditId, payableId, 40m, cri);
        var t2 = ExecuteApplyRealAsync(creditId, payableId, 40m, cri);
        var results = await Task.WhenAll(t1, t2);

        results.Should().OnlyContain(r => r.Success);

        await using var verify = CreateContext();
        var count = await verify
            .Set<SupplierCreditMovement>()
            .CountAsync(m => m.SupplierCreditId == creditId && m.ClientRequestId == cri);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Idempotencia_mismo_CRI_payload_distinto_una_exitosa_la_otra_SC_006()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payableId = await SeedTargetPayableAsync(200m);
        var cri = Guid.NewGuid();

        var t1 = ExecuteApplyRealAsync(creditId, payableId, 40m, cri);
        var t2 = ExecuteApplyRealAsync(creditId, payableId, 25m, cri);
        var results = await Task.WhenAll(t1, t2);

        results.Count(r => r.Success).Should().Be(1);
        results.Count(r => !r.Success).Should().Be(1);

        await using var verify = CreateContext();
        var count = await verify
            .Set<SupplierCreditMovement>()
            .CountAsync(m => m.SupplierCreditId == creditId && m.ClientRequestId == cri);
        count.Should().Be(1, "el payload distinto no debe generar un segundo movimiento");
    }

    [Fact]
    public async Task Idempotencia_claves_distintas_producen_dos_efectos_independientes()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payable1 = await SeedTargetPayableAsync(200m);
        var payable2 = await SeedTargetPayableAsync(200m);

        var t1 = ExecuteApplyRealAsync(creditId, payable1, 20m, Guid.NewGuid());
        var t2 = ExecuteApplyRealAsync(creditId, payable2, 20m, Guid.NewGuid());
        var results = await Task.WhenAll(t1, t2);

        results.Should().OnlyContain(r => r.Success);

        await using var verify = CreateContext();
        var credit = await verify.Set<SupplierCredit>().AsNoTracking().FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(60m);
    }

    [Fact]
    public async Task Idempotencia_reintento_tras_commit_exitoso_sin_respuesta_no_duplica()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payableId = await SeedTargetPayableAsync(200m);
        var cri = Guid.NewGuid();

        var first = await ExecuteApplyRealAsync(creditId, payableId, 40m, cri);
        first.Success.Should().BeTrue(first.Error);

        var retry = await ExecuteApplyRealAsync(creditId, payableId, 40m, cri);
        retry.Success.Should().BeTrue();
        retry.AvailableAmount.Should().Be(first.AvailableAmount);

        await using var verify = CreateContext();
        var count = await verify
            .Set<SupplierCreditMovement>()
            .CountAsync(m => m.SupplierCreditId == creditId && m.ClientRequestId == cri);
        count.Should().Be(1);
    }

    // ── 4. §5.1 caso 4 (aplicar sobre CxP cancelled) y caso 5 (revertir tras cancelación) ──

    [Fact]
    public async Task Caso4_Aplicar_sobre_CxP_destino_cancelled_rechaza_SC_002()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payableId = await SeedTargetPayableAsync(200m, cancelled: true);

        var result = await ExecuteApplyRealAsync(creditId, payableId, 40m, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("anulada");

        await using var verify = CreateContext();
        var credit = await verify.Set<SupplierCredit>().AsNoTracking().FirstAsync(c => c.Id == creditId);
        credit.AvailableAmount.Should().Be(100m);
    }

    [Fact]
    public async Task Caso5_Revertir_aplicacion_despues_de_cancelar_el_destino_rechaza_SC_014()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payableId = await SeedTargetPayableAsync(200m);

        var apply = await ExecuteApplyRealAsync(creditId, payableId, 40m, Guid.NewGuid());
        apply.Success.Should().BeTrue();

        await using (var cancelDb = CreateContext())
        {
            var payable = await cancelDb.Set<PurchasePayable>().FirstAsync(p => p.Id == payableId);
            // §5.1 caso 5 exige PaidAmount==0 para poder cancelar (CancelPayable lo garantiza).
            payable.CancelPayable();
            await cancelDb.SaveChangesAsync();
        }

        Guid movementId;
        await using (var readDb = CreateContext())
        {
            var credit = await readDb.Set<SupplierCredit>().Include(c => c.Movements).AsNoTracking().FirstAsync(c => c.Id == creditId);
            movementId = credit.Movements.Single(m => m.TargetPurchasePayableId == payableId).Id;
        }

        await using var db = CreateContext();
        var reverseHandler = new ReverseSupplierCreditApplicationHandler(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchasePayableRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

        var result = await reverseHandler.Handle(
            new ReverseSupplierCreditApplicationCommand(creditId, movementId, payableId, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("anulada");

        await using var verify = CreateContext();
        var creditFinal = await verify.Set<SupplierCredit>().AsNoTracking().FirstAsync(c => c.Id == creditId);
        creditFinal.AvailableAmount.Should().Be(60m, "el crédito no debe mutar cuando la reversa queda bloqueada por SC-014");
    }

    // ── Reversa concurrente contra el mismo movimiento original ──────────

    [Fact]
    public async Task Dos_reversas_concurrentes_sobre_el_mismo_movimiento_solo_una_tiene_exito_la_otra_SC_011()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payableId = await SeedTargetPayableAsync(200m);

        var apply = await ExecuteApplyRealAsync(creditId, payableId, 40m, Guid.NewGuid());
        apply.Success.Should().BeTrue();

        Guid movementId;
        await using (var readDb = CreateContext())
        {
            var credit = await readDb
                .Set<SupplierCredit>()
                .Include(c => c.Movements)
                .AsNoTracking()
                .FirstAsync(c => c.Id == creditId);
            movementId = credit.Movements.Single(m => m.TargetPurchasePayableId == payableId).Id;
        }

        var t1 = ExecuteReverseRealAsync(creditId, movementId, payableId, Guid.NewGuid());
        var t2 = ExecuteReverseRealAsync(creditId, movementId, payableId, Guid.NewGuid());
        var results = await Task.WhenAll(t1, t2);

        // Lock B serializa: la primera en obtener el lock revierte con éxito; la segunda, al
        // recargar bajo el lock, encuentra el movimiento original ya revertido (EnsureNotAlreadyReversed
        // en el dominio) y se rechaza de forma determinista — nunca ambas reversas persisten.
        results.Count(r => r.Success).Should().Be(1);
        results.Count(r => !r.Success).Should().Be(1);
        results.First(r => !r.Success).Error.Should().Contain("ya fue revertido");

        await using var verify = CreateContext();

        // Sin doble reversa: exactamente 1 movimiento ReversalOfApplication para este crédito.
        var reversalCount = await verify
            .Set<SupplierCreditMovement>()
            .CountAsync(m =>
                m.SupplierCreditId == creditId
                && m.ReversalOfMovementId == movementId
            );
        reversalCount.Should().Be(1, "no debe haber doble reversa del mismo movimiento");

        // Sin doble asiento contable: a lo sumo un JournalEntry por SourceEventId de reversa
        // (puede ser 0 si no hay PostingRule sembrada para este test, lo relevante es que nunca
        // haya más de 1 — comprobado agrupando por SourceEventId sobre todos los movimientos de
        // reversa de este crédito).
        var reversalMovementIds = await verify
            .Set<SupplierCreditMovement>()
            .Where(m => m.SupplierCreditId == creditId && m.ReversalOfMovementId == movementId)
            .Select(m => m.Id)
            .ToListAsync();
        var reversalEntries = await verify
            .JournalEntries.Where(e => reversalMovementIds.Contains(e.SourceEventId))
            .ToListAsync();
        var duplicateEntries = reversalEntries
            .GroupBy(e => e.SourceEventId)
            .Where(g => g.Count() > 1)
            .ToList();
        duplicateEntries.Should().BeEmpty("no debe haber doble asiento por la misma reversa");

        // Saldo final consistente: crédito vuelve a 100 (el excedente de la reversa exitosa se
        // restituyó una sola vez), payable vuelve a SupplierCreditAppliedAmount=0.
        var creditFinal = await verify.Set<SupplierCredit>().AsNoTracking().FirstAsync(c => c.Id == creditId);
        creditFinal.AvailableAmount.Should().Be(100m);

        var payableFinal = await verify.Set<PurchasePayable>().AsNoTracking().FirstAsync(p => p.Id == payableId);
        payableFinal.SupplierCreditAppliedAmount.Should().Be(0m);
    }

    private async Task<(bool Success, string? Error)> ExecuteReverseRealAsync(
        Guid creditId,
        Guid originalMovementId,
        Guid targetPayableId,
        Guid cri
    )
    {
        await using var db = CreateContext();
        var handler = new ReverseSupplierCreditApplicationHandler(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchasePayableRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
        var result = await handler.Handle(
            new ReverseSupplierCreditApplicationCommand(creditId, originalMovementId, targetPayableId, cri),
            CancellationToken.None
        );
        return (result.IsSuccess, result.IsSuccess ? null : result.Error);
    }

    // ── Traducción evento→PostingFact (ambos translators, contra Posting Engine real) ──

    [Fact]
    public async Task Aplicar_credito_genera_JournalEntry_Posted_via_SupplierCreditAppliedPostingTranslator()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payableId = await SeedTargetPayableAsync(200m);

        var (db, _) = BuildWiredContext();
        await SeedRuleAsync(db, "SupplierCreditApplied");

        var handler = BuildWiredApplyHandler(db);
        var result = await handler.Handle(
            new ApplySupplierCreditCommand(creditId, payableId, 40m, Guid.NewGuid()),
            CancellationToken.None
        );
        result.IsSuccess.Should().BeTrue(result.Error);

        await using var verify = CreateContext();
        var movementId = result.Value!.Movements.Single(m => m.TargetPurchasePayableId == payableId).Id;
        var entry = await verify.JournalEntries.Include(e => e.Lines).FirstOrDefaultAsync(x =>
            x.SourceEventId == movementId
        );

        entry.Should().NotBeNull();
        entry!.Status.Should().Be(JournalEntryStatus.Posted);
        entry.SourceModule.Should().Be("Purchases");
        entry.SourceEventType.Should().Be("SupplierCreditApplied");
        entry.Lines.Sum(l => l.Debit).Should().Be(40m);
        entry.Lines.Sum(l => l.Credit).Should().Be(40m);
    }

    [Fact]
    public async Task Revertir_aplicacion_genera_JournalEntry_via_SupplierCreditApplicationReversedPostingTranslator()
    {
        var creditId = await SeedSupplierCreditAsync(100m);
        var payableId = await SeedTargetPayableAsync(200m);

        var (db, _) = BuildWiredContext();
        await SeedRuleAsync(db, "SupplierCreditApplied");
        await SeedRuleAsync(db, "SupplierCreditApplicationReversed");

        var applyHandler = BuildWiredApplyHandler(db);
        var applyResult = await applyHandler.Handle(
            new ApplySupplierCreditCommand(creditId, payableId, 40m, Guid.NewGuid()),
            CancellationToken.None
        );
        applyResult.IsSuccess.Should().BeTrue(applyResult.Error);
        var movementId = applyResult.Value!.Movements.Single(m => m.TargetPurchasePayableId == payableId).Id;

        var reverseHandler = new ReverseSupplierCreditApplicationHandler(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchasePayableRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
        var reverseResult = await reverseHandler.Handle(
            new ReverseSupplierCreditApplicationCommand(creditId, movementId, payableId, Guid.NewGuid()),
            CancellationToken.None
        );
        reverseResult.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reversalMovementId = reverseResult
            .Value!.Movements.Single(m => m.ReversalOfMovementId == movementId)
            .Id;
        var entry = await verify.JournalEntries.Include(e => e.Lines).FirstOrDefaultAsync(x =>
            x.SourceEventId == reversalMovementId
        );

        entry.Should().NotBeNull();
        entry!.SourceEventType.Should().Be("SupplierCreditApplicationReversed");
        entry.Lines.Sum(l => l.Debit).Should().Be(40m);
        entry.Lines.Sum(l => l.Credit).Should().Be(40m);
    }

    // ── Infraestructura de wiring real (Posting Engine + Application handler) ──

    private (ErpDbContext db, MediatR.IPublisher publisher) BuildWiredContext()
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;
        var db = new ErpDbContext(
            options,
            new FixedCurrentTenant(() => _tenantId),
            deferred,
            new FixedCurrentCompany(() => _companyId)
        );

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(() => _tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(() => _companyId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        // SupplierCreditAuditHandler (Entity Audit, ADR-022) también escucha
        // SupplierCreditAppliedEvent/SupplierCreditApplicationReversedEvent — el escaneo de
        // ensamblado de AddMediatR lo descubre igual que a los translators, así que requiere su
        // propia cadena de dependencias registrada aquí (mismo criterio que
        // PurchaseInvoiceConfirmedPostingIntegrationTests, Fase 3.4).
        services.AddScoped(
            typeof(ERP.Application.Audit.IAuditWriter<>),
            typeof(ERP.Infrastructure.Audit.EfAuditWriter<>)
        );
        services.AddScoped<ERP.Application.Audit.IAuditService, ERP.Infrastructure.Audit.AuditService>();
        services.AddScoped<ERP.Application.Audit.IAuditContext>(_ => new ERP.Infrastructure.Tests.Audit.FixedAuditContext(
            () => _tenantId,
            () => _companyId,
            Guid.NewGuid()
        ));
        services.AddScoped<Domain.Modules.Purchases.Interfaces.ISupplierCreditRepository>(sp =>
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId))
        );
        // P0-02 Fase 8 — SupplierCreditAuditHandler ahora también depende de
        // ISupplierCreditRefundTransactionRepository (extensión autorizada del handler de Fase 7
        // para cubrir Refunded/RefundReversed) — debe registrarse aquí igual que las demás
        // dependencias del fan-out de MediatR para que la resolución de DI no falle.
        services.AddScoped<Domain.Modules.Finance.Interfaces.ISupplierCreditRefundTransactionRepository>(sp =>
            new ERP.Infrastructure.Persistence.Repositories.Finance.SupplierCreditRefundTransactionRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            )
        );
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(SupplierCreditAppliedPostingTranslator).Assembly)
        );

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<MediatR.IPublisher>();

        return (db, deferred);
    }

    private ApplySupplierCreditHandler BuildWiredApplyHandler(ErpDbContext db) =>
        new(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchasePayableRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private async Task SeedRuleAsync(ErpDbContext db, string factType)
    {
        var debitAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.{Guid.NewGuid():N}"[..8]),
            "CxP / Crédito debito",
            null,
            AccountType.Liability,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _userId
        );
        var creditAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"2.{Guid.NewGuid():N}"[..8]),
            "Crédito a favor de proveedores",
            null,
            AccountType.Liability,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _userId
        );
        db.Accounts.AddRange(debitAccount, creditAccount);

        var rule = PostingRule.Create(_tenantId, _companyId, "Purchases", factType, null, null, null, _userId);
        rule.AddLine(debitAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(creditAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);
        db.PostingRules.Add(rule);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodExists = await db.AccountingPeriods.AnyAsync(p =>
            p.TenantId == _tenantId
            && p.CompanyId == _companyId
            && p.FiscalYear == today.Year
            && p.PeriodNumber == today.Month
        );
        if (!periodExists)
        {
            var period = AccountingPeriod.Create(
                _tenantId,
                _companyId,
                today.Year,
                today.Month,
                new DateOnly(today.Year, today.Month, 1),
                new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)),
                _userId
            );
            db.AccountingPeriods.Add(period);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Ejecuta ApplySupplierCreditHandler contra un ErpDbContext propio (sin wiring de Posting Engine) — usado por las pruebas de locks/idempotencia que no necesitan contabilidad.</summary>
    private async Task<(bool Success, string? Error, decimal? AvailableAmount)> ExecuteApplyRealAsync(
        Guid creditId,
        Guid targetPayableId,
        decimal amount,
        Guid cri
    )
    {
        await using var db = CreateContext();
        var handler = new ApplySupplierCreditHandler(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchasePayableRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
        var result = await handler.Handle(
            new ApplySupplierCreditCommand(creditId, targetPayableId, amount, cri),
            CancellationToken.None
        );
        return (
            result.IsSuccess,
            result.IsSuccess ? null : result.Error,
            result.IsSuccess ? result.Value!.AvailableAmount : null
        );
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

    private sealed class DeferredPublisher : MediatR.IPublisher
    {
        public MediatR.IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : MediatR.INotification => Inner!.Publish(notification, cancellationToken);
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
