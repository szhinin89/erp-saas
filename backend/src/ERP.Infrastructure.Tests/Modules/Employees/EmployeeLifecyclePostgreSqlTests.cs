using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.UseCases.DeactivateBusinessPartner;
using ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.EventHandlers;
using ERP.Application.Modules.Employees.UseCases;
using ERP.Domain.Audit;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Events;
using ERP.Domain.Modules.Employees.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Employees;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Employees;

/// <summary>
/// EMP-E5 — baja, reincorporación y asignaciones a sucursales con los handlers reales sobre PostgreSQL 16
/// (Testcontainers; base desechable): ciclo laboral atómico, asignaciones, integridad temporal, aislamiento
/// tenant/empresa (observación B de sucursales), concurrencia con rowVersion, T-05 completo de la
/// reincorporación y auditoría. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class EmployeeLifecyclePostgreSqlTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_employee_lifecycle_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenantA, _companyA1, _companyA2, _tenantB, _companyB1;
    private int _seq;

    private static DateOnly D(int month, int day) => new(2026, month, day);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        await db.Database.MigrateAsync();

        var ta = Tenant.Create("Tenant A", $"ta-{Guid.NewGuid():N}"[..16], _user);
        var tb = Tenant.Create("Tenant B", $"tb-{Guid.NewGuid():N}"[..16], _user);
        var a1 = Company.CreateManaged(ta.Id, "1790012345001", "A1 S.A.", createdBy: _user);
        var a2 = Company.CreateManaged(ta.Id, "1790016919001", "A2 S.A.", createdBy: _user);
        var b1 = Company.CreateManaged(tb.Id, "1790055000001", "B1 S.A.", createdBy: _user);
        db.Tenants.AddRange(ta, tb);
        db.Companies.AddRange(a1, a2, b1);
        await db.SaveChangesAsync();
        (_tenantA, _companyA1, _companyA2, _tenantB, _companyB1) = (ta.Id, a1.Id, a2.Id, tb.Id, b1.Id);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    // ── infraestructura de prueba ────────────────────────────────────────────

    private sealed class DirectAudit(ErpDbContext db) : IAuditService
    {
        public Task RecordAsync<TAudit>(TAudit record, CancellationToken ct = default)
            where TAudit : AuditRecordBase => db.Set<TAudit>().AddAsync(record, ct).AsTask();
    }

    private sealed class AuditPublisher(Func<ErpDbContext> db, Func<IAuditContext> context) : IPublisher
    {
        public Task Publish(object notification, CancellationToken ct = default)
        {
            var handler = new EmployeeAuditHandler(new DirectAudit(db()), context());
            return notification switch
            {
                EmployeeCreatedEvent e => handler.Handle(e, ct),
                EmployeeTerminatedEvent e => handler.Handle(e, ct),
                EmployeeRehiredEvent e => handler.Handle(e, ct),
                EmployeeBranchAssignmentChangedEvent e => handler.Handle(e, ct),
                _ => Task.CompletedTask,
            };
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Publish((object)notification, ct);
    }

    private sealed class NoPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private static ICompanyClock Clock()
    {
        var clock = new Mock<ICompanyClock>();
        clock
            .Setup(c => c.TodayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(D(6, 15));
        return clock.Object;
    }

    private ErpDbContext NewDb(Guid tenant, Guid company, bool audit = false)
    {
        ErpDbContext? holder = null;
        IPublisher publisher = audit
            ? new AuditPublisher(() => holder!, () => new FixedAuditContext(() => tenant, () => company, _user))
            : new NoPublisher();
        holder = new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(() => tenant),
            publisher,
            new FixedCurrentCompany(() => company)
        );
        return holder;
    }

    private static IOperationalContext Ctx(Guid tenant, Guid company, Guid user)
    {
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(company);
        ctx.SetupGet(x => x.UserId).Returns(user);
        return ctx.Object;
    }

    private CreateEmployeeHandler CreateH(ErpDbContext db, Guid t, Guid c) =>
        new(new EmployeeRepository(db), new EmployeeCatalogRepository(db), new EmployeeBusinessPartnerReader(db),
            new BusinessPartnerEmploymentGuard(db), new IdentificationUsageValidator(db), new EmployeeQueries(db, new EmployeeIdentityProjector(new AccessRepository(db))),
            new PostgresDatabaseExceptionTranslator(), Ctx(t, c, _user), new UnitOfWork(db));

    private TerminateEmployeeHandler TerminateH(ErpDbContext db, Guid t, Guid c, IEmployeeRepository? repo = null) =>
        new(repo ?? new EmployeeRepository(db), new EmployeeQueries(db, new EmployeeIdentityProjector(new AccessRepository(db))), Ctx(t, c, _user), new UnitOfWork(db));

    private RehireEmployeeHandler RehireH(ErpDbContext db, Guid t, Guid c, IBusinessPartnerEmploymentGuard? guard = null) =>
        new(new EmployeeRepository(db), new EmployeeBusinessPartnerReader(db),
            guard ?? new BusinessPartnerEmploymentGuard(db), new EmployeeQueries(db, new EmployeeIdentityProjector(new AccessRepository(db))), Ctx(t, c, _user), new UnitOfWork(db));

    private CreateBranchAssignmentHandler AssignH(
        ErpDbContext db, Guid t, Guid c, IEmployeeBranchReader? branches = null, IEmployeeRepository? repo = null) =>
        new(repo ?? new EmployeeRepository(db), branches ?? new EmployeeBranchReader(db), new EmployeeQueries(db, new EmployeeIdentityProjector(new AccessRepository(db))),
            Ctx(t, c, _user), new UnitOfWork(db));

    private CloseBranchAssignmentHandler CloseH(ErpDbContext db, Guid t, Guid c) =>
        new(new EmployeeRepository(db), new EmployeeQueries(db, new EmployeeIdentityProjector(new AccessRepository(db))), Ctx(t, c, _user), new UnitOfWork(db));

    private DeactivateBusinessPartnerHandler DeactivateH(ErpDbContext db, Guid t, Guid c, IBusinessPartnerEmploymentGuard? g = null) =>
        new(new BusinessPartnerRepository(db), Ctx(t, c, _user), g ?? new BusinessPartnerEmploymentGuard(db), new UnitOfWork(db));

    private RevokeBusinessPartnerRoleHandler RevokeH(ErpDbContext db, Guid t, Guid c, IBusinessPartnerEmploymentGuard? g = null) =>
        new(new BusinessPartnerRoleRepository(db), Ctx(t, c, _user), g ?? new BusinessPartnerEmploymentGuard(db), new UnitOfWork(db));

    private async Task<Result<EmployeeDto>> TerminateAsync(
        Guid id, uint version, DateOnly end, string? reason = null, Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await TerminateH(db, t, c).Handle(new TerminateEmployeeCommand(id, version, end, reason), default);
    }

    private async Task<Result<EmployeeDto>> RehireAsync(
        Guid id, uint version, DateOnly start, Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await RehireH(db, t, c).Handle(new RehireEmployeeCommand(id, version, start), default);
    }

    private async Task<Result<EmployeeBranchAssignmentDto>> AssignAsync(
        Guid employee, Guid branch, DateOnly start, DateOnly? end = null,
        Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await AssignH(db, t, c).Handle(new CreateBranchAssignmentCommand(employee, branch, start, end), default);
    }

    private async Task<Result<EmployeeBranchAssignmentDto>> CloseAsync(
        Guid employee, Guid assignment, uint version, DateOnly end,
        Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await CloseH(db, t, c).Handle(new CloseBranchAssignmentCommand(employee, assignment, version, end), default);
    }

    private async Task<EmployeeDto> GetAsync(Guid id, Guid? tenant = null, Guid? company = null)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c);
        var r = await new GetEmployeeByIdHandler(new EmployeeQueries(db, new EmployeeIdentityProjector(new AccessRepository(db))), Ctx(t, c, _user))
            .Handle(new GetEmployeeByIdQuery(id), default);
        r.IsSuccess.Should().BeTrue(r.Error);
        return r.Value!;
    }

    private static string ValidCedula(int seq)
    {
        var digits = $"173{seq % 1_000_000:000000}".Select(ch => ch - '0').ToArray();
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var p = digits[i] * (i % 2 == 0 ? 2 : 1);
            sum += p > 9 ? p - 9 : p;
        }
        return string.Concat(digits) + ((10 - sum % 10) % 10);
    }

    private async Task<(Guid Bp, Guid RoleId)> NewBpAsync(Guid? tenant = null)
    {
        var t = tenant ?? _tenantA;
        await using var db = NewDb(t, Guid.Empty);
        var bp = BusinessPartner.Create(t, "05", ValidCedula(Interlocked.Increment(ref _seq)), null, "Persona ciclo", _user);
        var role = BusinessPartnerRole.Create(t, bp.Id, RoleType.Employee, _user);
        db.BusinessPartners.Add(bp);
        db.BusinessPartnerRoles.Add(role);
        await db.SaveChangesAsync();
        return (bp.Id, role.Id);
    }

    /// <summary>Alta real (handler) de una ficha activa desde <paramref name="start"/>.</summary>
    private async Task<(Guid Employee, Guid Bp, Guid RoleId)> NewEmployeeAsync(
        DateOnly? start = null, Guid? tenant = null, Guid? company = null)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        var (bp, role) = await NewBpAsync(t);
        await using var db = NewDb(t, c);
        var r = await CreateH(db, t, c).Handle(new CreateEmployeeCommand(bp, start ?? D(1, 10)), default);
        r.IsSuccess.Should().BeTrue(r.Error);
        return (r.Value!.Id, bp, role);
    }

    private async Task<Guid> NewBranchAsync(Guid? tenant = null, Guid? company = null, bool active = true)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c);
        var code = $"S{Interlocked.Increment(ref _seq):0000}";
        var branch = Branch.Create(
            t, $"Sucursal {code}", "Av. Principal 123", code,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, isMainBranch: false, _user, companyId: c);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        if (!active)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE branches SET is_active = false WHERE id = {branch.Id}");
        return branch.Id;
    }

    private async Task<T> ScalarAsync<T>(FormattableString sql)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        return await db.Database.SqlQuery<T>(sql).SingleAsync();
    }

    private Task<int> PeriodCountAsync(Guid employee) =>
        ScalarAsync<int>($"SELECT count(*)::int AS \"Value\" FROM employee_employment_periods WHERE employee_id = {employee}");

    private Task<int> AssignmentCountAsync(Guid employee) =>
        ScalarAsync<int>($"SELECT count(*)::int AS \"Value\" FROM employee_branch_assignments WHERE employee_id = {employee}");

    private Task<int> AuditCountAsync(Guid employee) =>
        ScalarAsync<int>($"SELECT count(*)::int AS \"Value\" FROM employee_audit WHERE entity_id = {employee}");

    private async Task<long> AdvisoryCountAsync(int ns, int key, bool granted)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        var c = (long)unchecked((uint)ns);
        var o = (long)unchecked((uint)key);
        return await db.Database.SqlQuery<long>($"""
            SELECT count(*)::bigint AS "Value" FROM pg_locks
             WHERE locktype = 'advisory' AND objsubid = 2 AND granted = {granted}
               AND classid::bigint = {c} AND objid::bigint = {o}
            """).SingleAsync();
    }

    private Task<int> BlockedRowLockWaitersAsync() =>
        ScalarAsync<int>($"""
            SELECT count(*)::int AS "Value" FROM pg_stat_activity
             WHERE datname = current_database() AND wait_event_type = 'Lock'
               AND wait_event IN ('transactionid', 'tuple')
            """);

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("La condición no se cumplió.");
            await Task.Delay(25);
        }
    }

    private static async Task<Result<T>?> RunAsync<T>(Func<Task<Result<T>>> op)
    {
        try { return await op(); }
        catch (DomainRuleViolationException) { return null; }
    }

    // ── A. ciclo laboral ─────────────────────────────────────────────────────

    [Fact]
    public async Task Baja_valida_cierra_el_periodo_y_las_asignaciones_vigentes_de_forma_atomica_con_auditoria()
    {
        var (id, bp, _) = await NewEmployeeAsync(D(1, 10));
        var b1 = await NewBranchAsync();
        var b2 = await NewBranchAsync();
        var openA = (await AssignAsync(id, b1, D(1, 10))).Value!;
        var past = (await AssignAsync(id, b2, D(1, 10), D(2, 28))).Value!;
        var endsLater = (await AssignAsync(id, b2, D(3, 1), D(12, 31))).Value!;
        var before = await GetAsync(id);

        var r = await TerminateAsync(id, before.RowVersion, D(6, 30), "Renuncia voluntaria", audit: true);

        r.IsSuccess.Should().BeTrue(r.Error);
        var dto = r.Value!;
        dto.IsActive.Should().BeFalse();
        dto.Periods.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { StartDate = D(1, 10), EndDate = (DateOnly?)D(6, 30), TerminationReason = "Renuncia voluntaria" });
        dto.Warnings.Should().BeEmpty("ACTIVE_MEMBERSHIP llega con EMP-E6");
        dto.IdentityUser.Should().BeNull();
        var byId = dto.Assignments.ToDictionary(a => a.Id);
        byId[openA.Id].EndDate.Should().Be(D(6, 30), "la vigente abierta se cierra con la fecha de baja");
        byId[endsLater.Id].EndDate.Should().Be(D(6, 30), "la que terminaba después se acorta");
        byId[past.Id].EndDate.Should().Be(D(2, 28), "la ya terminada no se modifica");

        await using var db = NewDb(_tenantA, _companyA1);
        var audits = await db.EmployeeAudits.AsNoTracking().Where(a => a.EntityId == id).ToListAsync();
        audits.Select(a => a.Action).Should().BeEquivalentTo(
            ["Terminated", "BranchAssignmentClosed", "BranchAssignmentClosed"]);
        audits.Single(a => a.Action == "Terminated").Should().BeEquivalentTo(
            new { ChangedField = "EndDate", NewValue = "2026-06-30", Reason = "Renuncia voluntaria", BusinessPartnerId = bp });
    }

    [Fact]
    public async Task Baja_de_ficha_ya_dada_de_baja_fecha_anterior_al_inicio_y_periodo_de_un_dia()
    {
        var (id, _, _) = await NewEmployeeAsync(D(3, 1));
        var v = (await GetAsync(id)).RowVersion;

        var early = await TerminateAsync(id, v, D(2, 28));
        early.Code.Should().Be(ApiResponseCodes.Employees.InvalidDate);
        (await TerminateAsync(id, v, default)).Code.Should().Be(ApiResponseCodes.Employees.InvalidDate);
        (await GetAsync(id)).IsActive.Should().BeTrue("sin cambios parciales");

        var oneDay = await TerminateAsync(id, v, D(3, 1), reason: null);
        oneDay.IsSuccess.Should().BeTrue(oneDay.Error);
        oneDay.Value!.Periods.Single().EndDate.Should().Be(D(3, 1));

        var again = await TerminateAsync(id, oneDay.Value.RowVersion, D(3, 5));
        again.Code.Should().Be(ApiResponseCodes.Employees.AlreadyTerminated);
    }

    [Fact]
    public async Task Baja_con_asignacion_posterior_a_la_fecha_se_bloquea_sin_modificar_nada_ni_auditar()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var b = await NewBranchAsync();
        await AssignAsync(id, b, D(1, 10));
        var future = (await AssignAsync(id, await NewBranchAsync(), D(9, 1), D(9, 30))).Value!;
        var before = await GetAsync(id);
        var auditsBefore = await AuditCountAsync(id);

        var r = await TerminateAsync(id, before.RowVersion, D(6, 30), audit: true);

        r.Code.Should().Be(ApiResponseCodes.Employees.TerminationBlockedByFutureAssignments);
        var after = await GetAsync(id);
        after.Should().BeEquivalentTo(before);
        after.Assignments.Single(a => a.Id == future.Id).StartDate.Should().Be(D(9, 1));
        (await AuditCountAsync(id)).Should().Be(auditsBefore);
    }

    [Fact]
    public async Task Reincorporacion_el_mismo_dia_se_rechaza_el_siguiente_se_acepta_y_conserva_la_ficha_y_el_historial()
    {
        var job = await NewJobAsync();
        var (bp, _) = await NewBpAsync();
        await using (var db = NewDb(_tenantA, _companyA1))
        {
            var created = await CreateH(db, _tenantA, _companyA1).Handle(new CreateEmployeeCommand(bp, D(1, 10), job), default);
            created.IsSuccess.Should().BeTrue(created.Error);
        }
        var id = (await ListEmployeeIdsAsync(bp)).Single();
        var branch = await NewBranchAsync();
        var assignment = (await AssignAsync(id, branch, D(1, 10))).Value!;
        var terminated = (await TerminateAsync(id, (await GetAsync(id)).RowVersion, D(6, 30))).Value!;

        var sameDay = await RehireAsync(id, terminated.RowVersion, D(6, 30));
        sameDay.Code.Should().Be(ApiResponseCodes.Employees.PeriodOverlap);
        (await RehireAsync(id, terminated.RowVersion, D(6, 1))).Code.Should().Be(ApiResponseCodes.Employees.PeriodOverlap);
        (await GetAsync(id)).IsActive.Should().BeFalse();

        var next = await RehireAsync(id, terminated.RowVersion, D(7, 1), audit: true);
        next.IsSuccess.Should().BeTrue(next.Error);
        var dto = next.Value!;
        dto.IsActive.Should().BeTrue();
        dto.Id.Should().Be(id);
        dto.JobPosition!.Id.Should().Be(job, "cargo conservado");
        dto.Periods.Select(p => (p.StartDate, p.EndDate)).Should().Equal((D(1, 10), (DateOnly?)D(6, 30)), (D(7, 1), null));
        dto.Assignments.Should().ContainSingle(a => a.Id == assignment.Id && a.EndDate == D(6, 30), "no reabre asignaciones")
            .And.HaveCount(1, "no asigna sucursales");
        (await ListEmployeeIdsAsync(bp)).Should().ContainSingle("no se crea otra ficha");
        (await PeriodCountAsync(id)).Should().Be(2);

        await using var verify = NewDb(_tenantA, _companyA1);
        (await verify.EmployeeAudits.AsNoTracking().Where(a => a.EntityId == id && a.Action == "Rehired").SingleAsync())
            .Should().BeEquivalentTo(new { ChangedField = "StartDate", NewValue = "2026-07-01" });
    }

    private async Task<Guid> NewJobAsync()
    {
        await using var db = NewDb(_tenantA, _companyA1);
        var entry = JobPosition.Create(_tenantA, _companyA1, $"Cargo {Guid.NewGuid():N}"[..14], _user);
        db.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private async Task<List<Guid>> ListEmployeeIdsAsync(Guid bp) =>
        await Task.FromResult(await ReadIdsAsync(bp));

    private async Task<List<Guid>> ReadIdsAsync(Guid bp)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        return await db.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM employees WHERE business_partner_id = {bp}").ToListAsync();
    }

    [Fact]
    public async Task Reincorporacion_rechaza_ficha_activa_BP_inactivo_y_rol_ausente_sin_cambios()
    {
        var (active, _, _) = await NewEmployeeAsync();
        (await RehireAsync(active, (await GetAsync(active)).RowVersion, D(8, 1))).Code
            .Should().Be(ApiResponseCodes.Employees.AlreadyActive);

        var (a, bpA, _) = await NewEmployeeAsync();
        var (b, _, roleB) = await NewEmployeeAsync();
        var ta = (await TerminateAsync(a, (await GetAsync(a)).RowVersion, D(6, 30))).Value!;
        var tb = (await TerminateAsync(b, (await GetAsync(b)).RowVersion, D(6, 30))).Value!;
        await using (var db = NewDb(_tenantA, _companyA1))
        {
            (await DeactivateH(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bpA), default)).IsSuccess.Should().BeTrue();
        }
        await using (var db = NewDb(_tenantA, _companyA1))
        {
            (await RevokeH(db, _tenantA, _companyA1).Handle(new RevokeBusinessPartnerRoleCommand(await BpOfAsync(b), roleB), default)).IsSuccess.Should().BeTrue();
        }

        (await RehireAsync(a, ta.RowVersion, D(7, 1))).Code.Should().Be(ApiResponseCodes.Employees.BpInactive);
        (await RehireAsync(b, tb.RowVersion, D(7, 1))).Code.Should().Be(ApiResponseCodes.Employees.BpRoleMissing);
        (await GetAsync(a)).IsActive.Should().BeFalse();
        (await PeriodCountAsync(a)).Should().Be(1);
        (await PeriodCountAsync(b)).Should().Be(1);
    }

    private Task<Guid> BpOfAsync(Guid employee) =>
        ScalarAsync<Guid>($"SELECT business_partner_id AS \"Value\" FROM employees WHERE id = {employee}");

    [Fact]
    public async Task Orden_de_validacion_existencia_antes_que_version_y_version_antes_que_reglas()
    {
        var (id, _, _) = await NewEmployeeAsync();
        var v = (await GetAsync(id)).RowVersion;

        // Existencia/pertenencia primero: inexistente o de otro ámbito → EMPLOYEE_NOT_FOUND aunque la versión sea obsoleta.
        (await TerminateAsync(Guid.NewGuid(), 1, D(6, 30))).Code.Should().Be(ApiResponseCodes.Employees.NotFound);
        (await TerminateAsync(id, 1, D(6, 30), company: _companyA2)).Code.Should().Be(ApiResponseCodes.Employees.NotFound);
        (await RehireAsync(id, 1, D(7, 1), tenant: _tenantB, company: _companyB1)).Code.Should().Be(ApiResponseCodes.Employees.NotFound);

        // Versión antes que reglas: obsoleta + fecha inválida → conflicto.
        (await TerminateAsync(id, v + 7, default)).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        var done = (await TerminateAsync(id, v, D(6, 30))).Value!;
        (await TerminateAsync(id, v, D(7, 1))).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict, "obsoleta, aunque ya esté en baja");
        (await RehireAsync(id, v, D(7, 1))).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await RehireAsync(id, done.RowVersion, D(7, 1))).IsSuccess.Should().BeTrue();
    }

    // ── B. asignaciones ──────────────────────────────────────────────────────

    [Fact]
    public async Task Asignacion_valida_devuelve_sucursal_fechas_y_rowVersion_y_admite_sucursales_simultaneas()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var b1 = await NewBranchAsync();
        var b2 = await NewBranchAsync();

        var first = await AssignAsync(id, b1, D(2, 1), D(2, 28), audit: true);
        first.IsSuccess.Should().BeTrue(first.Error);
        first.Value!.Should().BeEquivalentTo(new { BranchId = b1, StartDate = D(2, 1), EndDate = (DateOnly?)D(2, 28) });
        first.Value.BranchName.Should().StartWith("Sucursal");
        first.Value.BranchCode.Should().NotBeNullOrEmpty();
        first.Value.RowVersion.Should().BeGreaterThan(0u);

        (await AssignAsync(id, b2, D(2, 1), D(2, 28))).IsSuccess.Should().BeTrue("varias sucursales simultáneas");
        (await AssignAsync(id, b1, D(3, 1))).IsSuccess.Should().BeTrue("borde inclusivo: empieza el día siguiente al fin");
        (await AssignAsync(id, b1, D(2, 28))).Code.Should().Be(ApiResponseCodes.Employees.AssignmentOverlap, "comparte el día 28");

        await using var db = NewDb(_tenantA, _companyA1);
        var list = await new ListBranchAssignmentsHandler(new EmployeeQueries(db, new EmployeeIdentityProjector(new AccessRepository(db))), Ctx(_tenantA, _companyA1, _user))
            .Handle(new ListBranchAssignmentsQuery(id), default);
        list.Value!.Should().HaveCount(3);
        (await AssignmentCountAsync(id)).Should().Be(3);
    }

    [Fact]
    public async Task Asignacion_con_sucursal_invalida_inactiva_ajena_o_inexistente_da_el_mismo_422_sin_persistir()
    {
        var (id, _, _) = await NewEmployeeAsync();
        var inactive = await NewBranchAsync(active: false);
        var otherCompany = await NewBranchAsync(company: _companyA2);
        var otherTenant = await NewBranchAsync(_tenantB, _companyB1);

        var results = new List<Result<EmployeeBranchAssignmentDto>>();
        foreach (var branch in new[] { inactive, otherCompany, otherTenant, Guid.NewGuid() })
            results.Add(await AssignAsync(id, branch, D(2, 1)));

        results.Should().OnlyContain(r => !r.IsSuccess && r.Code == ApiResponseCodes.Employees.BranchInvalid);
        results.Select(r => r.Error).Distinct().Should().HaveCount(1, "indistinguibles");
        (await AssignmentCountAsync(id)).Should().Be(0);
    }

    [Fact]
    public async Task Asignacion_de_empleado_inactivo_o_de_ficha_ajena_se_rechaza()
    {
        var (id, _, _) = await NewEmployeeAsync();
        var branch = await NewBranchAsync();
        (await TerminateAsync(id, (await GetAsync(id)).RowVersion, D(6, 30))).IsSuccess.Should().BeTrue();

        (await AssignAsync(id, branch, D(2, 1))).Code.Should().Be(ApiResponseCodes.Employees.AlreadyTerminated);

        var (active, _, _) = await NewEmployeeAsync();
        var a2 = await NewBranchAsync(company: _companyA2);
        (await AssignAsync(active, branch, D(2, 1), company: _companyA2)).Code.Should().Be(ApiResponseCodes.Employees.NotFound);
        (await AssignAsync(active, a2, D(2, 1), company: _companyA2)).Code.Should().Be(ApiResponseCodes.Employees.NotFound);
        (await AssignAsync(active, branch, D(2, 1), tenant: _tenantB, company: _companyB1)).Code.Should().Be(ApiResponseCodes.Employees.NotFound);
        (await AssignmentCountAsync(active)).Should().Be(0);
    }

    [Fact]
    public async Task Asignacion_fuera_del_periodo_laboral_se_rechaza_y_la_abierta_exige_periodo_abierto()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();
        (await AssignAsync(id, branch, D(1, 9))).Code.Should().Be(ApiResponseCodes.Employees.AssignmentOutsidePeriod, "antes del inicio");
        (await AssignAsync(id, branch, D(1, 10), D(1, 5))).Code.Should().Be(ApiResponseCodes.Employees.InvalidDate);
        (await AssignAsync(id, branch, default)).Code.Should().Be(ApiResponseCodes.Employees.InvalidDate);

        // Dos períodos: [1/10–6/30] y [8/1–abierto]; el hueco 7/1–7/31 no pertenece a ningún período.
        var t = (await TerminateAsync(id, (await GetAsync(id)).RowVersion, D(6, 30))).Value!;
        (await RehireAsync(id, t.RowVersion, D(8, 1))).IsSuccess.Should().BeTrue();
        (await AssignAsync(id, branch, D(7, 10), D(7, 20))).Code.Should().Be(ApiResponseCodes.Employees.AssignmentOutsidePeriod, "en el hueco");
        (await AssignAsync(id, branch, D(6, 1), D(8, 15))).Code.Should().Be(ApiResponseCodes.Employees.AssignmentOutsidePeriod, "cruza dos períodos");
        (await AssignAsync(id, branch, D(6, 1))).Code.Should().Be(ApiResponseCodes.Employees.AssignmentOutsidePeriod, "abierta que empieza en un período cerrado");

        (await AssignAsync(id, branch, D(2, 1), D(6, 30))).IsSuccess.Should().BeTrue("contenida en el primer período, borde inclusivo");
        (await AssignAsync(id, branch, D(8, 1))).IsSuccess.Should().BeTrue("abierta dentro del período abierto");
        (await AssignmentCountAsync(id)).Should().Be(2, "los rechazos no dejaron filas");
    }

    [Fact]
    public async Task Cierre_valido_asignacion_ya_cerrada_version_obsoleta_y_asignacion_ajena()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var (other, _, _) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();
        var a = (await AssignAsync(id, branch, D(2, 1))).Value!;
        var foreign = (await AssignAsync(other, branch, D(2, 1))).Value!;

        // Existencia y pertenencia primero: la asignación de otra ficha, inexistente o de otro ámbito → NOT_FOUND.
        (await CloseAsync(id, foreign.Id, foreign.RowVersion, D(3, 1))).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await CloseAsync(id, Guid.NewGuid(), 1, D(3, 1))).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await CloseAsync(id, a.Id, a.RowVersion, D(3, 1), company: _companyA2)).Code.Should().Be(ApiResponseCodes.Employees.NotFound);
        // Versión antes que reglas.
        (await CloseAsync(id, a.Id, a.RowVersion + 5, D(1, 1))).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await CloseAsync(id, a.Id, a.RowVersion, D(1, 31))).Code.Should().Be(ApiResponseCodes.Employees.InvalidDate, "anterior al inicio");

        var closed = await CloseAsync(id, a.Id, a.RowVersion, D(3, 1), audit: true);
        closed.IsSuccess.Should().BeTrue(closed.Error);
        closed.Value!.EndDate.Should().Be(D(3, 1));
        closed.Value.RowVersion.Should().NotBe(a.RowVersion);

        // Ya cerrada con versión vigente → violación de regla de dominio (422); con obsoleta → conflicto.
        await FluentActions.Awaiting(() => CloseAsync(id, a.Id, closed.Value.RowVersion, D(3, 5)))
            .Should().ThrowAsync<DomainRuleViolationException>();
        (await CloseAsync(id, a.Id, a.RowVersion, D(3, 5))).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await GetAsync(id)).Assignments.Single(x => x.Id == a.Id).EndDate.Should().Be(D(3, 1));
        (await GetAsync(other)).Assignments.Single().EndDate.Should().BeNull("la ajena no cambió");
    }

    // ── filtro por sucursal (D-E5-4) ─────────────────────────────────────────

    [Fact]
    public async Task Filtro_por_sucursal_usa_la_fecha_de_referencia_con_bordes_inclusivos_y_la_fecha_de_la_empresa_por_defecto()
    {
        var company = await NewCompanyAsync();
        var branch = await NewBranchAsync(company: company);
        var otherBranch = await NewBranchAsync(company: company);
        var (windowed, _, _) = await NewEmployeeAsync(D(1, 1), company: company);
        var (open, _, _) = await NewEmployeeAsync(D(1, 1), company: company);
        var (none, _, _) = await NewEmployeeAsync(D(1, 1), company: company);
        var (elsewhere, _, _) = await NewEmployeeAsync(D(1, 1), company: company);
        (await AssignAsync(windowed, branch, D(3, 1), D(3, 31), company: company)).IsSuccess.Should().BeTrue();
        (await AssignAsync(windowed, branch, D(5, 1), D(5, 31), company: company)).IsSuccess.Should().BeTrue();
        (await AssignAsync(open, branch, D(6, 1), company: company)).IsSuccess.Should().BeTrue();
        (await AssignAsync(elsewhere, otherBranch, D(1, 1), company: company)).IsSuccess.Should().BeTrue();

        async Task<List<Guid>> Ids(ListEmployeesQuery q)
        {
            await using var db = NewDb(_tenantA, company);
            var r = await new ListEmployeesHandler(new EmployeeQueries(db, new EmployeeIdentityProjector(new AccessRepository(db))), Ctx(_tenantA, company, _user), Clock()).Handle(q, default);
            r.IsSuccess.Should().BeTrue(r.Error);
            return r.Value!.Items.Select(i => i.Id).ToList();
        }

        (await Ids(new ListEmployeesQuery(BranchId: branch, AsOf: D(3, 1)))).Should().BeEquivalentTo([windowed], "inicio inclusivo");
        (await Ids(new ListEmployeesQuery(BranchId: branch, AsOf: D(3, 31)))).Should().BeEquivalentTo([windowed], "fin inclusivo");
        (await Ids(new ListEmployeesQuery(BranchId: branch, AsOf: D(2, 28)))).Should().BeEmpty("todavía no iniciada");
        (await Ids(new ListEmployeesQuery(BranchId: branch, AsOf: D(4, 1)))).Should().BeEmpty("vencida");
        (await Ids(new ListEmployeesQuery(BranchId: branch))).Should().BeEquivalentTo([open], "por defecto, hoy de la empresa (15/06)");
        (await Ids(new ListEmployeesQuery(BranchId: branch, AsOf: D(5, 15)))).Should().ContainSingle().Which.Should().Be(windowed);
        (await Ids(new ListEmployeesQuery(BranchId: otherBranch, AsOf: D(6, 15)))).Should().BeEquivalentTo([elsewhere]);
        (await Ids(new ListEmployeesQuery(BranchId: Guid.NewGuid()))).Should().BeEmpty();
        (await Ids(new ListEmployeesQuery())).Should().HaveCount(4, "sin filtro, incluidos los sin asignación");
        _ = none;

        var page = await Ids(new ListEmployeesQuery(BranchId: branch, AsOf: D(6, 15), Skip: 0, Take: 1));
        page.Should().HaveCount(1);

        // Aislamiento: otra empresa del tenant, aunque conozca la sucursal, no ve nada.
        await using var db = NewDb(_tenantA, _companyA1);
        var foreign = await new ListEmployeesHandler(new EmployeeQueries(db, new EmployeeIdentityProjector(new AccessRepository(db))), Ctx(_tenantA, _companyA1, _user), Clock())
            .Handle(new ListEmployeesQuery(BranchId: branch, AsOf: D(6, 15)), default);
        foreign.Value!.TotalCount.Should().Be(0);
    }

    private async Task<Guid> NewCompanyAsync()
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        var n = Interlocked.Increment(ref _seq);
        var company = Company.CreateManaged(_tenantA, $"17910{n:00000}001", $"Aislada {n} S.A.", createdBy: _user);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    // ── C. concurrencia y rowVersion ─────────────────────────────────────────

    [Fact]
    public async Task Dos_bajas_simultaneas_con_la_misma_version_solo_aplican_una()
    {
        for (var round = 0; round < 3; round++)
        {
            var (id, _, _) = await NewEmployeeAsync();
            var v = (await GetAsync(id)).RowVersion;
            var results = await Task.WhenAll(TerminateAsync(id, v, D(6, 30), audit: true), TerminateAsync(id, v, D(6, 30), audit: true));
            results.Count(r => r.IsSuccess).Should().Be(1);
            results.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
            (await GetAsync(id)).Periods.Should().ContainSingle(p => p.EndDate == D(6, 30));
            (await AuditCountAsync(id)).Should().Be(1, "solo audita la baja que prosperó");
        }
    }

    [Fact]
    public async Task Dos_reincorporaciones_simultaneas_dejan_un_unico_periodo_abierto()
    {
        var (id, _, _) = await NewEmployeeAsync();
        var t = (await TerminateAsync(id, (await GetAsync(id)).RowVersion, D(6, 30))).Value!;

        var results = await Task.WhenAll(RehireAsync(id, t.RowVersion, D(7, 1), audit: true), RehireAsync(id, t.RowVersion, D(7, 1), audit: true));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await PeriodCountAsync(id)).Should().Be(2);
        (await GetAsync(id)).Periods.Count(p => p.EndDate is null).Should().Be(1);
        (await AuditCountAsync(id)).Should().Be(1, "solo la reincorporación que prosperó audita (alta y baja sin auditoría activa en la prueba)");
    }

    [Fact]
    public async Task Dos_creaciones_simultaneas_de_la_misma_asignacion_dejan_una_sola()
    {
        var (id, _, _) = await NewEmployeeAsync();
        var branch = await NewBranchAsync();
        var results = await Task.WhenAll(AssignAsync(id, branch, D(2, 1)), AssignAsync(id, branch, D(2, 1)), AssignAsync(id, branch, D(2, 1)));
        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Where(r => !r.IsSuccess).Should().OnlyContain(r => r.Code == ApiResponseCodes.Employees.AssignmentOverlap);
        (await AssignmentCountAsync(id)).Should().Be(1);
    }

    /// <summary>Repositorio que pausa tras adquirir el lock por ficha (espera efectiva del segundo).</summary>
    private sealed class PausingEmployeeRepository(IEmployeeRepository inner) : IEmployeeRepository
    {
        public readonly TaskCompletionSource Reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Proceed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task AcquireEmployeeLockAsync(Guid employeeId, CancellationToken ct = default)
        {
            await inner.AcquireEmployeeLockAsync(employeeId, ct);
            Reached.TrySetResult();
            await Proceed.Task.WaitAsync(Timeout, ct);
        }

        public Task<Employee?> GetByIdAsync(Guid t, Guid c, Guid id, CancellationToken ct = default) => inner.GetByIdAsync(t, c, id, ct);
        public Task<bool> ExistsForBusinessPartnerAsync(Guid t, Guid c, Guid bp, CancellationToken ct = default) => inner.ExistsForBusinessPartnerAsync(t, c, bp, ct);
        public Task<Employee?> GetWithPeriodsAsync(Guid t, Guid c, Guid id, CancellationToken ct = default) => inner.GetWithPeriodsAsync(t, c, id, ct);
        public uint GetRowVersion(Employee employee) => inner.GetRowVersion(employee);
        public Task<IReadOnlyList<EmployeeBranchAssignment>> GetAssignmentsAsync(Guid t, Guid c, Guid e, CancellationToken ct = default) => inner.GetAssignmentsAsync(t, c, e, ct);
        public Task<EmployeeBranchAssignment?> GetAssignmentAsync(Guid t, Guid c, Guid e, Guid a, CancellationToken ct = default) => inner.GetAssignmentAsync(t, c, e, a, ct);
        public Task AddAssignmentAsync(EmployeeBranchAssignment assignment, CancellationToken ct = default) => inner.AddAssignmentAsync(assignment, ct);
        public Task AddPeriodAsync(EmployeeEmploymentPeriod period, CancellationToken ct = default) => inner.AddPeriodAsync(period, ct);
        public uint GetAssignmentRowVersion(EmployeeBranchAssignment assignment) => inner.GetAssignmentRowVersion(assignment);
        public void SetExpectedAssignmentRowVersion(EmployeeBranchAssignment assignment, uint v) => inner.SetExpectedAssignmentRowVersion(assignment, v);
        public Task<bool> IsUserLinkedToOtherEmployeeAsync(Guid t, Guid c, Guid u, Guid e, CancellationToken ct = default) => inner.IsUserLinkedToOtherEmployeeAsync(t, c, u, e, ct);
        public Task AddAsync(Employee employee, CancellationToken ct = default) => inner.AddAsync(employee, ct);
        public void SetExpectedRowVersion(Employee employee, uint v) => inner.SetExpectedRowVersion(employee, v);
        public Task<IReadOnlyList<EmployeeSpecialty>> GetSpecialtyLinksAsync(Guid t, Guid c, Guid e, CancellationToken ct = default) => inner.GetSpecialtyLinksAsync(t, c, e, ct);
        public Task AddSpecialtyLinkAsync(EmployeeSpecialty link, CancellationToken ct = default) => inner.AddSpecialtyLinkAsync(link, ct);
        public Task<bool> SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Baja_en_curso_hace_esperar_a_la_creacion_de_asignacion_que_luego_se_rechaza()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();
        var (ns, key) = EmployeesAdvisoryLocks.EmployeeKey(id);
        var v = (await GetAsync(id)).RowVersion;

        await using var termDb = NewDb(_tenantA, _companyA1);
        var pausing = new PausingEmployeeRepository(new EmployeeRepository(termDb));
        var terminate = TerminateH(termDb, _tenantA, _companyA1, pausing).Handle(new TerminateEmployeeCommand(id, v, D(6, 30), null), default);
        await pausing.Reached.Task.WaitAsync(Timeout);

        var assign = AssignAsync(id, branch, D(2, 1));
        await WaitUntilAsync(async () => await AdvisoryCountAsync(ns, key, granted: false) == 1);
        assign.IsCompleted.Should().BeFalse();

        pausing.Proceed.SetResult();
        (await terminate.WaitAsync(Timeout)).IsSuccess.Should().BeTrue();
        (await assign.WaitAsync(Timeout)).Code.Should().Be(ApiResponseCodes.Employees.AlreadyTerminated, "revalida tras el lock");
        (await AssignmentCountAsync(id)).Should().Be(0);
    }

    [Fact]
    public async Task Baja_y_operaciones_de_asignacion_concurrentes_nunca_dejan_asignaciones_incoherentes()
    {
        for (var round = 0; round < 4; round++)
        {
            var (id, _, _) = await NewEmployeeAsync(D(1, 10));
            var branch = await NewBranchAsync();
            var existing = (await AssignAsync(id, await NewBranchAsync(), D(1, 10))).Value!;
            var v = (await GetAsync(id)).RowVersion;

            var tasks = new Task[]
            {
                RunAsync(() => TerminateAsync(id, v, D(6, 30))),
                RunAsync(() => AssignAsync(id, branch, D(2, 1))),
                RunAsync(() => CloseAsync(id, existing.Id, existing.RowVersion, D(4, 30))),
            };
            await Task.WhenAll(tasks);

            var dto = await GetAsync(id);
            dto.Periods.Should().ContainSingle();
            if (!dto.IsActive)
            {
                dto.Periods.Single().EndDate.Should().Be(D(6, 30));
                dto.Assignments.Should().OnlyContain(a => a.EndDate != null && a.EndDate <= D(6, 30), "INV-09: la baja no deja asignaciones abiertas");
            }
            else
            {
                dto.Periods.Single().EndDate.Should().BeNull();
            }
            (await AssignmentCountAsync(id)).Should().BeInRange(1, 2);
        }
    }

    [Fact]
    public async Task Reincorporacion_y_asignaciones_concurrentes_son_consistentes()
    {
        for (var round = 0; round < 3; round++)
        {
            var (id, _, _) = await NewEmployeeAsync(D(1, 10));
            var branch = await NewBranchAsync();
            var t = (await TerminateAsync(id, (await GetAsync(id)).RowVersion, D(6, 30))).Value!;

            var rehireTask = RehireAsync(id, t.RowVersion, D(7, 1));
            var assignTask = AssignAsync(id, branch, D(8, 1));
            var rehired = await rehireTask;
            var assigned = await assignTask;

            rehired.IsSuccess.Should().BeTrue(rehired.Error);
            var dto = await GetAsync(id);
            dto.IsActive.Should().BeTrue();
            if (assigned.IsSuccess)
                dto.Assignments.Should().ContainSingle(a => a.StartDate == D(8, 1), "solo dentro del período reabierto");
            else
            {
                assigned.Code.Should().Be(ApiResponseCodes.Employees.AlreadyTerminated, "la asignación llegó antes de la reincorporación");
                dto.Assignments.Should().BeEmpty();
            }
        }
    }

    [Fact]
    public async Task Sin_auditoria_de_exito_ante_conflictos_y_rechazos_de_ficha_y_asignacion()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();
        var a = (await AssignAsync(id, branch, D(2, 1), audit: true)).Value!;
        var v = (await GetAsync(id)).RowVersion;
        var baseline = await AuditCountAsync(id);

        (await TerminateAsync(id, v + 3, D(6, 30), audit: true)).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await CloseAsync(id, a.Id, a.RowVersion + 3, D(3, 1), audit: true)).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await AssignAsync(id, Guid.NewGuid(), D(2, 1), audit: true)).Code.Should().Be(ApiResponseCodes.Employees.BranchInvalid);
        (await AssignAsync(id, branch, D(2, 15), audit: true)).Code.Should().Be(ApiResponseCodes.Employees.AssignmentOverlap);
        (await RehireAsync(id, v, D(8, 1), audit: true)).Code.Should().Be(ApiResponseCodes.Employees.AlreadyActive);

        (await AuditCountAsync(id)).Should().Be(baseline);
        (await GetAsync(id)).RowVersion.Should().Be(v);
    }

    // ── asignación vs desactivación de sucursal ──────────────────────────────

    private sealed class PausingBranchReader(IEmployeeBranchReader inner) : IEmployeeBranchReader
    {
        public readonly TaskCompletionSource Reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Proceed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> TryLockActiveAsync(Guid t, Guid c, Guid b, CancellationToken ct = default)
        {
            var r = await inner.TryLockActiveAsync(t, c, b, ct);
            Reached.TrySetResult();
            await Proceed.Task.WaitAsync(Timeout, ct);
            return r;
        }
    }

    [Fact]
    public async Task Asignacion_en_curso_hace_esperar_a_la_desactivacion_de_la_sucursal_y_conserva_el_historial()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();

        await using var assignDb = NewDb(_tenantA, _companyA1);
        var pausing = new PausingBranchReader(new EmployeeBranchReader(assignDb));
        var assign = AssignH(assignDb, _tenantA, _companyA1, pausing).Handle(new CreateBranchAssignmentCommand(id, branch, D(2, 1), null), default);
        await pausing.Reached.Task.WaitAsync(Timeout);

        await using var deactivateDb = NewDb(Guid.Empty, Guid.Empty);
        var deactivate = deactivateDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE branches SET is_active = false WHERE id = {branch}");
        await WaitUntilAsync(async () => await BlockedRowLockWaitersAsync() >= 1);
        deactivate.IsCompleted.Should().BeFalse("la sucursal está bloqueada FOR SHARE por la asignación");

        pausing.Proceed.SetResult();
        (await assign.WaitAsync(Timeout)).IsSuccess.Should().BeTrue();
        await deactivate.WaitAsync(Timeout);

        (await GetAsync(id)).Assignments.Should().ContainSingle(a => a.BranchId == branch, "la desactivación posterior conserva el historial");
        (await AssignAsync(id, branch, D(3, 1), D(3, 5))).Code.Should().Be(ApiResponseCodes.Employees.BranchInvalid, "nuevas asignaciones a inactiva se rechazan");
    }

    [Fact]
    public async Task Desactivacion_de_sucursal_en_curso_hace_esperar_a_la_asignacion_que_luego_se_rechaza()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();

        await using var deactivateDb = NewDb(Guid.Empty, Guid.Empty);
        await using var tx = await deactivateDb.Database.BeginTransactionAsync();
        await deactivateDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE branches SET is_active = false WHERE id = {branch}");

        var assign = AssignAsync(id, branch, D(2, 1));
        await WaitUntilAsync(async () => await BlockedRowLockWaitersAsync() >= 1);
        assign.IsCompleted.Should().BeFalse();

        await tx.CommitAsync();
        (await assign.WaitAsync(Timeout)).Code.Should().Be(ApiResponseCodes.Employees.BranchInvalid, "re-evalúa el estado confirmado");
        (await AssignmentCountAsync(id)).Should().Be(0);
    }

    // ── D. T-05 completo: reincorporación real contra desactivar / revocar ───

    private sealed class PausingGuard(IBusinessPartnerEmploymentGuard inner, bool pauseAfterLock) : IBusinessPartnerEmploymentGuard
    {
        public readonly TaskCompletionSource Reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Proceed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task AcquireBpEmploymentLockAsync(Guid t, Guid b, CancellationToken ct = default)
        {
            await inner.AcquireBpEmploymentLockAsync(t, b, ct);
            if (pauseAfterLock)
            {
                Reached.TrySetResult();
                await Proceed.Task.WaitAsync(Timeout, ct);
            }
        }

        public async Task<bool> HasActiveEmploymentAsync(Guid t, Guid b, CancellationToken ct = default)
        {
            var r = await inner.HasActiveEmploymentAsync(t, b, ct);
            if (!pauseAfterLock)
            {
                Reached.TrySetResult();
                await Proceed.Task.WaitAsync(Timeout, ct);
            }
            return r;
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task T05_reincorporacion_real_contra_desactivar_o_revocar_en_ambos_ordenes(bool revoke, bool rehireFirst)
    {
        var (id, bp, roleId) = await NewEmployeeAsync();
        var terminated = (await TerminateAsync(id, (await GetAsync(id)).RowVersion, D(6, 30))).Value!;
        var (ns, key) = EmployeesAdvisoryLocks.BpEmploymentKey(_tenantA, bp);

        await using var rehireDb = NewDb(_tenantA, _companyA1);
        await using var otherDb = NewDb(_tenantA, _companyA1);
        var guard = new PausingGuard(
            rehireFirst ? new BusinessPartnerEmploymentGuard(rehireDb) : new BusinessPartnerEmploymentGuard(otherDb),
            pauseAfterLock: rehireFirst);

        Task<Result<EmployeeDto>> rehire;
        Task<Result<bool>> block;
        if (rehireFirst)
        {
            rehire = RehireH(rehireDb, _tenantA, _companyA1, guard).Handle(new RehireEmployeeCommand(id, terminated.RowVersion, D(7, 1)), default);
            await guard.Reached.Task.WaitAsync(Timeout);
            block = revoke
                ? RevokeH(otherDb, _tenantA, _companyA1).Handle(new RevokeBusinessPartnerRoleCommand(bp, roleId), default)
                : DeactivateH(otherDb, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), default);
        }
        else
        {
            block = revoke
                ? RevokeH(otherDb, _tenantA, _companyA1, guard).Handle(new RevokeBusinessPartnerRoleCommand(bp, roleId), default)
                : DeactivateH(otherDb, _tenantA, _companyA1, guard).Handle(new DeactivateBusinessPartnerCommand(bp), default);
            await guard.Reached.Task.WaitAsync(Timeout);
            rehire = RehireH(rehireDb, _tenantA, _companyA1).Handle(new RehireEmployeeCommand(id, terminated.RowVersion, D(7, 1)), default);
        }

        // El segundo espera de verdad el lock BpEmployment del primero (pg_locks).
        await WaitUntilAsync(async () => await AdvisoryCountAsync(ns, key, granted: false) == 1);
        (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(1);
        (rehireFirst ? (Task)block : rehire).IsCompleted.Should().BeFalse();

        guard.Proceed.SetResult();
        var rehireResult = await rehire.WaitAsync(Timeout);
        var blockResult = await block.WaitAsync(Timeout);

        (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(0);
        await using var verify = NewDb(_tenantA, _companyA1);
        var bpRow = await verify.BusinessPartners.AsNoTracking().SingleAsync(b => b.Id == bp);
        var roleRow = await verify.BusinessPartnerRoles.AsNoTracking().SingleAsync(r => r.Id == roleId);
        var employee = await GetAsync(id);

        if (rehireFirst)
        {
            rehireResult.IsSuccess.Should().BeTrue(rehireResult.Error);
            blockResult.Code.Should().Be(ApiResponseCodes.MasterData.BpHasActiveEmployment);
            (bpRow.IsActive, roleRow.IsActive, employee.IsActive).Should().Be((true, true, true));
            (await PeriodCountAsync(id)).Should().Be(2);
        }
        else
        {
            blockResult.IsSuccess.Should().BeTrue(blockResult.Error);
            rehireResult.Code.Should().Be(revoke ? ApiResponseCodes.Employees.BpRoleMissing : ApiResponseCodes.Employees.BpInactive,
                "la reincorporación revalida el estado confirmado DESPUÉS del lock");
            (revoke ? !roleRow.IsActive : !bpRow.IsActive).Should().BeTrue();
            employee.IsActive.Should().BeFalse("sin ficha activa con BP inactivo o sin rol");
            (await PeriodCountAsync(id)).Should().Be(1, "sin período parcial");
            employee.RowVersion.Should().Be(terminated.RowVersion, "sin escritura");
        }
    }

    // ── E. integridad temporal: defensa física de PostgreSQL ─────────────────

    private async Task<string?> SqlStateAsync(Func<ErpDbContext, Task> action)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        try
        {
            await action(db);
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetBaseException() is PostgresException pg ? pg.SqlState : throw ex;
        }
    }

    private Task<string?> InsertPeriodAsync(Guid employee, DateOnly start, DateOnly? end) =>
        SqlStateAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO employee_employment_periods (id, tenant_id, company_id, created_at, created_by, employee_id, start_date, end_date)
            VALUES ({Guid.NewGuid()}, {_tenantA}, {_companyA1}, now(), {_user}, {employee}, {start}, {end})
            """));

    private Task<string?> InsertAssignmentAsync(Guid employee, Guid branch, DateOnly start, DateOnly? end) =>
        SqlStateAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO employee_branch_assignments (id, tenant_id, company_id, created_at, created_by, employee_id, branch_id, start_date, end_date)
            VALUES ({Guid.NewGuid()}, {_tenantA}, {_companyA1}, now(), {_user}, {employee}, {branch}, {start}, {end})
            """));

    [Fact]
    public async Task Las_restricciones_fisicas_de_periodos_defienden_incluso_sin_las_validaciones_de_aplicacion()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var t = (await TerminateAsync(id, (await GetAsync(id)).RowVersion, D(6, 30))).Value!;
        (await RehireAsync(id, t.RowVersion, D(7, 1))).IsSuccess.Should().BeTrue();

        (await InsertPeriodAsync(id, D(9, 1), null)).Should().Be("23505", "UX-PER-01: un solo período abierto");
        (await InsertPeriodAsync(id, D(3, 1), D(3, 10))).Should().Be("23P01", "EX-PER-01: solapa con un período registrado");
        (await InsertPeriodAsync(id, D(6, 30), D(6, 30))).Should().Be("23P01", "INV-13: mismo día de la baja (rango cerrado)");
        (await InsertPeriodAsync(id, D(5, 5), D(5, 1))).Should().Be("23514", "CK: fin anterior al inicio");
        (await PeriodCountAsync(id)).Should().Be(2);
    }

    [Fact]
    public async Task Las_restricciones_fisicas_de_asignaciones_defienden_incluso_sin_las_validaciones_de_aplicacion()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var b1 = await NewBranchAsync();
        var b2 = await NewBranchAsync();
        (await AssignAsync(id, b1, D(2, 1), D(2, 28))).IsSuccess.Should().BeTrue();

        (await InsertAssignmentAsync(id, b1, D(2, 28), D(3, 5))).Should().Be("23P01", "EX-ASG-01: misma sucursal, día compartido");
        (await InsertAssignmentAsync(id, b2, D(2, 10), D(2, 20))).Should().BeNull("otra sucursal no se solapa (varias simultáneas)");
        (await InsertAssignmentAsync(id, b1, D(4, 5), D(4, 1))).Should().Be("23514", "CK: fin anterior al inicio");
    }

    // ── T-11 / T-08 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task T11_cada_operacion_del_ciclo_deja_su_registro_oficial_de_auditoria()
    {
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();
        var a = (await AssignAsync(id, branch, D(2, 1), audit: true)).Value!;
        (await CloseAsync(id, a.Id, a.RowVersion, D(3, 1), audit: true)).IsSuccess.Should().BeTrue();
        var t = (await TerminateAsync(id, (await GetAsync(id)).RowVersion, D(6, 30), audit: true)).Value!;
        (await RehireAsync(id, t.RowVersion, D(7, 1), audit: true)).IsSuccess.Should().BeTrue();

        await using var db = NewDb(_tenantA, _companyA1);
        var rows = await db.EmployeeAudits.AsNoTracking().Where(r => r.EntityId == id).OrderBy(r => r.OccurredAtUtc).ToListAsync();
        rows.Select(r => r.Action).Should().Equal("BranchAssigned", "BranchAssignmentClosed", "Terminated", "Rehired");
        rows[0].NewValue.Should().Be($"{branch} 2026-02-01/");
        rows[1].OldValue.Should().Be($"{branch} 2026-02-01/");
        rows[1].NewValue.Should().Be($"{branch} 2026-02-01/2026-03-01");
        rows.Should().OnlyContain(r => r.CompanyId == _companyA1);
    }

    [Fact]
    public async Task T08_asignar_dar_de_baja_y_reincorporar_no_tocan_membresias_ni_CompanyUserBranch()
    {
        string[] tables = ["company_user_memberships", "company_user_branches", "access_profile_permissions"];
        async Task<long> Snapshot()
        {
            long total = 0;
            foreach (var table in tables)
            {
                await using var db = NewDb(Guid.Empty, Guid.Empty);
                total += await db.Database.SqlQueryRaw<long>($"SELECT count(*)::bigint AS \"Value\" FROM {table}").SingleAsync();
            }
            return total;
        }

        var before = await Snapshot();
        var (id, _, _) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();
        (await AssignAsync(id, branch, D(2, 1))).IsSuccess.Should().BeTrue();
        var t = (await TerminateAsync(id, (await GetAsync(id)).RowVersion, D(6, 30))).Value!;
        (await RehireAsync(id, t.RowVersion, D(7, 1))).IsSuccess.Should().BeTrue();

        (await Snapshot()).Should().Be(before);
    }
}
