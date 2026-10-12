using ERP.Application.Common;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// PURCHASE-DRAFT-RECOVERY — Update atómico de un borrador de compra (gate PostgreSQL real, mismo
/// criterio que <c>UpdateSalesDraftAtomicityPostgreSqlTests</c> A3).
///
/// El Update borra líneas y cuotas con SQL directo (<c>RemoveLinesByInvoiceAsync</c> /
/// <c>ClearScheduleTrackingAsync</c>) antes del SaveChanges. El fallo forzado es real y ocurre
/// DESPUÉS de esos DELETE: el número de factura colisiona en SaveChanges con el índice único
/// <c>uq_purchase_invoices_tenant_company_supplier_number</c> (no se pre-valida). Con el boundary
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/> el rollback debe ser completo.
///
/// Real: ErpDbContext + migraciones (PostgreSQL 16, Testcontainers), PurchaseInvoiceRepository,
/// BusinessPartnerRepository, UnitOfWork, PostgresDatabaseExceptionTranslator y el handler.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class UpdatePurchaseDraftAtomicityPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_draft_atomicity_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private const string DraftNumber = "001-001-000000001";
    private const string OtherDraftNumber = "001-001-000000002";
    private static readonly DateOnly IssueDate = new(2026, 9, 1);

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private Guid _createdBy;
    private Guid _draftId;

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
            true,
            _createdBy,
            companyId: company.Id
        );
        var supplier = BusinessPartner.Create(
            tenant.Id,
            TaxIdentification.SriRuc,
            "1791352688001",
            2,
            "Proveedor Test",
            _createdBy
        );
        var paymentTerm = PaymentTerm.Create(
            tenant.Id,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        db.PaymentTerms.Add(paymentTerm);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;

        _draftId = await SeedDraftAsync(DraftNumber, withManualSchedule: true);
        await SeedDraftAsync(OtherDraftNumber, withManualSchedule: false);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            // Mismo interceptor que la DI de producción (ver UpdateSalesDraftAtomicityPostgreSqlTests).
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

    /// <summary>Borrador con 2 líneas (1 × 100 + IVA 15% = 115 c/u, total 230) y, opcional, cuota manual de 230.</summary>
    private async Task<Guid> SeedDraftAsync(string invoiceNumber, bool withManualSchedule)
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
            invoiceNumber,
            IssueDate,
            _createdBy,
            _paymentTermId,
            "Contado",
            1,
            0
        );
        var lines = new[] { "Línea original A", "Línea original B" }
            .Select(desc =>
            {
                var line = PurchaseInvoiceDetail.Create(
                    inv.Id,
                    _tenantId,
                    desc,
                    1m,
                    100m,
                    "10",
                    "UNIT"
                );
                line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
                return line;
            })
            .ToList();
        inv.ReplaceLines(lines, _createdBy);
        if (withManualSchedule)
            inv.ReplaceDraftPaymentSchedule([(1, IssueDate.AddDays(30), 230m, "Original")]);

        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();
        return inv.Id;
    }

    private (UpdatePurchaseDraftHandler Handler, ErpDbContext Db) BuildRealHandler()
    {
        var db = CreateContext();
        var tax = new Mock<PurchaseTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ERP.Application.Common.Services.TaxRateResult(15m, "IVA 15%"));

        var handler = new UpdatePurchaseDraftHandler(
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId)),
            new BusinessPartnerRepository(db),
            // No se alcanza: el Update no cambia proveedor ni condición de pago.
            Mock.Of<IPaymentTermDefaultResolver>(),
            // Líneas sin ItemId ni bodega: el handler nunca consulta ítems/bodegas/recepción.
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            tax.Object,
            Mock.Of<IPurchaseReceptionDocumentRepository>(),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentUser(_createdBy),
            new PostgresDatabaseExceptionTranslator(),
            CreatePrecisionPolicy(),
            // REAL — transacción + rollback + SaveChanges único.
            new UnitOfWork(db)
        );
        return (handler, db);
    }

    private static UpdatePurchaseDraftCommand Command(
        string invoiceNumber,
        List<PurchaseLineInput> lines,
        List<ConfirmScheduleInput>? schedule
    ) =>
        new(
            Guid.Empty,
            Guid.Empty,
            "01",
            invoiceNumber,
            IssueDate,
            lines,
            Schedule: schedule
        );

    private async Task<PurchaseInvoice> ReloadDraftAsync()
    {
        await using var db = CreateContext();
        return await db
            .PurchaseInvoices.AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.PaymentSchedules)
            .SingleAsync(x => x.Id == _draftId);
    }

    [Fact]
    public async Task Fallo_de_persistencia_despues_de_borrar_lineas_y_cuotas_hace_rollback_completo()
    {
        var (handler, db) = BuildRealHandler();
        await using (db)
        {
            var cmd = Command(
                OtherDraftNumber,
                [new PurchaseLineInput(null, "Línea nueva", 3m, 50m, "10")],
                schedule: []
            ) with
            {
                Id = _draftId,
                SupplierId = _supplierId,
            };

            var result = await handler.Handle(cmd, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("Ya existe una compra con este número para este proveedor.");
        }

        var reloaded = await ReloadDraftAsync();
        reloaded.InvoiceNumber.Should().Be(DraftNumber);
        reloaded.Lines.Select(l => l.Description)
            .Should().BeEquivalentTo("Línea original A", "Línea original B");
        reloaded.Lines.Should().OnlyContain(l => l.Quantity == 1m && l.UnitPrice == 100m);
        reloaded.PaymentSchedules.Should().ContainSingle(s =>
            s.InstallmentNumber == 1 && s.Amount == 230m && s.Notes == "Original"
        );
    }

    [Fact]
    public async Task Update_valido_persiste_lineas_y_conserva_cuotas_que_ya_no_cuadran()
    {
        var (handler, db) = BuildRealHandler();
        await using (db)
        {
            var cmd = Command(
                DraftNumber,
                [new PurchaseLineInput(null, "Línea nueva", 3m, 100m, "10")],
                schedule: null
            ) with
            {
                Id = _draftId,
                SupplierId = _supplierId,
            };

            var result = await handler.Handle(cmd, CancellationToken.None);

            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value!.GrandTotal.Should().Be(345m);
        }

        var reloaded = await ReloadDraftAsync();
        reloaded.Lines.Should().ContainSingle(l => l.Description == "Línea nueva" && l.Quantity == 3m);
        reloaded.PaymentSchedules.Should().ContainSingle(s => s.Amount == 230m && s.Notes == "Original");
    }

    [Fact]
    public async Task Update_valido_reemplaza_las_cuotas_dentro_de_la_misma_transaccion()
    {
        var (handler, db) = BuildRealHandler();
        await using (db)
        {
            var cmd = Command(
                DraftNumber,
                [new PurchaseLineInput(null, "Línea original A", 1m, 100m, "10")],
                schedule:
                [
                    new(1, IssueDate.AddDays(15), 15m, "Anticipo"),
                    new(2, IssueDate.AddDays(45), 100m),
                ]
            ) with
            {
                Id = _draftId,
                SupplierId = _supplierId,
            };

            var result = await handler.Handle(cmd, CancellationToken.None);

            result.IsSuccess.Should().BeTrue(result.Error);
        }

        var reloaded = await ReloadDraftAsync();
        reloaded.PaymentSchedules.OrderBy(s => s.InstallmentNumber)
            .Select(s => (s.InstallmentNumber, s.Amount, s.Notes))
            .Should().Equal((1, 15m, "Anticipo"), (2, 100m, (string?)null));
    }

    private static ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider CreatePrecisionPolicy()
    {
        var dto = new ERP.Application.Modules.Companies.UseCases.PrecisionPolicy.EffectivePrecisionPolicyDto(
            ProfileType: "StandardCommercial",
            SalesUnitPriceDecimals: 2,
            PurchaseUnitPriceDecimals: 4,
            QuantityDecimals: 4,
            PercentageDecimals: 2,
            UnitCostDecimals: 6,
            AverageCostDecimals: 6,
            ConversionFactorDecimals: 6,
            SettlementToleranceAmount: 0.02m,
            IsLocked: false,
            LockedAt: null,
            LockedReason: null,
            MoneyDecimals: 2,
            TaxDecimals: 2,
            AccountingDecimals: 2,
            FiscalPercentageDecimals: ERP.Domain.Common.FiscalPrecision.Percentage,
            WarehouseCapacityDecimals: ERP.Domain.Modules.Inventory.Entities.WarehousePrecision.Capacity,
            CreditInstallmentPercentageDecimals: ERP.Domain.Modules.Finance.Entities.CreditTermsPrecision.InstallmentPercentage,
            PackagingWeightDecimals: ERP.Domain.Modules.Items.Entities.ItemPrecision.PackagingWeight
        );

        var mock = new Mock<ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider>();
        mock.Setup(p => p.GetEffectiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        return mock.Object;
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

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "purchase-draft-test";
        public string? Email => null;
        public string? FullName => "Purchase Draft Test";
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
