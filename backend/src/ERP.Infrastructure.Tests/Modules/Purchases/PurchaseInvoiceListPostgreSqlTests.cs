using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// FASE 5 PURCHASE-UX — listado de compras (<see cref="PurchaseInvoiceRepository.GetPagedAsync"/>)
/// contra PostgreSQL real: la búsqueda (ILIKE) matchea número, razón social o RUC del proveedor sin
/// distinguir mayúsculas, y los items traen líneas + impuestos para calcular GrandTotal de un
/// borrador. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseInvoiceListPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_list_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _paymentTermId;
    private Guid _createdBy;
    private Guid _andinaSupplierId;
    private Guid _costaSupplierId;

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
        var andina = BusinessPartner.Create(
            tenant.Id,
            TaxIdentification.SriRuc,
            "1791352688001",
            2,
            "Distribuidora Andina S.A.",
            _createdBy
        );
        var costa = BusinessPartner.Create(
            tenant.Id,
            TaxIdentification.SriRuc,
            "0990011223001",
            2,
            "Comercial Costa Cia. Ltda.",
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
        db.BusinessPartners.AddRange(andina, costa);
        db.PaymentTerms.Add(paymentTerm);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _paymentTermId = paymentTerm.Id;
        _andinaSupplierId = andina.Id;
        _costaSupplierId = costa.Id;

        await SeedDraftAsync(_andinaSupplierId, "Distribuidora Andina S.A.", "1791352688001", "001-001-000000101");
        await SeedDraftAsync(_costaSupplierId, "Comercial Costa Cia. Ltda.", "0990011223001", "002-001-000000202");
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

    /// <summary>Borrador con 2 líneas de 1 × 100 + IVA 15% → total 230.</summary>
    private async Task SeedDraftAsync(
        Guid supplierId,
        string supplierName,
        string supplierTaxId,
        string invoiceNumber
    )
    {
        await using var db = CreateContext();
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            supplierId,
            supplierName,
            supplierTaxId,
            "01",
            invoiceNumber,
            new DateOnly(2026, 10, 1),
            _createdBy,
            _paymentTermId,
            "Contado",
            1,
            0
        );
        var lines = new[] { "Línea A", "Línea B" }
            .Select(desc =>
            {
                var line = PurchaseInvoiceDetail.Create(inv.Id, _tenantId, desc, 1m, 100m, "10", "UNIT");
                line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
                return line;
            })
            .ToList();
        inv.ReplaceLines(lines, _createdBy);
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();
    }

    private async Task<(IReadOnlyList<PurchaseInvoice> Items, int Total)> SearchAsync(string? search)
    {
        await using var db = CreateContext();
        var repo = new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId));
        var (items, _, total) = await repo.GetPagedAsync(_tenantId, search, null, 1, 25);
        return (items, total);
    }

    [Theory]
    [InlineData("andina")]
    [InlineData("DISTRIBUIDORA")]
    [InlineData("1791352688")]
    [InlineData("000000101")]
    public async Task Busqueda_por_proveedor_RUC_o_numero_sin_distinguir_mayusculas(string search)
    {
        var (items, total) = await SearchAsync(search);

        total.Should().Be(1);
        items.Single().SupplierId.Should().Be(_andinaSupplierId);
    }

    [Fact]
    public async Task Sin_busqueda_devuelve_todas_con_total_calculado_desde_lineas()
    {
        var (items, total) = await SearchAsync(null);

        total.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().OnlyContain(i => i.GrandTotal == 230m);
        items.Select(i => i.SupplierId).Should().BeEquivalentTo([_andinaSupplierId, _costaSupplierId]);
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
