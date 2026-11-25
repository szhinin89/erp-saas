using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Access.UseCases.RevokeCompanyUserMembership;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.EventHandlers;
using ERP.Application.Modules.Employees.UseCases;
using ERP.Application.Navigation;
using ERP.Domain.Access.Entities;
using ERP.Domain.Audit;
using ERP.Domain.Kernel.Security;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Employees.Events;
using ERP.Domain.Modules.Employees.Interfaces;
using ERP.Domain.Tenants.Entities;
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
/// EMP-E6 — vinculación y desvinculación de usuarios con los handlers reales sobre PostgreSQL 16
/// (Testcontainers; base desechable): reglas IAM, aislamiento tenant/empresa, unicidad UX-EMP-02,
/// concurrencia (rowVersion, locks, membresías), cuentas globales, ACTIVE_MEMBERSHIP, independencia de
/// Access (T-08) y auditoría. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class EmployeeUserLinkPostgreSqlTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_employee_user_link_test")
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

        // Un administrador propio por empresa: la revocación real de Access exige conservar al menos uno.
        foreach (var company in new[] { _companyA1, _companyA2, _companyB1 })
            await NewUserAsync([company], role: SecurityRoles.Admin);
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
                EmployeeUserLinkChangedEvent e => handler.Handle(e, ct),
                EmployeeTerminatedEvent e => handler.Handle(e, ct),
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

    private static EmployeeQueries Queries(ErpDbContext db) =>
        new(db, new EmployeeIdentityProjector(new AccessRepository(db)));

    private LinkEmployeeUserHandler LinkH(
        ErpDbContext db, Guid t, Guid c, IEmployeeUserReader? users = null, IEmployeeRepository? repo = null) =>
        new(repo ?? new EmployeeRepository(db), users ?? new EmployeeUserReader(db), new AccessRepository(db),
            Queries(db), new PostgresDatabaseExceptionTranslator(), Ctx(t, c, _user), new UnitOfWork(db));

    private UnlinkEmployeeUserHandler UnlinkH(ErpDbContext db, Guid t, Guid c) =>
        new(new EmployeeRepository(db), Queries(db), Ctx(t, c, _user), new UnitOfWork(db));

    private async Task<Result<EmployeeDto>> LinkAsync(
        Guid employee, Guid userId, uint version, Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await LinkH(db, t, c).Handle(new LinkEmployeeUserCommand(employee, userId, version), default);
    }

    private async Task<Result<EmployeeDto>> UnlinkAsync(
        Guid employee, uint version, Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await UnlinkH(db, t, c).Handle(new UnlinkEmployeeUserCommand(employee, version), default);
    }

    private async Task<Result<EmployeeDto>> TerminateAsync(Guid id, uint version, Guid? company = null, bool audit = false)
    {
        var c = company ?? _companyA1;
        await using var db = NewDb(_tenantA, c, audit);
        return await new TerminateEmployeeHandler(
                new EmployeeRepository(db), Queries(db), Ctx(_tenantA, c, _user), new UnitOfWork(db))
            .Handle(new TerminateEmployeeCommand(id, version, D(6, 30), null), default);
    }

    private async Task<Result<EmployeeDto>> RehireAsync(Guid id, uint version)
    {
        await using var db = NewDb(_tenantA, _companyA1);
        return await new RehireEmployeeHandler(
                new EmployeeRepository(db), new EmployeeBusinessPartnerReader(db), new BusinessPartnerEmploymentGuard(db),
                Queries(db), Ctx(_tenantA, _companyA1, _user), new UnitOfWork(db))
            .Handle(new RehireEmployeeCommand(id, version, D(7, 1)), default);
    }

    private async Task<EmployeeDto> GetAsync(Guid id, Guid? tenant = null, Guid? company = null)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c);
        var r = await new GetEmployeeByIdHandler(Queries(db), Ctx(t, c, _user)).Handle(new GetEmployeeByIdQuery(id), default);
        r.IsSuccess.Should().BeTrue(r.Error);
        return r.Value!;
    }

    private static string ValidCedula(int seq)
    {
        var digits = $"174{seq % 1_000_000:000000}".Select(ch => ch - '0').ToArray();
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var p = digits[i] * (i % 2 == 0 ? 2 : 1);
            sum += p > 9 ? p - 9 : p;
        }
        return string.Concat(digits) + ((10 - sum % 10) % 10);
    }

    /// <summary>Alta real (handler) de una ficha activa.</summary>
    private async Task<Guid> NewEmployeeAsync(Guid? tenant = null, Guid? company = null)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        Guid bpId;
        await using (var db = NewDb(t, Guid.Empty))
        {
            var bp = BusinessPartner.Create(t, "05", ValidCedula(Interlocked.Increment(ref _seq)), null, "Persona vínculo", _user);
            db.BusinessPartners.Add(bp);
            db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(t, bp.Id, RoleType.Employee, _user));
            await db.SaveChangesAsync();
            bpId = bp.Id;
        }
        await using var createDb = NewDb(t, c);
        var r = await new CreateEmployeeHandler(
                new EmployeeRepository(createDb), new EmployeeCatalogRepository(createDb),
                new EmployeeBusinessPartnerReader(createDb), new BusinessPartnerEmploymentGuard(createDb),
                new IdentificationUsageValidator(createDb), Queries(createDb), new PostgresDatabaseExceptionTranslator(),
                Ctx(t, c, _user), new UnitOfWork(createDb))
            .Handle(new CreateEmployeeCommand(bpId, D(1, 10)), default);
        r.IsSuccess.Should().BeTrue(r.Error);
        return r.Value!.Id;
    }

    private async Task<Guid> NewUserAsync(
        Guid[]? companies = null, bool membershipActive = true, bool userActive = true, string role = "Compras")
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        var name = $"usuario-{Guid.NewGuid():N}";
        var user = IdentityUser.Create(name, "Nombre", "Apellido", $"{name}@example.com", "hash", _user);
        if (!userActive)
            user.Deactivate(_user);
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        foreach (var company in companies ?? [])
        {
            var membership = CompanyUserMembership.Create(company, user.Id, role, null, _user);
            if (!membershipActive)
                membership.Deactivate(_user);
            db.CompanyUserMemberships.Add(membership);
        }
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task MakeGlobalAsync(Guid userId)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        db.GlobalUserRoles.Add(GlobalUserRole.Create(userId, SecurityRoles.Admin, _user));
        await db.SaveChangesAsync();
    }

    private Task<int> SetMembershipActiveAsync(Guid company, Guid userId, bool active) =>
        ExecuteAsync($"UPDATE company_user_memberships SET is_active = {active} WHERE company_id = {company} AND identity_user_id = {userId}");

    private async Task<int> ExecuteAsync(FormattableString sql)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        return await db.Database.ExecuteSqlInterpolatedAsync(sql);
    }

    private async Task<T> ScalarAsync<T>(FormattableString sql)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        return await db.Database.SqlQuery<T>(sql).SingleAsync();
    }

    private Task<Guid?> LinkedAsync(Guid employee) =>
        ScalarAsync<Guid?>($"SELECT identity_user_id AS \"Value\" FROM employees WHERE id = {employee}");

    private Task<int> AuditCountAsync(Guid employee) =>
        ScalarAsync<int>($"SELECT count(*)::int AS \"Value\" FROM employee_audit WHERE entity_id = {employee}");

    private async Task<long> AdvisoryWaitingAsync(Guid employee)
    {
        var (ns, key) = EmployeesAdvisoryLocks.EmployeeKey(employee);
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        var c = (long)unchecked((uint)ns);
        var o = (long)unchecked((uint)key);
        return await db.Database.SqlQuery<long>($"""
            SELECT count(*)::bigint AS "Value" FROM pg_locks
             WHERE locktype = 'advisory' AND objsubid = 2 AND NOT granted
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

    /// <summary>Revocación con el handler real de Access (sus puertos no persistentes se simulan).</summary>
    private async Task<Result<object>> RevokeAsync(ErpDbContext db, Guid tenant, Guid company, Guid userId)
    {
        var username = await ScalarAsync<string>($"SELECT username AS \"Value\" FROM identity_users WHERE id = {userId}");
        var cache = new Mock<IPermissionsCacheInvalidator>();
        cache.Setup(c => c.TryBeginAsync(It.IsAny<PermissionsCacheScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cache.Setup(c => c.TryCompleteAsync(It.IsAny<PermissionsCacheScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.UserId).Returns(_user);
        var handler = new RevokeCompanyUserMembershipHandler(
            new AccessRepository(db), currentUser.Object, new TenantRepository(db), new CompanyRepository(db),
            cache.Object, Mock.Of<INavigationBuilder>(), Mock.Of<ICompanyContextProvider>());
        return await handler.Handle(new RevokeCompanyUserMembershipCommand(tenant, company, username), default);
    }

    // ── A. vinculación y desvinculación ──────────────────────────────────────

    [Fact]
    public async Task Vinculacion_y_desvinculacion_validas_con_revinculacion_proyeccion_y_auditoria()
    {
        var employee = await NewEmployeeAsync();
        var userId = await NewUserAsync([_companyA1]);
        var before = await GetAsync(employee);
        before.IdentityUser.Should().BeNull();

        var linked = await LinkAsync(employee, userId, before.RowVersion, audit: true);

        linked.IsSuccess.Should().BeTrue(linked.Error);
        linked.Value!.IdentityUser.Should().BeEquivalentTo(new { Id = userId, MembershipActive = true });
        linked.Value.IdentityUser!.Username.Should().StartWith("usuario-");
        linked.Value.Warnings.Should().BeEmpty("solo la baja genera la advertencia");
        linked.Value.RowVersion.Should().NotBe(before.RowVersion);
        linked.Value.Periods.Should().BeEquivalentTo(before.Periods);

        var unlinked = await UnlinkAsync(employee, linked.Value.RowVersion, audit: true);
        unlinked.IsSuccess.Should().BeTrue(unlinked.Error);
        unlinked.Value!.IdentityUser.Should().BeNull();
        (await LinkedAsync(employee)).Should().BeNull();

        var again = await LinkAsync(employee, userId, unlinked.Value.RowVersion, audit: true);
        again.IsSuccess.Should().BeTrue("tras desvincular se puede volver a vincular");

        await using var db = NewDb(_tenantA, _companyA1);
        var rows = await db.EmployeeAudits.AsNoTracking().Where(a => a.EntityId == employee).OrderBy(a => a.OccurredAtUtc).ToListAsync();
        rows.Select(a => (a.Action, a.ChangedField, a.OldValue, a.NewValue)).Should().Equal(
            ("UserLinked", "IdentityUser", null, userId.ToString()),
            ("UserUnlinked", "IdentityUser", userId.ToString(), null),
            ("UserLinked", "IdentityUser", null, userId.ToString()));
        rows.Should().OnlyContain(a => a.CompanyId == _companyA1);
    }

    [Fact]
    public async Task Ficha_en_baja_se_vincula_y_desvincula_D1_y_un_usuario_inactivo_con_membresia_activa_se_admite_D4()
    {
        var employee = await NewEmployeeAsync();
        var terminated = (await TerminateAsync(employee, (await GetAsync(employee)).RowVersion)).Value!;
        var inactiveUser = await NewUserAsync([_companyA1], userActive: false);

        var linked = await LinkAsync(employee, inactiveUser, terminated.RowVersion);
        linked.IsSuccess.Should().BeTrue(linked.Error);
        linked.Value!.IsActive.Should().BeFalse("no exige reincorporación");
        linked.Value.IdentityUser!.MembershipActive.Should().BeTrue();
        (await ScalarAsync<bool>($"SELECT is_active AS \"Value\" FROM identity_users WHERE id = {inactiveUser}"))
            .Should().BeFalse("el estado del usuario no se modifica");

        (await UnlinkAsync(employee, linked.Value.RowVersion)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Ficha_ya_vinculada_no_se_reemplaza_ni_con_el_mismo_usuario_D2_y_desvincular_sin_vinculo_falla()
    {
        var employee = await NewEmployeeAsync();
        var first = await NewUserAsync([_companyA1]);
        var second = await NewUserAsync([_companyA1]);

        var noLink = await UnlinkAsync(employee, (await GetAsync(employee)).RowVersion, audit: true);
        noLink.Code.Should().Be(ApiResponseCodes.Employees.UserNotLinked);

        var linked = (await LinkAsync(employee, first, (await GetAsync(employee)).RowVersion)).Value!;
        var auditBefore = await AuditCountAsync(employee);
        (await LinkAsync(employee, first, linked.RowVersion, audit: true)).Code.Should().Be(ApiResponseCodes.Employees.UserAlreadyLinked);
        (await LinkAsync(employee, second, linked.RowVersion, audit: true)).Code.Should().Be(ApiResponseCodes.Employees.UserAlreadyLinked);
        (await LinkAsync(employee, Guid.NewGuid(), linked.RowVersion)).Code.Should().Be(ApiResponseCodes.Employees.UserAlreadyLinked,
            "la ficha vinculada se rechaza antes de mirar al usuario");
        (await LinkedAsync(employee)).Should().Be(first);
        (await AuditCountAsync(employee)).Should().Be(auditBefore);
    }

    // ── B. identidad y aislamiento ───────────────────────────────────────────

    [Fact]
    public async Task Usuarios_fuera_de_alcance_reciben_el_mismo_codigo_y_mensaje_sin_persistir()
    {
        var employee = await NewEmployeeAsync();
        var version = (await GetAsync(employee)).RowVersion;
        var global = await NewUserAsync([_companyA1]);
        await MakeGlobalAsync(global);
        var staleTenant = await NewUserAsync([_companyB1]);
        // IdentityUser.TenantId (obsoleto) apunta al tenant operativo: no debe otorgar pertenencia.
        await ExecuteAsync($"UPDATE identity_users SET tenant_id = {_tenantA} WHERE id = {staleTenant}");
        var cases = new[]
        {
            Guid.NewGuid(),                                         // inexistente
            await NewUserAsync(),                                   // sin membresía
            await NewUserAsync([_companyA2]),                       // solo otra empresa del tenant
            await NewUserAsync([_companyB1]),                       // solo otro tenant
            staleTenant,
            await NewUserAsync([_companyA1], membershipActive: false),
            global,
        };

        var results = new List<Result<EmployeeDto>>();
        foreach (var userId in cases)
            results.Add(await LinkAsync(employee, userId, version, audit: true));

        results.Should().OnlyContain(r => !r.IsSuccess && r.Code == ApiResponseCodes.Employees.UserNoMembership);
        results.Select(r => r.Error).Distinct().Should().HaveCount(1, "indistinguibles");
        (await LinkedAsync(employee)).Should().BeNull();
        (await GetAsync(employee)).RowVersion.Should().Be(version);
        (await AuditCountAsync(employee)).Should().Be(0);
    }

    [Fact]
    public async Task Ficha_de_otra_empresa_o_tenant_en_link_y_unlink_es_EMPLOYEE_NOT_FOUND_indistinguible()
    {
        var employee = await NewEmployeeAsync();
        var userId = await NewUserAsync([_companyA1, _companyA2, _companyB1]);
        var version = (await GetAsync(employee)).RowVersion;

        var results = new[]
        {
            await LinkAsync(Guid.NewGuid(), userId, version),
            await LinkAsync(employee, userId, version, company: _companyA2),
            await LinkAsync(employee, userId, version, _tenantB, _companyB1),
            await UnlinkAsync(employee, version, company: _companyA2),
            await UnlinkAsync(employee, version, _tenantB, _companyB1),
        };

        results.Should().OnlyContain(r => r.Code == ApiResponseCodes.Employees.NotFound);
        results.Select(r => r.Error).Distinct().Should().HaveCount(1);
        (await GetAsync(employee)).RowVersion.Should().Be(version);
    }

    [Fact]
    public async Task El_bloqueo_y_la_busqueda_de_vinculo_aplican_tenant_y_empresa_como_predicado_propio()
    {
        var employee = await NewEmployeeAsync();
        var userId = await NewUserAsync([_companyA1]);
        (await LinkAsync(employee, userId, (await GetAsync(employee)).RowVersion)).IsSuccess.Should().BeTrue();

        await using var db = NewDb(_tenantA, _companyA1);
        var repo = new EmployeeRepository(db);
        (await repo.IsUserLinkedToOtherEmployeeAsync(_tenantA, _companyA1, userId, Guid.NewGuid())).Should().BeTrue();
        (await repo.IsUserLinkedToOtherEmployeeAsync(_tenantB, _companyA1, userId, Guid.NewGuid())).Should().BeFalse();
        (await repo.IsUserLinkedToOtherEmployeeAsync(_tenantA, _companyA2, userId, Guid.NewGuid())).Should().BeFalse();
        (await repo.IsUserLinkedToOtherEmployeeAsync(_tenantA, _companyA1, userId, employee)).Should().BeFalse("su propia ficha");

        await using var tx = await db.Database.BeginTransactionAsync();
        var reader = new EmployeeUserReader(db);
        (await reader.TryLockMembershipAsync(_tenantA, _companyA1, userId)).Should().BeTrue();
        (await reader.TryLockMembershipAsync(_tenantB, _companyA1, userId)).Should().BeFalse("predicado de tenant");
        (await reader.TryLockMembershipAsync(_tenantA, _companyA2, userId)).Should().BeFalse("predicado de empresa");
        await tx.RollbackAsync();
    }

    // ── C. unicidad, concurrencia y rowVersion ───────────────────────────────

    [Fact]
    public async Task Un_usuario_puede_estar_en_fichas_de_distintas_empresas_pero_solo_una_por_empresa_T01()
    {
        var userId = await NewUserAsync([_companyA1, _companyA2]);
        var e1 = await NewEmployeeAsync();
        var e2 = await NewEmployeeAsync();
        var other = await NewEmployeeAsync(company: _companyA2);

        (await LinkAsync(e1, userId, (await GetAsync(e1)).RowVersion)).IsSuccess.Should().BeTrue();
        (await LinkAsync(other, userId, (await GetAsync(other, company: _companyA2)).RowVersion, company: _companyA2))
            .IsSuccess.Should().BeTrue("la unicidad es por empresa");
        (await LinkAsync(e2, userId, (await GetAsync(e2)).RowVersion)).Code.Should().Be(ApiResponseCodes.Employees.UserAlreadyLinked);
        (await LinkedAsync(e2)).Should().BeNull();

        // Defensa física UX-EMP-02, saltándose las validaciones de aplicación.
        var act = () => ExecuteAsync($"UPDATE employees SET identity_user_id = {userId} WHERE id = {e2}");
        var failure = await act.Should().ThrowAsync<Exception>();
        var pg = failure.Which.GetBaseException().Should().BeOfType<PostgresException>().Subject;
        (pg.SqlState, pg.ConstraintName).Should().Be(("23505", "uq_employees_company_identity_user"));
    }

    /// <summary>Lector que pausa tras bloquear la membresía (dentro de la transacción de vinculación).</summary>
    private sealed class PausingReader(IEmployeeUserReader inner) : IEmployeeUserReader
    {
        public readonly TaskCompletionSource Reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Proceed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> TryLockMembershipAsync(Guid t, Guid c, Guid u, CancellationToken ct = default)
        {
            var r = await inner.TryLockMembershipAsync(t, c, u, ct);
            Reached.TrySetResult();
            await Proceed.Task.WaitAsync(Timeout, ct);
            return r;
        }
    }

    [Fact]
    public async Task Dos_vinculaciones_simultaneas_del_mismo_usuario_a_fichas_distintas_dejan_una_y_la_otra_ALREADY_LINKED()
    {
        // Determinista: la primera queda pausada con su validación hecha; la segunda confirma antes, de modo
        // que la primera choca con UX-EMP-02 al escribir (traducción de la restricción física).
        var userId = await NewUserAsync([_companyA1]);
        var e1 = await NewEmployeeAsync();
        var e2 = await NewEmployeeAsync();
        var v1 = (await GetAsync(e1)).RowVersion;
        var v2 = (await GetAsync(e2)).RowVersion;

        await using var db = NewDb(_tenantA, _companyA1, audit: true);
        var paused = new PausingReader(new EmployeeUserReader(db));
        var first = LinkH(db, _tenantA, _companyA1, paused).Handle(new LinkEmployeeUserCommand(e1, userId, v1), default);
        await paused.Reached.Task.WaitAsync(Timeout);
        var second = await LinkAsync(e2, userId, v2, audit: true);
        second.IsSuccess.Should().BeTrue(second.Error);
        paused.Proceed.SetResult();
        var firstResult = await first.WaitAsync(Timeout);

        firstResult.Code.Should().Be(ApiResponseCodes.Employees.UserAlreadyLinked);
        (await LinkedAsync(e1)).Should().BeNull();
        (await LinkedAsync(e2)).Should().Be(userId);
        (await AuditCountAsync(e1)).Should().Be(0, "sin auditoría de éxito ante el conflicto");
        (await AuditCountAsync(e2)).Should().Be(1);

        // Sin coordinación forzada: siempre exactamente un ganador.
        for (var round = 0; round < 3; round++)
        {
            var user = await NewUserAsync([_companyA1]);
            var a = await NewEmployeeAsync();
            var b = await NewEmployeeAsync();
            var va = (await GetAsync(a)).RowVersion;
            var vb = (await GetAsync(b)).RowVersion;
            var results = await Task.WhenAll(LinkAsync(a, user, va), LinkAsync(b, user, vb));
            results.Count(r => r.IsSuccess).Should().Be(1);
            results.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Employees.UserAlreadyLinked);
        }
    }

    [Fact]
    public async Task Dos_vinculaciones_sobre_la_misma_ficha_se_serializan_y_la_segunda_ve_la_version_obsoleta()
    {
        var u1 = await NewUserAsync([_companyA1]);
        var u2 = await NewUserAsync([_companyA1]);
        var employee = await NewEmployeeAsync();
        var v = (await GetAsync(employee)).RowVersion;

        await using var db = NewDb(_tenantA, _companyA1);
        var paused = new PausingReader(new EmployeeUserReader(db));
        var first = LinkH(db, _tenantA, _companyA1, paused).Handle(new LinkEmployeeUserCommand(employee, u1, v), default);
        await paused.Reached.Task.WaitAsync(Timeout);
        var second = LinkAsync(employee, u2, v, audit: true);
        await WaitUntilAsync(async () => await AdvisoryWaitingAsync(employee) == 1);
        second.IsCompleted.Should().BeFalse();

        paused.Proceed.SetResult();
        (await first.WaitAsync(Timeout)).IsSuccess.Should().BeTrue();
        (await second.WaitAsync(Timeout)).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await LinkedAsync(employee)).Should().Be(u1);
        (await AuditCountAsync(employee)).Should().Be(0);
    }

    [Fact]
    public async Task Version_obsoleta_en_link_y_unlink_no_cambia_nada_y_un_unlink_obsoleto_no_borra_el_vinculo_nuevo()
    {
        var u1 = await NewUserAsync([_companyA1]);
        var u2 = await NewUserAsync([_companyA1]);
        var employee = await NewEmployeeAsync();
        var v0 = (await GetAsync(employee)).RowVersion;
        var v1 = (await LinkAsync(employee, u1, v0)).Value!.RowVersion;

        (await LinkAsync(employee, u2, v0, audit: true)).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict,
            "la versión se comprueba antes que la regla de vínculo existente");
        var v2 = (await UnlinkAsync(employee, v1)).Value!.RowVersion;
        (await LinkAsync(employee, u2, v2)).IsSuccess.Should().BeTrue();

        var stale = await UnlinkAsync(employee, v1, audit: true);
        stale.Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await LinkedAsync(employee)).Should().Be(u2, "el vínculo nuevo sobrevive");
        (await AuditCountAsync(employee)).Should().Be(0);
        // Existencia antes que versión.
        (await UnlinkAsync(Guid.NewGuid(), 1)).Code.Should().Be(ApiResponseCodes.Employees.NotFound);
    }

    [Fact]
    public async Task Vinculacion_contra_desvinculacion_baja_y_reincorporacion_con_la_misma_version_aplica_una_sola()
    {
        for (var round = 0; round < 3; round++)
        {
            var userId = await NewUserAsync([_companyA1]);

            var a = await NewEmployeeAsync();
            var linkedA = (await LinkAsync(a, userId, (await GetAsync(a)).RowVersion)).Value!;
            var other = await NewUserAsync([_companyA1]);
            var unlink = UnlinkAsync(a, linkedA.RowVersion);
            var relink = LinkAsync(a, other, linkedA.RowVersion);
            var u = await unlink;
            var l = await relink;
            u.IsSuccess.Should().BeTrue("la desvinculación con la versión vigente prospera");
            l.IsSuccess.Should().BeFalse();
            (await LinkedAsync(a)).Should().BeNull();

            var b = await NewEmployeeAsync();
            var vb = (await GetAsync(b)).RowVersion;
            var userB = await NewUserAsync([_companyA1]);
            var link = LinkAsync(b, userB, vb);
            var terminate = TerminateAsync(b, vb);
            var results = new[] { await link, await terminate };
            results.Count(r => r.IsSuccess).Should().Be(1);
            results.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
            var dtoB = await GetAsync(b);
            (dtoB.IdentityUser is not null).Should().Be(results[0].IsSuccess);
            dtoB.IsActive.Should().Be(!results[1].IsSuccess);

            var c = await NewEmployeeAsync();
            var t = (await TerminateAsync(c, (await GetAsync(c)).RowVersion)).Value!;
            var userC = await NewUserAsync([_companyA1]);
            var linkC = LinkAsync(c, userC, t.RowVersion);
            var rehire = RehireAsync(c, t.RowVersion);
            var rc = new[] { await linkC, await rehire };
            rc.Count(r => r.IsSuccess).Should().Be(1);
            rc.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
            var dtoC = await GetAsync(c);
            dtoC.Periods.Count.Should().Be(rc[1].IsSuccess ? 2 : 1);
            (dtoC.IdentityUser is not null).Should().Be(rc[0].IsSuccess);
        }
    }

    // ── D. concurrencia con membresías (handler real de Access) ──────────────

    [Fact]
    public async Task Vinculacion_primero_hace_esperar_a_la_revocacion_que_luego_prospera_y_el_vinculo_permanece()
    {
        var userId = await NewUserAsync([_companyA1]);
        var employee = await NewEmployeeAsync();
        var v = (await GetAsync(employee)).RowVersion;

        await using var db = NewDb(_tenantA, _companyA1);
        var paused = new PausingReader(new EmployeeUserReader(db));
        var link = LinkH(db, _tenantA, _companyA1, paused).Handle(new LinkEmployeeUserCommand(employee, userId, v), default);
        await paused.Reached.Task.WaitAsync(Timeout);

        await using var revokeDb = NewDb(_tenantA, _companyA1);
        var revoke = RevokeAsync(revokeDb, _tenantA, _companyA1, userId);
        await WaitUntilAsync(async () => await BlockedRowLockWaitersAsync() >= 1);
        revoke.IsCompleted.Should().BeFalse("la membresía está bloqueada FOR SHARE por la vinculación");

        paused.Proceed.SetResult();
        (await link.WaitAsync(Timeout)).IsSuccess.Should().BeTrue();
        var revoked = await revoke.WaitAsync(Timeout);
        revoked.IsSuccess.Should().BeTrue(revoked.Error);

        var dto = await GetAsync(employee);
        dto.IdentityUser.Should().BeEquivalentTo(new { Id = userId, MembershipActive = false }, "el vínculo permanece");
        (await ScalarAsync<bool>($"SELECT is_active AS \"Value\" FROM company_user_memberships WHERE company_id = {_companyA1} AND identity_user_id = {userId}"))
            .Should().BeFalse("nada reactiva la membresía");
    }

    [Fact]
    public async Task Revocacion_en_curso_hace_esperar_a_la_vinculacion_que_luego_se_rechaza()
    {
        var userId = await NewUserAsync([_companyA1]);
        var employee = await NewEmployeeAsync();
        var v = (await GetAsync(employee)).RowVersion;

        await using var revokeDb = NewDb(_tenantA, _companyA1);
        await using var tx = await revokeDb.Database.BeginTransactionAsync();
        var revoked = await RevokeAsync(revokeDb, _tenantA, _companyA1, userId);
        revoked.IsSuccess.Should().BeTrue(revoked.Error); // escrito, sin confirmar

        var link = LinkAsync(employee, userId, v, audit: true);
        await WaitUntilAsync(async () => await BlockedRowLockWaitersAsync() >= 1);
        link.IsCompleted.Should().BeFalse();

        await tx.CommitAsync();
        (await link.WaitAsync(Timeout)).Code.Should().Be(ApiResponseCodes.Employees.UserNoMembership, "re-evalúa tras el bloqueo");
        (await LinkedAsync(employee)).Should().BeNull();
        (await AuditCountAsync(employee)).Should().Be(0);
    }

    // ── E. cuenta posteriormente global ──────────────────────────────────────

    [Fact]
    public async Task Cuenta_que_pasa_a_global_se_oculta_no_genera_advertencia_y_se_puede_desvincular_sin_cambios_en_IAM()
    {
        var userId = await NewUserAsync([_companyA1]);
        var employee = await NewEmployeeAsync();
        (await LinkAsync(employee, userId, (await GetAsync(employee)).RowVersion)).IsSuccess.Should().BeTrue();
        await MakeGlobalAsync(userId);
        var iamBefore = await IamStateAsync();

        var dto = await GetAsync(employee);
        dto.IdentityUser.Should().BeNull("IAM-V1-F1");
        System.Text.Json.JsonSerializer.Serialize(dto).Should().NotContain(userId.ToString()).And.NotContain("usuario-");

        var terminated = await TerminateAsync(employee, dto.RowVersion);
        terminated.Value!.Warnings.Should().BeEmpty();
        terminated.Value.IdentityUser.Should().BeNull();

        var unlinked = await UnlinkAsync(employee, terminated.Value.RowVersion);
        unlinked.IsSuccess.Should().BeTrue(unlinked.Error);
        unlinked.Value!.IdentityUser.Should().BeNull();
        System.Text.Json.JsonSerializer.Serialize(unlinked.Value).Should().NotContain(userId.ToString());
        (await LinkedAsync(employee)).Should().BeNull();
        (await IamStateAsync()).Should().Be(iamBefore, "roles globales, usuario y membresías intactos");
    }

    // ── F. ACTIVE_MEMBERSHIP ─────────────────────────────────────────────────

    [Fact]
    public async Task Baja_advierte_ACTIVE_MEMBERSHIP_solo_con_membresia_activa_en_la_empresa_de_la_ficha()
    {
        // sin usuario
        var none = await NewEmployeeAsync();
        (await TerminateAsync(none, (await GetAsync(none)).RowVersion)).Value!.Warnings.Should().BeEmpty();

        // con membresía activa
        var active = await NewEmployeeAsync();
        var userActive = await NewUserAsync([_companyA1]);
        var linkedActive = (await LinkAsync(active, userActive, (await GetAsync(active)).RowVersion)).Value!;
        var r = await TerminateAsync(active, linkedActive.RowVersion);
        r.IsSuccess.Should().BeTrue("la advertencia no bloquea la baja");
        r.Value!.Warnings.Should().Equal(ApiResponseCodes.Employees.ActiveMembership);
        r.Value.IsActive.Should().BeFalse();
        r.Value.IdentityUser!.Id.Should().Be(userActive, "la baja no desvincula");
        var after = await GetAsync(active);
        after.Warnings.Should().BeEmpty("no se persiste ni aparece en GET");
        (await ScalarAsync<bool>($"SELECT is_active AS \"Value\" FROM company_user_memberships WHERE company_id = {_companyA1} AND identity_user_id = {userActive}"))
            .Should().BeTrue("la baja no revoca el acceso");

        // vinculado sin membresía activa en la empresa, con membresía activa solo en otra
        var inactive = await NewEmployeeAsync();
        var elsewhere = await NewUserAsync([_companyA1, _companyA2]);
        var linkedInactive = (await LinkAsync(inactive, elsewhere, (await GetAsync(inactive)).RowVersion)).Value!;
        await SetMembershipActiveAsync(_companyA1, elsewhere, false);
        var w = await TerminateAsync(inactive, linkedInactive.RowVersion);
        w.Value!.Warnings.Should().BeEmpty("la membresía activa de otra empresa no cuenta");
        w.Value.IdentityUser!.MembershipActive.Should().BeFalse();
    }

    // ── H. T-08: independencia de Access ─────────────────────────────────────

    private async Task<string> IamStateAsync()
    {
        var parts = new List<string>();
        foreach (var table in new[]
        {
            "identity_users", "company_user_memberships", "access_profiles", "access_profile_permissions",
            "access.global_user_roles", "company_user_branches",
        })
        {
            await using var db = NewDb(Guid.Empty, Guid.Empty);
            parts.Add(await db.Database
                .SqlQueryRaw<string>($"SELECT coalesce(md5(string_agg(t::text, '|' ORDER BY t::text)), '-') AS \"Value\" FROM {table} t")
                .SingleAsync());
        }
        return string.Join(";", parts);
    }

    [Fact]
    public async Task T08_vincular_desvincular_y_dar_de_baja_no_modifican_nada_de_Identity_ni_Access()
    {
        var userId = await NewUserAsync([_companyA1]);
        var employee = await NewEmployeeAsync();
        var before = await IamStateAsync();

        var linked = (await LinkAsync(employee, userId, (await GetAsync(employee)).RowVersion)).Value!;
        (await IamStateAsync()).Should().Be(before);
        var terminated = (await TerminateAsync(employee, linked.RowVersion)).Value!;
        (await IamStateAsync()).Should().Be(before);
        var unlinked = await UnlinkAsync(employee, terminated.RowVersion);
        unlinked.IsSuccess.Should().BeTrue();
        (await IamStateAsync()).Should().Be(before);
        unlinked.Value!.Periods.Should().BeEquivalentTo(terminated.Periods, "historial laboral conservado");
        unlinked.Value.Assignments.Should().BeEquivalentTo(terminated.Assignments);
    }
}
