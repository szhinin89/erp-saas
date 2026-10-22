using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// Snapshot fiscal XML de ICE (tarifa, base e importe declarados) contra PostgreSQL real: se
/// persiste, se reabre, sobrevive a un recálculo posterior y bloquea el descuento incompatible
/// tras la reapertura. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseIceXmlSnapshotPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_ice_snapshot_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _paymentTermId;
    private Guid _supplierId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            true, _createdBy, companyId: company.Id
        );
        var supplier = BusinessPartner.Create(
            tenant.Id, TaxIdentification.SriRuc, "1791352688001", 2, "Distribuidora Andina S.A.", _createdBy);
        var paymentTerm = PaymentTerm.Create(
            tenant.Id, "CONT", "Contado", installments: 1, daysBetweenInstallments: 0, _createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        db.PaymentTerms.Add(paymentTerm);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _paymentTermId = paymentTerm.Id;
        _supplierId = supplier.Id;
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
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private async Task<Guid> SeedXmlIceDraftAsync(SriTaxCalculationType mode, decimal iceAmount)
    {
        await using var db = CreateContext();
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId, _companyId, _branchId, _supplierId, "Distribuidora Andina S.A.",
            "1791352688001", "01", "001-001-000000301", new DateOnly(2026, 10, 1),
            _createdBy, _paymentTermId, "Contado", 1, 0
        );
        var line = PurchaseInvoiceDetail.Create(inv.Id, _tenantId, "Bebida XML", 1m, 100m, "4", "UNIT", iceCode: "3031");
        line.ReplaceTaxes([
            PurchaseInvoiceDetailTax.Create(line.Id, _tenantId, "3", "3031", "ICE", 0.18m,
                mode, 35.81m, iceAmount, PurchaseTaxSource.Xml),
            PurchaseInvoiceDetailTax.Create(line.Id, _tenantId, "2", "4", "IVA", 12m,
                SriTaxCalculationType.Percentage, 118.55m, 14.23m, PurchaseTaxSource.Xml),
        ]);
        line.ApplyTaxes("4", 15m, "IVA", "3031", 75m, "ICE", mode, iceAmount);
        inv.ReplaceLines([line], _createdBy);
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();
        return inv.Id;
    }

    private async Task<PurchaseInvoice> ReopenAsync(ErpDbContext db, Guid id)
    {
        var repo = new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId));
        return (await repo.GetByIdAsync(_tenantId, id))!;
    }

    private static void AssertXmlSnapshot(PurchaseInvoice inv, SriTaxCalculationType mode, decimal iceAmount)
    {
        var line = inv.Lines.Single();
        var ice = line.Taxes.Single(t => t.TaxCode == "3");
        ice.Source.Should().Be(PurchaseTaxSource.Xml);
        ice.CalculationType.Should().Be(mode);
        ice.Rate.Should().Be(0.18m);
        ice.TaxableBase.Should().Be(35.81m);
        ice.TaxAmount.Should().Be(iceAmount);
        var vat = line.Taxes.Single(t => t.TaxCode == "2");
        vat.Rate.Should().Be(12m);
        vat.TaxableBase.Should().Be(118.55m);
        vat.TaxAmount.Should().Be(14.23m);
    }

    [Theory]
    [InlineData(SriTaxCalculationType.Specific, 0)]
    [InlineData(SriTaxCalculationType.Mixed, 18.55)]
    [InlineData(SriTaxCalculationType.Percentage, 9.87)]
    public async Task Snapshot_XML_se_persiste_reabre_y_sobrevive_al_recalculo(
        SriTaxCalculationType mode, double amount)
    {
        var iceAmount = (decimal)amount;
        var id = await SeedXmlIceDraftAsync(mode, iceAmount);

        await using (var db = CreateContext())
        {
            var inv = await ReopenAsync(db, id);
            AssertXmlSnapshot(inv, mode, iceAmount);
            // Recalcular tras reabrir (mismo camino que Recalculate/Confirm) no altera el snapshot.
            inv.Lines.Single().ApplyTaxes("4", 15m, "IVA", "3031", 75m, "ICE", mode, 999m);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
            AssertXmlSnapshot(await ReopenAsync(db, id), mode, iceAmount);
    }

    [Theory]
    [InlineData(SriTaxCalculationType.Specific)]
    [InlineData(SriTaxCalculationType.Mixed)]
    public async Task Descuento_incompatible_tras_reabrir_se_bloquea_sin_alterar_el_snapshot(
        SriTaxCalculationType mode)
    {
        var id = await SeedXmlIceDraftAsync(mode, 18.55m);

        await using (var db = CreateContext())
        {
            var inv = await ReopenAsync(db, id);
            var act = () => inv.ApplyGlobalDiscount(5m, _createdBy);
            act.Should().Throw<DomainRuleViolationException>();
        }

        await using (var db = CreateContext())
            AssertXmlSnapshot(await ReopenAsync(db, id), mode, 18.55m);
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
