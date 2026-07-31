using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
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
/// Suite de integración (PostgreSQL real vía Testcontainers) para
/// NewChildEntityTrackingInterceptor — cubre el escenario original del bug
/// (DbUpdateConcurrencyException por hijos nuevos descubiertos vía fixup de
/// navegación) y los escenarios adversariales exigidos: múltiples hijos nuevos,
/// dos agregados a la vez, entidades existentes genuinamente modificadas,
/// ejecución repetida, y la combinación anómala que debe fallar fuerte en vez
/// de adivinar.
///
/// Usa PostgreSQL real (no InMemory) porque CashSession tiene un concurrency
/// token `xmin`, que el provider InMemory no soporta.
/// </summary>
public sealed class NewChildEntityTrackingInterceptorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_interceptor_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _emissionPointId;
    private Guid _cashRegisterId;
    private Guid _cashRegisterId2;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _userId2 = Guid.NewGuid();

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
            EmissionType.Electronic,
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
        // Segunda caja registradora — únicamente para el escenario "dos agregados a la vez",
        // que necesita dos CashSession Open simultáneas. Reutilizar la misma caja/usuario para
        // ambas violaría ahora ux_cash_sessions_open_per_register/ux_cash_sessions_open_per_user
        // (P1-01, ERP_CORE_SUMAK_READINESS_AUDIT.md) — correctamente, es el mismo invariante que
        // esos índices existen para proteger.
        var cashRegister2 = CashRegister.Create(
            tenant.Id,
            company.Id,
            branch.Id,
            "CAJA-02",
            "Caja Secundaria",
            _userId,
            emissionPoint.Id
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.Establishments.Add(establishment);
        db.EmissionPoints.Add(emissionPoint);
        db.CashRegisters.Add(cashRegister);
        db.CashRegisters.Add(cashRegister2);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _emissionPointId = emissionPoint.Id;
        _cashRegisterId = cashRegister.Id;
        _cashRegisterId2 = cashRegister2.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new NewChildEntityTrackingInterceptor())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private async Task<Guid> OpenSessionAsync(Guid? cashRegisterId = null, Guid? userId = null)
    {
        var registerId = cashRegisterId ?? _cashRegisterId;
        var openedBy = userId ?? _userId;

        await using var db = CreateContext();
        var session = CashSession.Open(
            _tenantId,
            _companyId,
            _branchId,
            openedBy,
            registerId,
            registerId == _cashRegisterId2 ? "CAJA-02" : "CAJA-01",
            registerId == _cashRegisterId2 ? "Caja Secundaria" : "Caja Principal",
            _emissionPointId,
            "001",
            100m,
            openedBy
        );
        db.CashSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    // ── Escenario original del bug ───────────────────────────────────────────

    [Fact]
    public async Task Original_bug_scenario_RecordMovement_on_query_loaded_session_does_not_throw()
    {
        var sessionId = await OpenSessionAsync();

        await using var db = CreateContext();
        var session = await db
            .CashSessions.Include(s => s.Movements)
            .FirstAsync(s => s.Id == sessionId);

        session.RecordMovement(CashMovementType.SaleIncome, 25m, "Venta 001", _userId);

        var act = () => db.SaveChangesAsync();
        await act.Should().NotThrowAsync();

        var persisted = await db
            .CashMovements.Where(m =>
                m.CashSessionId == sessionId && m.MovementType == CashMovementType.SaleIncome
            )
            .ToListAsync();
        persisted.Should().ContainSingle(m => m.Amount == 25m);
    }

    // ── Múltiples hijos nuevos en un solo SaveChanges ────────────────────────

    [Fact]
    public async Task Multiple_new_children_in_one_SaveChanges_are_all_persisted()
    {
        var sessionId = await OpenSessionAsync();

        await using var db = CreateContext();
        var session = await db
            .CashSessions.Include(s => s.Movements)
            .FirstAsync(s => s.Id == sessionId);

        session.RecordMovement(CashMovementType.SaleIncome, 10m, "Venta A", _userId);
        session.RecordMovement(CashMovementType.SaleIncome, 20m, "Venta B", _userId);
        session.RecordMovement(CashMovementType.ManualExpense, 5m, "Retiro", _userId);

        await db.SaveChangesAsync();

        var count = await db.CashMovements.CountAsync(m => m.CashSessionId == sessionId);
        count.Should().Be(4); // 1 Opening (del Open original) + 3 nuevos
    }

    // ── Dos agregados modificados simultáneamente ────────────────────────────

    [Fact]
    public async Task Two_aggregates_with_new_children_in_the_same_SaveChanges_both_succeed()
    {
        var sessionAId = await OpenSessionAsync();
        var sessionBId = await OpenSessionAsync(_cashRegisterId2, _userId2);

        await using var db = CreateContext();
        var sessions = await db
            .CashSessions.Include(s => s.Movements)
            .Where(s => s.Id == sessionAId || s.Id == sessionBId)
            .ToListAsync();

        foreach (var s in sessions)
            s.RecordMovement(CashMovementType.SaleIncome, 15m, "Venta", _userId);

        await db.SaveChangesAsync();

        (await db.CashMovements.CountAsync(m => m.CashSessionId == sessionAId)).Should().Be(2);
        (await db.CashMovements.CountAsync(m => m.CashSessionId == sessionBId)).Should().Be(2);
    }

    // ── Entidad existente genuinamente modificada — no debe ser tratada como nueva ──

    [Fact]
    public async Task Genuinely_modified_existing_entity_is_not_misclassified_as_Added()
    {
        var sessionId = await OpenSessionAsync();

        await using var db = CreateContext();
        var session = await db
            .CashSessions.Include(s => s.Movements)
            .FirstAsync(s => s.Id == sessionId);

        session.Close(_userId, new List<CashClosingCount>());

        await db.SaveChangesAsync();

        await using var verify = CreateContext();
        var reloaded = await verify.CashSessions.FirstAsync(s => s.Id == sessionId);
        reloaded.Status.Should().Be(CashSessionStatus.Closed);

        // Una sola fila — Close() nunca debió producir un INSERT duplicado.
        (await verify.CashSessions.CountAsync(s => s.Id == sessionId))
            .Should()
            .Be(1);
    }

    // ── Ejecución repetida ────────────────────────────────────────────────────

    [Fact]
    public async Task Repeated_SaveChanges_calls_in_the_same_context_keep_working()
    {
        var sessionId = await OpenSessionAsync();

        await using var db = CreateContext();
        var session = await db
            .CashSessions.Include(s => s.Movements)
            .FirstAsync(s => s.Id == sessionId);

        session.RecordMovement(CashMovementType.SaleIncome, 1m, "Venta 1", _userId);
        await db.SaveChangesAsync();

        session.RecordMovement(CashMovementType.SaleIncome, 2m, "Venta 2", _userId);
        await db.SaveChangesAsync();

        session.RecordMovement(CashMovementType.SaleIncome, 3m, "Venta 3", _userId);
        await db.SaveChangesAsync();

        var count = await db.CashMovements.CountAsync(m => m.CashSessionId == sessionId);
        count.Should().Be(4); // 1 Opening + 3 ventas, repartidas en 3 SaveChanges separados
    }

    // ── Combinación anómala: query-tracked pero Modified sin diff real ───────

    [Fact]
    public async Task Query_tracked_entity_forced_Modified_with_zero_real_diff_throws_instead_of_guessing()
    {
        var sessionId = await OpenSessionAsync();

        await using var db = CreateContext();
        var session = await db
            .CashSessions.Include(s => s.Movements)
            .FirstAsync(s => s.Id == sessionId);

        // Simula la firma anómala que el interceptor no debe resolver por adivinanza:
        // una entidad que SÍ vino de una query queda Modified sin ninguna diferencia real.
        db.Entry(session).State = EntityState.Modified;

        var act = () => db.SaveChangesAsync();
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*fue cargada por una query*");
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
