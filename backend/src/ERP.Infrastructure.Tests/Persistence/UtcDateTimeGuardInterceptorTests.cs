using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ZH-DATETIME-UTC-GUARDRAILS-01: suite de integración (PostgreSQL real vía Testcontainers)
/// para UtcDateTimeGuardInterceptor — el guard global que impide que un DateTime con Kind
/// Unspecified/Local llegue a SaveChanges (y por lo tanto a una columna timestamptz).
/// </summary>
public sealed class UtcDateTimeGuardInterceptorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_utc_guard_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _emissionPointId;
    private Guid _cashRegisterId;
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
        var branch = Branch.Create(
            tenantId: tenant.Id,
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
            companyId: company.Id
        );
        var establishment = Establishment.Create(
            tenant.Id,
            null,
            company.Id,
            "001",
            "Matriz",
            "Av. Principal 123",
            null,
            true,
            _userId
        );
        var emissionPoint = EmissionPoint.Create(
            tenant.Id,
            company.Id,
            establishment.Id,
            "001",
            null,
            ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            true,
            _userId
        );
        var cashRegister = CashRegister.Create(
            tenant.Id,
            company.Id,
            branch.Id,
            "CAJA-01",
            "Caja Principal",
            _userId,
            emissionPoint.Id
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.Establishments.Add(establishment);
        db.EmissionPoints.Add(emissionPoint);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _emissionPointId = emissionPoint.Id;
        _cashRegisterId = cashRegister.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new UtcDateTimeGuardInterceptor())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    [Fact]
    public async Task SaveChanges_WithUtcKindDateTime_Succeeds()
    {
        await using var db = CreateContext();

        var session = CashSession.Open(
            _tenantId,
            _companyId,
            _branchId,
            _userId,
            _cashRegisterId,
            "CAJA-01",
            "Caja Principal",
            _emissionPointId,
            "001",
            100m,
            _userId
        );

        db.CashSessions.Add(session);

        var act = () => db.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveChanges_WithUnspecifiedKindDateTime_ThrowsUnspecifiedDateTimeKindException()
    {
        await using var db = CreateContext();

        var session = CashSession.Open(
            _tenantId,
            _companyId,
            _branchId,
            _userId,
            _cashRegisterId,
            "CAJA-01",
            "Caja Principal",
            _emissionPointId,
            "001",
            100m,
            _userId
        );

        db.CashSessions.Add(session);
        db.Entry(session).Property(nameof(CashSession.OpenedAt)).CurrentValue = DateTime.SpecifyKind(
            DateTime.UtcNow,
            DateTimeKind.Unspecified
        );

        var act = () => db.SaveChangesAsync();
        var exception = await act.Should().ThrowAsync<UnspecifiedDateTimeKindException>();
        exception.WithMessage("*CashSession.OpenedAt*");
        exception.WithMessage("*Unspecified*");
    }

    [Fact]
    public async Task SaveChanges_WithLocalKindDateTime_ThrowsUnspecifiedDateTimeKindException()
    {
        await using var db = CreateContext();

        var session = CashSession.Open(
            _tenantId,
            _companyId,
            _branchId,
            _userId,
            _cashRegisterId,
            "CAJA-01",
            "Caja Principal",
            _emissionPointId,
            "001",
            100m,
            _userId
        );

        db.CashSessions.Add(session);
        db.Entry(session).Property(nameof(CashSession.OpenedAt)).CurrentValue = DateTime.SpecifyKind(
            DateTime.UtcNow,
            DateTimeKind.Local
        );

        var act = () => db.SaveChangesAsync();
        var exception = await act.Should().ThrowAsync<UnspecifiedDateTimeKindException>();
        exception.WithMessage("*CashSession.OpenedAt*");
        exception.WithMessage("*Local*");
    }

    // ── Helpers de identidad para el DbContext ───────────────────────────────

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

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
