using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Caja;

/// <summary>
/// R3 (2026-10-28) — los turnos de caja son independientes por TenantId + CompanyId: un usuario con
/// un turno abierto en la empresa A puede abrir otro en la empresa B del mismo tenant, pero nunca dos
/// en la misma empresa. El índice único parcial <c>ux_cash_sessions_open_per_company_user</c> es la
/// defensa atómica de esa regla (la validación previa del caso de uso ya es por empresa).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CashSessionOpenUniquenessTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_cash_session_open_uniqueness_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _createdBy = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private Guid _tenantId;
    private CashContext _companyA = null!;
    private CashContext _companyB = null!;

    private sealed record CashContext(
        Guid CompanyId,
        Guid BranchId,
        Guid RegisterId,
        Guid SecondRegisterId,
        Guid EmissionPointId
    );

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = ERP.Domain.Tenants.Entities.Tenant.Create(
            "Test Tenant",
            $"test-{Guid.NewGuid():N}"[..16],
            _createdBy
        );
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;

        _companyA = await SeedCompanyAsync(db, "1790012345001", "Empresa A S.A.");
        _companyB = await SeedCompanyAsync(db, "1790016919001", "Empresa B S.A.");
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Mismo_usuario_puede_tener_un_turno_abierto_en_cada_empresa_del_tenant()
    {
        await using (var db = CreateContext())
        {
            db.CashSessions.Add(OpenSession(_companyA, _companyA.RegisterId));
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            db.CashSessions.Add(OpenSession(_companyB, _companyB.RegisterId));
            await db.SaveChangesAsync();
        }

        await using var verify = CreateContext();
        var open = await verify
            .CashSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == _userId && s.Status == ERP.Domain.Modules.Caja.Enums.CashSessionStatus.Open)
            .Select(s => s.CompanyId)
            .ToListAsync();
        open.Should().BeEquivalentTo(new[] { _companyA.CompanyId, _companyB.CompanyId });
    }

    [Fact]
    public async Task Mismo_usuario_no_puede_tener_dos_turnos_abiertos_en_la_misma_empresa()
    {
        await using (var db = CreateContext())
        {
            db.CashSessions.Add(OpenSession(_companyA, _companyA.RegisterId));
            await db.SaveChangesAsync();
        }

        await using var second = CreateContext();
        second.CashSessions.Add(OpenSession(_companyA, _companyA.SecondRegisterId));
        var act = () => second.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should()
            .BeOfType<PostgresException>()
            .Which.ConstraintName.Should()
            .Be("ux_cash_sessions_open_per_company_user");
    }

    private CashSession OpenSession(CashContext ctx, Guid registerId) =>
        CashSession.Open(
            _tenantId,
            ctx.CompanyId,
            ctx.BranchId,
            _userId,
            registerId,
            "CAJA",
            "Caja",
            ctx.EmissionPointId,
            "001",
            0m,
            _createdBy
        );

    private async Task<CashContext> SeedCompanyAsync(ErpDbContext db, string ruc, string name)
    {
        var company = Company.CreateManaged(_tenantId, ruc, name, createdBy: _createdBy);
        var branch = Branch.Create(
            _tenantId,
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
            isMainBranch: true,
            Guid.NewGuid(),
            companyId: company.Id
        );
        db.Companies.Add(company);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var establishment = Establishment.Create(
            _tenantId,
            branchId: branch.Id,
            company.Id,
            code: "001",
            name: "Matriz",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        var register = CashRegister.Create(_tenantId, company.Id, branch.Id, "CAJA-1", "Caja 1", _createdBy);
        var secondRegister = CashRegister.Create(_tenantId, company.Id, branch.Id, "CAJA-2", "Caja 2", _createdBy);
        db.Establishments.Add(establishment);
        db.CashRegisters.AddRange(register, secondRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            _tenantId,
            company.Id,
            establishment.Id,
            code: "001",
            name: "PE-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();

        return new CashContext(company.Id, branch.Id, register.Id, secondRegister.Id, emissionPoint.Id);
    }

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(Guid.Empty)
        );
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
