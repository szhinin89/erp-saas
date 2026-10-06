using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Persistence.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Sales;

/// <summary>
/// A3 — Atomic Sales Draft Update (gate PostgreSQL real, obligatorio para cerrar A3).
///
/// Escenario: un Update de Draft que SÍ supera las validaciones iniciales y ejecuta el DELETE
/// directo real de hijos (<c>RemoveLinesByInvoiceAsync</c> → <c>DELETE FROM sales_invoice_details</c>)
/// y luego rechaza en una validación posterior YA EXISTENTE (rama "else" de ADR-033: cronograma
/// manual desalineado con el nuevo saldo pendiente). Antes de A3 ese rechazo dejaba el Draft
/// parcialmente alterado (líneas borradas/reemplazadas persistidas sin transacción). Con el
/// boundary <see cref="IUnitOfWork.ExecuteInTransactionAsync"/> agregado en A3, el rechazo debe
/// producir rollback COMPLETO: el Draft queda exactamente igual que antes del Update.
///
/// Todo es REAL (sin mocks): ErpDbContext + migrations sobre PostgreSQL 16 (Testcontainers),
/// SalesInvoiceRepository, PaymentMethodRepository, UnitOfWork, SriTaxResolver y handler real.
/// Solo se fakean dependencias de contexto de request / catálogos no usados por este flujo
/// (mismo criterio de construcción directa de handlers que PurchaseReturnCrossInvariantTests).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class UpdateSalesDraftAtomicityPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_sales_draft_atomicity_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _customerId;
    private Guid _cashSessionId;
    private Guid _paymentMethodId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );
        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            _createdBy,
            companyId: company.Id
        );
        var customer = BusinessPartner.Create(
            tenant.Id,
            "05",
            "1710034065",
            1,
            "Cliente Test",
            _createdBy
        );
        var establishment = Establishment.Create(
            tenant.Id,
            branchId: branch.Id,
            company.Id,
            code: "001",
            name: "Matriz Test",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        var cashRegister = CashRegister.Create(
            tenant.Id,
            company.Id,
            branch.Id,
            "CAJA-01",
            "Caja Principal",
            _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id,
            company.Id,
            establishment.Id,
            code: "001",
            name: "PE-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);

        // Forma de pago EFECTIVO real (IsCreditAllowed=false → su pago cuenta como cashApplied
        // en SalesPaymentHelper.CalculateCashAppliedAsync, igual que en producción).
        var cashMethod = PaymentMethod.Create(
            tenant.Id,
            "EFECTIVO",
            "Efectivo",
            requiresReference: false,
            isCreditAllowed: false,
            sortOrder: 1,
            createdBy: _createdBy
        );
        db.PaymentMethods.Add(cashMethod);

        // Catálogo SRI real mínimo: IVA código "4" al 15% (tasa usada por el seed de líneas).
        db.SriVatRates.Add(
            new SriVatRate
            {
                Code = "4",
                Name = "IVA 15%",
                Percentage = 15m,
                IsActive = true
            }
        );
        await db.SaveChangesAsync();

        var cashSession = CashSession.Open(
            tenant.Id,
            company.Id,
            branch.Id,
            _createdBy,
            cashRegister.Id,
            "CAJA-01",
            "Caja Principal",
            emissionPoint.Id,
            "001",
            0m,
            _createdBy
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;
        _paymentMethodId = cashMethod.Id;

        await SeedDraftWithManualScheduleAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            // Mismo interceptor que la DI de producción (ver comentario en
            // SalesInvoiceDetailTaxDuplicationIntegrationTests): necesario para que los hijos
            // NUEVOS añadidos a un agregado ya trackeado (reconstrucción de líneas del Update)
            // se clasifiquen como Added y no fallen contra el xmin del agregado raíz.
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    /// <summary>
    /// Estado inicial: Draft con 2 líneas (qty 1, unitPrice 50, IVA "4" 15% → total 115 c/u,
    /// GrandTotal 230), pago efectivo 30 (pendiente 200) y cronograma MANUAL de exactamente
    /// 2 cuotas que suman 200 (calza con el saldo inicial → el Draft puede existir así).
    /// </summary>
    private async Task SeedDraftWithManualScheduleAsync()
    {
        await using var db = CreateContext();

        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            Guid.NewGuid(),
            "Contado",
            installments: 1,
            daysBetween: 0
        );

        var inv = SalesInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            customer,
            invoiceNumber: "A3-ATOMIC-001",
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: _createdBy,
            paymentTerm: paymentTerm,
            cashSessionId: _cashSessionId,
            emissionType: EmissionType.Physical
        );

        var lines = new List<SalesInvoiceDetail>();
        foreach (var desc in new[] { "Línea original A", "Línea original B" })
        {
            var line = SalesInvoiceDetail.Create(
                inv.Id,
                _tenantId,
                desc,
                quantity: 1,
                unitPrice: 50m,
                vatCode: "4",
                uomCode: "UNIT"
            );
            // Simula lo que SalesLineBuilder hace en un Update legítimo: impuestos aplicados.
            line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
            lines.Add(line);
        }
        inv.ReplaceLines(lines, _createdBy);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            _tenantId,
            _paymentMethodId,
            "01",
            "Efectivo",
            30m
        );
        inv.ReplacePayments(new[] { payment }, _createdBy);

        // Cronograma manual (ReplacePaymentSchedule marca IsPaymentScheduleManual=true) con
        // amountOverride = saldo pendiente 200 — mismo valor que pasa Application cuando el
        // cronograma cubre solo el abono pendiente de una venta con pago parcial
        // (SALES-SETTLEMENT-CREDIT-01). Suma exacta 100+100=200 → válido en estado inicial.
        inv.ReplacePaymentSchedule(
            new List<(int, DateOnly, decimal, string?)>
            {
                (1, new DateOnly(2026, 8, 15), 100m, null),
                (2, new DateOnly(2026, 9, 15), 100m, null),
            },
            200m
        );

        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();
    }

    /// <summary>Construye el UpdateSalesDraftHandler REAL contra esta base PostgreSQL.</summary>
    private (UpdateSalesDraftHandler Handler, ErpDbContext Db) BuildRealHandler()
    {
        var db = CreateContext();
        var repo = new SalesInvoiceRepository(db, new FixedCurrentCompany(_companyId));

        // Fakes SOLO de dependencias de contexto/catalálogos que este flujo no usa o que no
        // tienen implementación real directamente construible aquí (mismo criterio que los
        // integration tests existentes al construir handlers a mano). NO se mocketea:
        // ErpDbContext, SalesInvoiceRepository, PaymentMethodRepository, UnitOfWork,
        // SriTaxResolver, BusinessPartnerRepository ni las operaciones Remove*.
        var ptResolver = new Mock<IPaymentTermDefaultResolver>();
        ptResolver
            .Setup(r =>
                r.ResolveForSaleAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<PaymentTerm>.Failure(
                    "Sin condición de pago default — el Update conserva el PaymentTerm vigente."
                )
            );

        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPreferences());

        var creditPolicy = new Mock<ISalesCreditRequirementPolicy>();
        creditPolicy
            .Setup(p =>
                p.ResolveCompanyOrManualAsync(
                    It.IsAny<DateOnly?>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            // No se alcanza en este escenario (PendingBalance != 0 y existen cuotas manuales);
            // se configura igualmente por determinismo.
            .ReturnsAsync(Result<PaymentTerm?>.Failure("No aplica."));

        // Contactos/direcciones del cliente: resolver devuelve (null, null) con colecciones
        // vacías — CustomerSnapshot.Create lo admite (patrón ya usado en UpdateSalesDraftScheduleTests).
        var contacts = new Mock<IBusinessPartnerContactRepository>();
        contacts
            .Setup(r =>
                r.GetByBusinessPartnerAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<BusinessPartnerContact>());
        var locations = new Mock<IBusinessPartnerLocationRepository>();
        locations
            .Setup(r =>
                r.GetByBusinessPartnerAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<BusinessPartnerLocation>());

        var handler = new UpdateSalesDraftHandler(
            // REAL — gate A3: UnitOfWork sobre ErpDbContext (transacción + rollback + SaveChanges único).
            new UnitOfWork(db),
            repo,
            new BusinessPartnerRepository(db),
            Mock.Of<IBusinessPartnerRoleRepository>(),
            contacts.Object,
            locations.Object,
            ptResolver.Object,
            new PaymentMethodRepository(db),
            // ItemRepo real NO es construible aquí (requiere ICurrentBranch del request); para
            // este escenario basta su comportamiento observado: líneas con ItemId=null → el
            // builder nunca lo consulta. No participa en DELETE/reconstrucción/persistencia.
            Mock.Of<IItemRepository>(),
            new SriTaxResolver(db),
            Mock.Of<IPricingResolver>(),
            Mock.Of<IPriceListSelectionResolver>(),
            Mock.Of<ICompanySpecialTaxResponsibilityRepository>(),
            Mock.Of<ERP.Domain.Modules.Inventory.Interfaces.IWarehouseRepository>(),
            Mock.Of<IAverageCostService>(),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_createdBy),
            preferences.Object,
            creditPolicy.Object,
            Mock.Of<ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository>(),
            PrecisionPolicyTestDouble.Mock()
        );

        return (handler, db);
    }

    private static OperationalPreferences DefaultPreferences() =>
        new(
            SalesPos: new SalesPosPreferences(
                true,
                false,
                true,
                0m,
                null,
                false,
                false,
                null,
                null
            ),
            Cash: new CashPreferences(true, true, 0m, true, true, true),
            Purchases: new PurchasesPreferences(null, true, true, true, false),
            Inventory: new InventoryPreferences(false, true, false, 0m),
            Printing: new PrintingPreferences(
                "AskBeforePrint",
                1,
                "80mm",
                false,
                true,
                true,
                false
            ),
            ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
            Notifications: new NotificationsPreferences(true, false, "es")
        );

    private sealed record Snapshot(
        decimal HeaderGrandTotal,
        string? HeaderNotes,
        int LineCount,
        IReadOnlyList<(string Description, decimal Quantity, decimal UnitPrice)> Lines,
        int TaxRowCount,
        int PaymentCount,
        decimal PaymentAmount,
        int ScheduleCount,
        IReadOnlyList<decimal> ScheduleAmounts
    );

    /// <summary>Lectura cruda desde PostgreSQL (AsNoTracking) — no usa entidades trackeadas.</summary>
    private async Task<Snapshot> ReadSnapshotAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        var header = await db.SalesInvoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId);
        var lineIds = await db
            .Set<SalesInvoiceDetail>()
            .AsNoTracking()
            .Where(l => l.InvoiceId == invoiceId)
            .Select(l => l.Id)
            .ToListAsync();
        var lines = await db
            .Set<SalesInvoiceDetail>()
            .AsNoTracking()
            .Where(l => l.InvoiceId == invoiceId)
            .OrderBy(l => l.Description)
            .Select(l => (l.Description, l.Quantity, l.UnitPrice))
            .ToListAsync();
        var taxRows = await db
            .Set<SalesInvoiceDetailTax>()
            .AsNoTracking()
            .CountAsync(t => lineIds.Contains(t.SalesInvoiceDetailId));
        var payments = await db
            .Set<SalesInvoicePayment>()
            .AsNoTracking()
            .Where(p => p.InvoiceId == invoiceId)
            .Select(p => p.Amount)
            .ToListAsync();
        var schedules = await db
            .Set<SalesPaymentSchedule>()
            .AsNoTracking()
            .Where(s => s.SalesInvoiceId == invoiceId)
            .OrderBy(s => s.InstallmentNumber)
            .Select(s => s.Amount)
            .ToListAsync();

        return new Snapshot(
            header.GrandTotal,
            header.Notes,
            lines.Count,
            lines,
            taxRows,
            payments.Count,
            payments.Sum(),
            schedules.Count,
            schedules
        );
    }

    [Fact]
    public async Task UpdateDraft_WhenLateValidationRejectsAfterChildDeletes_RollsBackEntireDraft()
    {
        await using var dbSeed = CreateContext();

        var invoiceId = await dbSeed
            .SalesInvoices.AsNoTracking()
            .Where(i => i.InvoiceNumber == "A3-ATOMIC-001")
            .Select(i => i.Id)
            .SingleAsync();

        // ── SNAPSHOT PREVIO (estado original persistido) ─────────────────────────────
        var before = await ReadSnapshotAsync(invoiceId);
        before.LineCount.Should().Be(2);
        before.Lines.Select(l => l.UnitPrice).Should().AllBeEquivalentTo(50m);
        before.Lines.Select(l => l.Quantity).Should().AllBeEquivalentTo(1m);
        before.HeaderGrandTotal.Should().Be(230m);
        before.TaxRowCount.Should().Be(2);
        before.PaymentCount.Should().Be(1);
        before.PaymentAmount.Should().Be(30m);
        before.ScheduleCount.Should().Be(2);
        before.ScheduleAmounts.Should().Equal(100m, 100m);

        // ── UPDATE: 2 nuevas líneas válidas (ItemId=null → no toca catálogo de ítems;
        // qty 2 × 50 + IVA 15% → GrandTotal 230 otra vez), Payments=null (no se reemplazan
        // pagos), Schedule=null y DueDate=null (no se toca el cronograma en ninguna rama
        // previa). Secuencia REAL dentro de ExecuteInTransactionAsync:
        //   1. SalesLineBuilder PASS (líneas manuales válidas, sin pricing floor).
        //   2. RemoveLinesByInvoiceAsync → DELETE real de las 2 líneas originales + INSERT
        //      de las nuevas (persistido inmediatamente, aún sin commit).
        //   3. settlement: cashApplied=30 (pago existente, método no-crédito) →
        //      PendingBalance=200... el comando cambia el subtotal por línea (2×50=100 c/u,
        //      total 230 con IVA) manteniendo GrandTotal 230 → pendiente sigue siendo 200
        //      y el cronograma calzaría. Para forzar el rechazo POSTERIOR AL DELETE se sube
        //      una línea a qty 2.3 → GrandTotal 259.90 → PendingBalance 229.90 ≠ 200.
        //   4. Rama else: currentScheduleSum(200) != PendingBalance(229.90) e
        //      IsPaymentScheduleManual=true → ValidationFailure EXISTENTE (ADR-033).
        //   5. A3: el rechazo viaja como TransactionalRejectionException → rollback TOTAL.
        var (handler, dbExec) = BuildRealHandler();
        try
        {
            var cmd = new UpdateSalesDraftCommand(
                Id: invoiceId,
                CustomerId: _customerId,
                IssueDate: new DateOnly(2026, 7, 25),
                Lines: new List<SalesLineInput>
                {
                    new(
                        ItemId: null,
                        Description: "Línea update A",
                        Quantity: 2.3m,
                        UnitPrice: 50m,
                        VatCode: "4"
                    ),
                    new(
                        ItemId: null,
                        Description: "Línea update B",
                        Quantity: 1m,
                        UnitPrice: 50m,
                        VatCode: "4"
                    ),
                },
                DueDate: null,
                Notes: "NOTAS DEL UPDATE QUE DEBE REVERTIRSE",
                PaymentTermId: null,
                Payments: null,
                Schedule: null
            );

            var result = await handler.Handle(cmd, CancellationToken.None);

            // El rechazo proviene de la validación EXISTENTE del cronograma personalizado
            // desalineado (mensaje literal del handler — no inventado).
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().NotBeNull();
            result.Error!.Should().Contain("El cronograma fue personalizado");
        }
        finally
        {
            await dbExec.DisposeAsync();
        }

        // ── VERIFICACIÓN DE ROLLBACK DESDE UN CONTEXTO NUEVO (nada trackeado reutilizado) ─
        var after = await ReadSnapshotAsync(invoiceId);

        after.Should().Be(before);

        // Explícitos, además de la igualdad estructural del snapshot:
        after.LineCount.Should().Be(2);
        after.Lines.Select(l => l.Description).Should().Equal("Línea original A", "Línea original B");
        after.Lines.Select(l => l.Quantity).Should().AllBeEquivalentTo(1m);
        after.Lines.Select(l => l.UnitPrice).Should().AllBeEquivalentTo(50m);
        after.TaxRowCount.Should().Be(2);
        after.PaymentCount.Should().Be(1);
        after.PaymentAmount.Should().Be(30m);
        after.ScheduleCount.Should().Be(2);
        after.ScheduleAmounts.Should().Equal(100m, 100m);
        after.HeaderGrandTotal.Should().Be(230m);
        after.HeaderNotes.Should().BeNull(); // la Notes del update rechazado NO debió persistir
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class FixedCurrentBranch(Guid branchId) : ICurrentBranch
    {
        public Guid BranchId => branchId;
        public bool IsAuthenticated => true;
        public bool HasBranchContext => branchId != Guid.Empty;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "a3-test";
        public string? Email => null;
        public string? FullName => "A3 Test";
        public string? Role => null;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
