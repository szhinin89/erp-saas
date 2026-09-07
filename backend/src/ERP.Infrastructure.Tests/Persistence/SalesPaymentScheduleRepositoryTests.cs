using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ADR-033, Fase 4 — persistencia del cronograma de pago de Ventas (SalesPaymentSchedule):
/// mapeo EF, recarga vía SalesInvoiceRepository.GetByIdAsync, cascade delete y
/// RemovePaymentSchedulesByInvoiceAsync. Requiere Docker (PostgreSQL 16 real vía Testcontainers).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SalesPaymentScheduleRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_sps_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _customerId;
    private Guid _cashSessionId;
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
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
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
            createdBy: _createdBy,
            companyId: company.Id
        );
        var customer = BusinessPartner.Create(tenant.Id, "05", "1710034065", 1, "Cliente Test", _createdBy);
        var establishment = Establishment.Create(
            tenant.Id, branchId: branch.Id, company.Id, code: "001", name: "Matriz Test",
            address: "Av. Principal 123", phone: null, isMain: true, createdBy: _createdBy
        );
        var cashRegister = CashRegister.Create(
            tenant.Id, company.Id, branch.Id, "CAJA-01", "Caja Principal", _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id, company.Id, establishment.Id, code: "001", name: "PE-001",
            emissionType: ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true, createdBy: _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();

        var cashSession = CashSession.Open(
            tenant.Id, company.Id, branch.Id, _createdBy, cashRegister.Id,
            "CAJA-01", "Caja Principal", emissionPoint.Id, "001", 0m, _createdBy
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );

    private SalesInvoice CreateDraftWithLine(decimal unitPrice = 100m, int installments = 2, int daysBetween = 30)
    {
        var pt = PaymentTermSnapshot.Create(Guid.NewGuid(), "Test", installments, daysBetween);
        var inv = SalesInvoice.CreateDraft(
            _tenantId, _companyId, _branchId, _customerId,
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
            "DRAFT-SPS-TEST", new DateOnly(2026, 1, 1), _createdBy, pt,
            cashSessionId: _cashSessionId
        );
        var line = SalesInvoiceDetail.Create(inv.Id, _tenantId, "Producto Test", 1, unitPrice, "0", "UNIT");
        inv.ReplaceLines(new[] { line }, _createdBy);
        return inv;
    }

    [Fact]
    public async Task Cronograma_persiste_y_se_recarga_en_orden_con_los_valores_exactos()
    {
        var inv = CreateDraftWithLine(unitPrice: 100m, installments: 2, daysBetween: 30);
        inv.ReplacePaymentSchedule(
            new List<(int, DateOnly, decimal, string?)>
            {
                (2, inv.IssueDate.AddDays(60), 40m, "Segunda"),
                (1, inv.IssueDate.AddDays(30), 60m, "Primera"),
            }
        );

        await using (var write = CreateContext())
        {
            write.SalesInvoices.Add(inv);
            await write.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var reloaded = await new SalesInvoiceRepository(read, new FixedCurrentCompany(_companyId))
            .GetByIdAsync(_tenantId, inv.Id);

        reloaded.Should().NotBeNull();
        reloaded!.PaymentSchedules.Should().HaveCount(2);
        reloaded.PaymentSchedules[0].InstallmentNumber.Should().Be(1);
        reloaded.PaymentSchedules[0].Amount.Should().Be(60m);
        reloaded.PaymentSchedules[0].DueDate.Should().Be(inv.IssueDate.AddDays(30));
        reloaded.PaymentSchedules[0].Notes.Should().Be("Primera");
        reloaded.PaymentSchedules[1].InstallmentNumber.Should().Be(2);
        reloaded.PaymentSchedules[1].Amount.Should().Be(40m);
        reloaded.IsPaymentScheduleManual.Should().BeTrue();
    }

    [Fact]
    public async Task RemovePaymentSchedulesByInvoiceAsync_elimina_las_filas_persistidas()
    {
        var inv = CreateDraftWithLine();
        inv.GeneratePaymentSchedule();

        await using (var write = CreateContext())
        {
            write.SalesInvoices.Add(inv);
            await write.SaveChangesAsync();
        }

        await using (var remove = CreateContext())
        {
            var repo = new SalesInvoiceRepository(remove, new FixedCurrentCompany(_companyId));
            await repo.RemovePaymentSchedulesByInvoiceAsync(inv.Id);
        }

        await using var read = CreateContext();
        var remaining = await read
            .Set<SalesPaymentSchedule>()
            .IgnoreQueryFilters()
            .Where(s => s.SalesInvoiceId == inv.Id)
            .CountAsync();

        remaining.Should().Be(0);
    }

    [Fact]
    public async Task Eliminar_la_factura_elimina_en_cascada_su_cronograma()
    {
        var inv = CreateDraftWithLine();
        inv.GeneratePaymentSchedule();

        await using (var write = CreateContext())
        {
            write.SalesInvoices.Add(inv);
            await write.SaveChangesAsync();
        }

        await using (var delete = CreateContext())
        {
            await delete.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM sales_invoices WHERE id = {inv.Id}"
            );
        }

        await using var read = CreateContext();
        var remaining = await read
            .Set<SalesPaymentSchedule>()
            .IgnoreQueryFilters()
            .Where(s => s.SalesInvoiceId == inv.Id)
            .CountAsync();

        remaining.Should().Be(0);
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
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
