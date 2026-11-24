using System.Data;
using ERP.Application.Common;
using ERP.Application.MasterData.UseCases.DeactivateBusinessPartner;
using ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Employees;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Employees;

/// <summary>
/// EMP-E3 — protección de BusinessPartner (EMP-T-03, E-D3, EMP-BR-17, INV-10) sobre PostgreSQL 16
/// real (Testcontainers; BD desechable): guardia funcional, aislamiento tenant/empresa (T-06), lock
/// transaccional <c>("BpEmployment", tenant, bp)</c> y carrera parcial T-05. El lado «crear ficha»
/// de T-05 se SIMULA aquí con el protocolo aprobado (lock → verificar BP y rol → insertar); el caso
/// de uso real llega con EMP-E4/E5, que completan la acreditación. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class BusinessPartnerEmploymentGuardPostgreSqlTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_bp_employment_guard_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenantA, _companyA1, _companyA2, _tenantB, _companyB1;
    private int _seq;

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

    private ErpDbContext NewDb(Guid tenant, Guid company) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(() => tenant),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => company)
        );

    private static IOperationalContext Ctx(Guid tenant, Guid company, Guid user)
    {
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(company);
        ctx.SetupGet(x => x.UserId).Returns(user);
        return ctx.Object;
    }

    private DeactivateBusinessPartnerHandler DeactivateHandler(
        ErpDbContext db, Guid tenant, Guid company, IBusinessPartnerEmploymentGuard? guard = null) =>
        new(new BusinessPartnerRepository(db), Ctx(tenant, company, _user),
            guard ?? new BusinessPartnerEmploymentGuard(db), new UnitOfWork(db));

    private RevokeBusinessPartnerRoleHandler RevokeHandler(
        ErpDbContext db, Guid tenant, Guid company, IBusinessPartnerEmploymentGuard? guard = null) =>
        new(new BusinessPartnerRoleRepository(db), Ctx(tenant, company, _user),
            guard ?? new BusinessPartnerEmploymentGuard(db), new UnitOfWork(db));

    /// <summary>Crea un BP con los roles indicados; devuelve (bpId, id del rol por tipo).</summary>
    private async Task<(Guid Bp, Dictionary<RoleType, Guid> Roles)> NewBpAsync(Guid tenant, params RoleType[] roles)
    {
        await using var db = NewDb(tenant, Guid.Empty);
        var bp = BusinessPartner.Create(tenant, "04", $"1791352688{Interlocked.Increment(ref _seq):000}", null, "BP prueba", _user);
        db.BusinessPartners.Add(bp);
        var ids = new Dictionary<RoleType, Guid>();
        foreach (var r in roles)
        {
            var role = BusinessPartnerRole.Create(tenant, bp.Id, r, _user);
            db.BusinessPartnerRoles.Add(role);
            ids[r] = role.Id;
        }
        await db.SaveChangesAsync();
        return (bp.Id, ids);
    }

    private async Task<Guid> NewFichaAsync(Guid tenant, Guid company, Guid bp, bool active = true)
    {
        await using var db = NewDb(tenant, company);
        var e = Employee.Create(tenant, company, bp, new DateOnly(2026, 1, 1), _user);
        db.Employees.Add(e);
        await db.SaveChangesAsync();
        if (!active)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE employee_employment_periods SET end_date = {new DateOnly(2026, 6, 30)} WHERE employee_id = {e.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE employees SET is_active = false WHERE id = {e.Id}");
        }
        return e.Id;
    }

    private async Task<(bool BpActive, bool RoleActive, DateTime? RevokedAt, int Fichas)> StateAsync(
        Guid tenant, Guid bp, Guid roleId)
    {
        await using var db = NewDb(tenant, Guid.Empty);
        var bpRow = await db.BusinessPartners.AsNoTracking().SingleAsync(x => x.Id == bp);
        var role = await db.BusinessPartnerRoles.AsNoTracking().SingleAsync(x => x.Id == roleId);
        var fichas = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM employees WHERE tenant_id = {tenant} AND business_partner_id = {bp}").SingleAsync();
        return (bpRow.IsActive, role.IsActive, role.RevokedAt, fichas);
    }

    // ── protección funcional ─────────────────────────────────────────────────

    [Fact]
    public async Task Desactivar_BP_con_ficha_activa_se_bloquea_sin_persistir_cambios()
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);
        await NewFichaAsync(_tenantA, _companyA1, bp);

        await using var db = NewDb(_tenantA, _companyA1);
        var result = await DeactivateHandler(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), default);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.MasterData.BpHasActiveEmployment).And.Be("BP_HAS_ACTIVE_EMPLOYMENT");
        var s = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
        s.BpActive.Should().BeTrue();
        s.RoleActive.Should().BeTrue();
        db.Database.CurrentTransaction.Should().BeNull("la transacción propia se revierte y se libera");
    }

    [Fact]
    public async Task Revocar_rol_Employee_con_ficha_activa_se_bloquea_sin_persistir_cambios()
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee, RoleType.Customer);
        await NewFichaAsync(_tenantA, _companyA1, bp);

        await using var db = NewDb(_tenantA, _companyA1);
        var result = await RevokeHandler(db, _tenantA, _companyA1)
            .Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Employee]), default);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.MasterData.BpHasActiveEmployment);
        var s = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
        (s.RoleActive, s.RevokedAt).Should().Be((true, (DateTime?)null));
    }

    [Fact]
    public async Task BP_sin_ficha_conserva_el_comportamiento_anterior()
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);

        await using (var db = NewDb(_tenantA, _companyA1))
            (await RevokeHandler(db, _tenantA, _companyA1)
                .Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Employee]), default))
                .IsSuccess.Should().BeTrue();
        await using (var db = NewDb(_tenantA, _companyA1))
            (await DeactivateHandler(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), default))
                .IsSuccess.Should().BeTrue();

        var s = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
        (s.BpActive, s.RoleActive).Should().Be((false, false));
    }

    [Fact]
    public async Task BP_con_ficha_inactiva_se_puede_desactivar_y_revocar()
    {
        var (bp1, roles1) = await NewBpAsync(_tenantA, RoleType.Employee);
        var (bp2, roles2) = await NewBpAsync(_tenantA, RoleType.Employee);
        await NewFichaAsync(_tenantA, _companyA1, bp1, active: false);
        await NewFichaAsync(_tenantA, _companyA1, bp2, active: false);

        await using (var db = NewDb(_tenantA, _companyA1))
            (await DeactivateHandler(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp1), default))
                .IsSuccess.Should().BeTrue();
        await using (var db = NewDb(_tenantA, _companyA1))
            (await RevokeHandler(db, _tenantA, _companyA1)
                .Handle(new RevokeBusinessPartnerRoleCommand(bp2, roles2[RoleType.Employee]), default))
                .IsSuccess.Should().BeTrue();

        (await StateAsync(_tenantA, bp1, roles1[RoleType.Employee])).BpActive.Should().BeFalse();
        (await StateAsync(_tenantA, bp2, roles2[RoleType.Employee])).RoleActive.Should().BeFalse();
    }

    [Fact]
    public async Task Revocar_otro_rol_no_se_bloquea_aunque_el_BP_tenga_ficha_activa()
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee, RoleType.Customer);
        await NewFichaAsync(_tenantA, _companyA1, bp);

        await using var db = NewDb(_tenantA, _companyA1);
        var result = await RevokeHandler(db, _tenantA, _companyA1)
            .Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Customer]), default);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await StateAsync(_tenantA, bp, roles[RoleType.Customer])).RoleActive.Should().BeFalse();
        (await StateAsync(_tenantA, bp, roles[RoleType.Employee])).RoleActive.Should().BeTrue();
    }

    // ── T-06 aislamiento ─────────────────────────────────────────────────────

    [Fact]
    public async Task T06_ficha_activa_en_otra_empresa_del_mismo_tenant_bloquea_desde_cualquier_empresa_activa()
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);
        await NewFichaAsync(_tenantA, _companyA2, bp); // ficha en A2

        await using var db = NewDb(_tenantA, _companyA1); // el usuario opera en A1
        // El filtro global de empresa no ve la ficha...
        (await db.Employees.CountAsync(e => e.BusinessPartnerId == bp)).Should().Be(0);
        // ...pero la guardia sí (todas las empresas del tenant).
        var result = await DeactivateHandler(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), default);

        result.Code.Should().Be(ApiResponseCodes.MasterData.BpHasActiveEmployment);
        (await StateAsync(_tenantA, bp, roles[RoleType.Employee])).BpActive.Should().BeTrue();
    }

    [Fact]
    public async Task T06_la_guardia_no_cruza_tenants_ni_filtra_informacion()
    {
        var (bpB, _) = await NewBpAsync(_tenantB, RoleType.Employee);
        await NewFichaAsync(_tenantB, _companyB1, bpB);

        await using var db = NewDb(_tenantA, _companyA1);
        var guard = new BusinessPartnerEmploymentGuard(db);

        (await guard.HasActiveEmploymentAsync(_tenantB, bpB)).Should().BeTrue("control: en su propio tenant sí existe");
        (await guard.HasActiveEmploymentAsync(_tenantA, bpB)).Should().BeFalse("la ficha de otro tenant no bloquea");

        // El tipo de retorno es un booleano; el mensaje al cliente no revela empresas ni cantidades.
        var (bpA, _) = await NewBpAsync(_tenantA, RoleType.Employee);
        await NewFichaAsync(_tenantA, _companyA2, bpA);
        var result = await DeactivateHandler(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bpA), default);
        result.Error.Should().NotContain("A2").And.NotContain(_companyA2.ToString()).And.NotContain(bpA.ToString());
    }

    [Fact]
    public async Task T06_ficha_del_otro_tenant_no_bloquea_la_desactivacion_en_el_propio()
    {
        var (bpA, rolesA) = await NewBpAsync(_tenantA, RoleType.Employee);
        var (bpB, _) = await NewBpAsync(_tenantB, RoleType.Employee);
        await NewFichaAsync(_tenantB, _companyB1, bpB);

        await using var db = NewDb(_tenantA, _companyA1);
        (await DeactivateHandler(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bpA), default))
            .IsSuccess.Should().BeTrue();
        (await StateAsync(_tenantA, bpA, rolesA[RoleType.Employee])).BpActive.Should().BeFalse();
    }

    [Fact]
    public async Task La_guardia_es_fail_closed_sin_tenant_o_sin_BP()
    {
        await using var db = NewDb(_tenantA, _companyA1);
        var guard = new BusinessPartnerEmploymentGuard(db);

        await FluentActions.Awaiting(() => guard.HasActiveEmploymentAsync(Guid.Empty, Guid.NewGuid()))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => guard.HasActiveEmploymentAsync(_tenantA, Guid.Empty))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => guard.AcquireBpEmploymentLockAsync(Guid.Empty, Guid.NewGuid()))
            .Should().ThrowAsync<ArgumentException>();
    }

    // ── lock transaccional ───────────────────────────────────────────────────

    [Fact]
    public async Task Lock_sin_transaccion_activa_se_rechaza()
    {
        await using var db = NewDb(_tenantA, _companyA1);
        await FluentActions.Awaiting(() => new BusinessPartnerEmploymentGuard(db)
                .AcquireBpEmploymentLockAsync(_tenantA, Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Clave_del_lock_es_determinista_y_distingue_tenant_y_BP()
    {
        var t = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var b = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        EmployeesAdvisoryLocks.BpEmploymentKey(t, b).Should().Be(EmployeesAdvisoryLocks.BpEmploymentKey(t, b));
        EmployeesAdvisoryLocks.BpEmploymentKey(t, b).Should().NotBe(EmployeesAdvisoryLocks.BpEmploymentKey(b, t));
        EmployeesAdvisoryLocks.BpEmploymentKey(t, b).Should().NotBe(EmployeesAdvisoryLocks.BpEmploymentKey(t, Guid.NewGuid()));

        // Algoritmo del precedente (h = 17; h = h * 31 + b), calculado de forma independiente.
        static int H(byte[] bytes) { var h = 17; foreach (var x in bytes) h = unchecked(h * 31 + x); return h; }
        var expectedKey = H(t.ToByteArray().Concat(b.ToByteArray()).ToArray());
        var expectedNs = H(System.Text.Encoding.UTF8.GetBytes("BpEmployment"));
        EmployeesAdvisoryLocks.BpEmploymentKey(t, b).Should().Be((expectedNs, expectedKey));
    }

    [Fact]
    public async Task Lock_usa_la_clave_aprobada_se_serializa_y_se_libera_al_confirmar_y_al_revertir()
    {
        var tenant = _tenantA;
        var bp = Guid.NewGuid();
        var (ns, key) = EmployeesAdvisoryLocks.BpEmploymentKey(tenant, bp);

        foreach (var commit in new[] { true, false })
        {
            await using var db1 = NewDb(tenant, _companyA1);
            await using var db2 = NewDb(tenant, _companyA1);
            await using var tx1 = await db1.Database.BeginTransactionAsync();
            await new BusinessPartnerEmploymentGuard(db1).AcquireBpEmploymentLockAsync(tenant, bp);

            // Clave real en pg_locks (concedida) = (hash del namespace, hash tenant+BP).
            (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(1);
            // El aislamiento de la transacción es READ COMMITTED (la guardia lee estado confirmado).
            (await db1.Database.SqlQueryRaw<string>("SELECT current_setting('transaction_isolation') AS \"Value\"")
                .SingleAsync()).Should().Be("read committed");

            await using var tx2 = await db2.Database.BeginTransactionAsync();
            var waiter = new BusinessPartnerEmploymentGuard(db2).AcquireBpEmploymentLockAsync(tenant, bp);
            await WaitUntilAsync(async () => await AdvisoryCountAsync(ns, key, granted: false) == 1);
            waiter.IsCompleted.Should().BeFalse("la misma clave se serializa");

            if (commit) await tx1.CommitAsync(); else await tx1.RollbackAsync();
            await waiter.WaitAsync(Timeout); // liberado automáticamente
            await tx2.RollbackAsync();
            (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(0);
        }
    }

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

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("La condición no se cumplió.");
            await Task.Delay(25);
        }
    }

    // ── T-05 (parcial EMP-E3): creación simulada vs desactivar/revocar ───────

    /// <summary>Decorador de la guardia que pausa tras comprobar (con el lock tomado) hasta recibir la señal.</summary>
    private sealed class PausingGuard(IBusinessPartnerEmploymentGuard inner) : IBusinessPartnerEmploymentGuard
    {
        public readonly TaskCompletionSource Checked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Proceed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task AcquireBpEmploymentLockAsync(Guid t, Guid b, CancellationToken ct = default) =>
            inner.AcquireBpEmploymentLockAsync(t, b, ct);

        public async Task<bool> HasActiveEmploymentAsync(Guid t, Guid b, CancellationToken ct = default)
        {
            var r = await inner.HasActiveEmploymentAsync(t, b, ct);
            Checked.TrySetResult();
            await Proceed.Task.WaitAsync(Timeout, ct);
            return r;
        }
    }

    /// <summary>
    /// Protocolo aprobado de alta (simulado): transacción → lock BpEmployment → verificar BP activo y
    /// rol Employee activo (estado confirmado) → insertar ficha → confirmar. Devuelve si creó la ficha.
    /// <paramref name="afterLock"/> permite coordinar de forma determinista.
    /// </summary>
    private async Task<bool> SimulatedCreateAsync(Guid bp, Func<Task>? afterLock = null)
    {
        await using var db = NewDb(_tenantA, _companyA1);
        await using var tx = await db.Database.BeginTransactionAsync();
        await new BusinessPartnerEmploymentGuard(db).AcquireBpEmploymentLockAsync(_tenantA, bp);
        if (afterLock is not null) await afterLock();

        var bpActive = await db.BusinessPartners.AsNoTracking().AnyAsync(b => b.Id == bp && b.IsActive);
        var roleActive = await db.BusinessPartnerRoles.AsNoTracking()
            .AnyAsync(r => r.BusinessPartnerId == bp && r.RoleType == RoleType.Employee && r.IsActive);
        if (!bpActive || !roleActive)
        {
            await tx.RollbackAsync();
            return false;
        }

        db.Employees.Add(Employee.Create(_tenantA, _companyA1, bp, new DateOnly(2026, 1, 1), _user));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }

    private async Task<int> ActiveFichasAsync(Guid bp)
    {
        await using var db = NewDb(_tenantA, _companyA1);
        return await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM employees WHERE tenant_id = {_tenantA} AND business_partner_id = {bp} AND is_active").SingleAsync();
    }

    private Task WaitForWaiterAsync(Guid bp)
    {
        var (ns, key) = EmployeesAdvisoryLocks.BpEmploymentKey(_tenantA, bp);
        return WaitUntilAsync(async () => await AdvisoryCountAsync(ns, key, granted: false) == 1);
    }

    private async Task<Result<bool>> RunProtectedAsync(bool revoke, Guid bp, Guid roleId, IBusinessPartnerEmploymentGuard? guard)
    {
        await using var db = NewDb(_tenantA, _companyA1);
        return revoke
            ? await RevokeHandler(db, _tenantA, _companyA1, guard).Handle(new RevokeBusinessPartnerRoleCommand(bp, roleId), default)
            : await DeactivateHandler(db, _tenantA, _companyA1, guard).Handle(new DeactivateBusinessPartnerCommand(bp), default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T05_orden1_la_creacion_toma_el_lock_primero_y_MasterData_espera_y_bloquea(bool revoke)
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var create = SimulatedCreateAsync(bp, async () =>
        {
            holding.SetResult();
            await release.Task.WaitAsync(Timeout);
        });
        await holding.Task.WaitAsync(Timeout);

        var protectedOp = RunProtectedAsync(revoke, bp, roles[RoleType.Employee], guard: null);
        await WaitForWaiterAsync(bp);
        protectedOp.IsCompleted.Should().BeFalse("MasterData espera el lock de la creación");

        release.SetResult();
        (await create.WaitAsync(Timeout)).Should().BeTrue("la creación vio BP y rol activos");
        var result = await protectedOp.WaitAsync(Timeout);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.MasterData.BpHasActiveEmployment);
        var s = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
        (s.BpActive, s.RoleActive, s.Fichas).Should().Be((true, true, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T05_orden2_MasterData_toma_el_lock_primero_y_la_creacion_espera_y_detecta_BP_o_rol_inactivo(bool revoke)
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);
        // Guardia real envuelta: se construye sobre el contexto del propio handler.
        await using var handlerDb = NewDb(_tenantA, _companyA1);
        var guard = new PausingGuard(new BusinessPartnerEmploymentGuard(handlerDb));

        var protectedOp = revoke
            ? RevokeHandler(handlerDb, _tenantA, _companyA1, guard)
                .Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Employee]), default)
            : DeactivateHandler(handlerDb, _tenantA, _companyA1, guard)
                .Handle(new DeactivateBusinessPartnerCommand(bp), default);
        await guard.Checked.Task.WaitAsync(Timeout); // MasterData tiene el lock y ya comprobó: sin ficha

        var create = SimulatedCreateAsync(bp);
        await WaitForWaiterAsync(bp);
        create.IsCompleted.Should().BeFalse("la creación espera el lock de MasterData");

        guard.Proceed.SetResult();
        (await protectedOp.WaitAsync(Timeout)).IsSuccess.Should().BeTrue();
        (await create.WaitAsync(Timeout)).Should().BeFalse("la creación observa el BP inactivo o el rol revocado");

        var s = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
        s.Fichas.Should().Be(0);
        (revoke ? s.RoleActive : s.BpActive).Should().BeFalse();
        (await ActiveFichasAsync(bp)).Should().Be(0);
    }

    // ── M1: aislamiento de la transacción ────────────────────────────────────

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted, true)]
    [InlineData(IsolationLevel.RepeatableRead, false)]
    [InlineData(IsolationLevel.Serializable, false)]
    public async Task M1_el_lock_solo_admite_READ_COMMITTED_y_rechaza_lo_demas_antes_de_adquirirlo(
        IsolationLevel isolation, bool allowed)
    {
        var bp = Guid.NewGuid();
        var (ns, key) = EmployeesAdvisoryLocks.BpEmploymentKey(_tenantA, bp);
        await using var db = NewDb(_tenantA, _companyA1);
        await using var tx = await db.Database.BeginTransactionAsync(isolation);
        var acquire = () => new BusinessPartnerEmploymentGuard(db).AcquireBpEmploymentLockAsync(_tenantA, bp);

        if (allowed)
        {
            await acquire();
            (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(1);
        }
        else
        {
            var ex = await FluentActions.Awaiting(acquire).Should().ThrowAsync<InvalidOperationException>();
            ex.Which.Message.Should().Contain("READ COMMITTED");
            (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(0, "se rechaza antes de adquirir el lock");
            (await AdvisoryCountAsync(ns, key, granted: false)).Should().Be(0);
        }

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData(false, IsolationLevel.RepeatableRead)]
    [InlineData(false, IsolationLevel.Serializable)]
    [InlineData(true, IsolationLevel.RepeatableRead)]
    [InlineData(true, IsolationLevel.Serializable)]
    public async Task M1_los_handlers_no_modifican_datos_ni_tocan_la_transaccion_ajena_incompatible(
        bool revoke, IsolationLevel isolation)
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);
        var (ns, key) = EmployeesAdvisoryLocks.BpEmploymentKey(_tenantA, bp);

        await using var db = NewDb(_tenantA, _companyA1);
        await using var external = await db.Database.BeginTransactionAsync(isolation);

        var act = () => revoke
            ? RevokeHandler(db, _tenantA, _companyA1).Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Employee]), default)
            : DeactivateHandler(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), default);
        await FluentActions.Awaiting(act).Should().ThrowAsync<InvalidOperationException>();

        // La transacción ajena sigue abierta y utilizable: el handler no la confirmó, revirtió ni dispuso.
        db.Database.CurrentTransaction.Should().BeSameAs(external);
        (await db.Database.SqlQuery<int>($"SELECT 1 AS \"Value\"").SingleAsync()).Should().Be(1);
        (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(0, "no se adquirió el lock");

        await external.RollbackAsync();
        var s = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
        (s.BpActive, s.RoleActive, s.Fichas).Should().Be((true, true, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task M1_con_transaccion_ambiental_READ_COMMITTED_el_handler_no_la_confirma_ni_la_revierte(bool revoke)
    {
        foreach (var commit in new[] { false, true })
        {
            var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);
            await using var db = NewDb(_tenantA, _companyA1);
            await using var external = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            var result = revoke
                ? await RevokeHandler(db, _tenantA, _companyA1).Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Employee]), default)
                : await DeactivateHandler(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), default);

            result.IsSuccess.Should().BeTrue(result.Error);
            db.Database.CurrentTransaction.Should().BeSameAs(external);
            // Sin confirmar: otra conexión sigue viendo el estado original.
            var before = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
            (before.BpActive, before.RoleActive).Should().Be((true, true), "el handler no confirma la transacción ajena");

            if (commit) await external.CommitAsync(); else await external.RollbackAsync();
            var after = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
            (revoke ? after.RoleActive : after.BpActive).Should().Be(!commit);
        }
    }

    // ── L1 / L2: excepciones y cancelación ───────────────────────────────────

    private async Task AssertLockFreeAsync(Guid bp)
    {
        var (ns, key) = EmployeesAdvisoryLocks.BpEmploymentKey(_tenantA, bp);
        (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(0);
        // Y una transacción nueva lo adquiere de inmediato.
        await using var db = NewDb(_tenantA, _companyA1);
        await using var tx = await db.Database.BeginTransactionAsync();
        await new BusinessPartnerEmploymentGuard(db).AcquireBpEmploymentLockAsync(_tenantA, bp).WaitAsync(Timeout);
        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L1_excepcion_de_negocio_tras_el_lock_revierte_la_transaccion_propia_y_libera_el_lock(bool revoke)
    {
        // BP ya inactivo / rol ya revocado y sin ficha: la guardia pasa y el dominio rechaza DESPUÉS del lock.
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);
        await using (var setup = NewDb(_tenantA, _companyA1))
        {
            var ok = revoke
                ? await RevokeHandler(setup, _tenantA, _companyA1).Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Employee]), default)
                : await DeactivateHandler(setup, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), default);
            ok.IsSuccess.Should().BeTrue(ok.Error);
        }
        var before = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);

        await using var db = NewDb(_tenantA, _companyA1);
        var act = () => revoke
            ? RevokeHandler(db, _tenantA, _companyA1).Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Employee]), default)
            : DeactivateHandler(db, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), default);
        await FluentActions.Awaiting(act).Should().ThrowAsync<ERP.Domain.Exceptions.DomainRuleViolationException>();

        db.Database.CurrentTransaction.Should().BeNull("la transacción propia se revierte y se dispone");
        await AssertLockFreeAsync(bp);
        (await StateAsync(_tenantA, bp, roles[RoleType.Employee])).Should().Be(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_cancelacion_con_el_lock_tomado_no_persiste_y_libera_el_lock(bool revoke)
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);
        var (ns, key) = EmployeesAdvisoryLocks.BpEmploymentKey(_tenantA, bp);
        using var cts = new CancellationTokenSource();
        var handlerDb = NewDb(_tenantA, _companyA1);
        var guard = new PausingGuard(new BusinessPartnerEmploymentGuard(handlerDb));

        var op = revoke
            ? RevokeHandler(handlerDb, _tenantA, _companyA1, guard).Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Employee]), cts.Token)
            : DeactivateHandler(handlerDb, _tenantA, _companyA1, guard).Handle(new DeactivateBusinessPartnerCommand(bp), cts.Token);
        await guard.Checked.Task.WaitAsync(Timeout);
        (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(1, "el lock está tomado cuando se cancela");

        cts.Cancel();
        await FluentActions.Awaiting(() => op.WaitAsync(Timeout)).Should().ThrowAsync<OperationCanceledException>();

        var s = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
        (s.BpActive, s.RoleActive, s.Fichas).Should().Be((true, true, 0), "la cancelación no persiste nada");

        // Ciclo de vida oficial: lo más tarde, al disponer el contexto (conexión/transacción) el lock se libera.
        await handlerDb.DisposeAsync();
        await WaitUntilAsync(async () => await AdvisoryCountAsync(ns, key, granted: true) == 0);
        await AssertLockFreeAsync(bp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_cancelacion_mientras_espera_el_lock_retira_la_espera_y_no_persiste(bool revoke)
    {
        var (bp, roles) = await NewBpAsync(_tenantA, RoleType.Employee);
        var (ns, key) = EmployeesAdvisoryLocks.BpEmploymentKey(_tenantA, bp);

        await using var holderDb = NewDb(_tenantA, _companyA1);
        await using var holder = await holderDb.Database.BeginTransactionAsync();
        await new BusinessPartnerEmploymentGuard(holderDb).AcquireBpEmploymentLockAsync(_tenantA, bp);

        using var cts = new CancellationTokenSource();
        await using var waiterDb = NewDb(_tenantA, _companyA1);
        var op = revoke
            ? RevokeHandler(waiterDb, _tenantA, _companyA1).Handle(new RevokeBusinessPartnerRoleCommand(bp, roles[RoleType.Employee]), cts.Token)
            : DeactivateHandler(waiterDb, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), cts.Token);
        await WaitUntilAsync(async () => await AdvisoryCountAsync(ns, key, granted: false) == 1);

        cts.Cancel();
        await FluentActions.Awaiting(() => op.WaitAsync(Timeout)).Should().ThrowAsync<OperationCanceledException>();
        await WaitUntilAsync(async () => await AdvisoryCountAsync(ns, key, granted: false) == 0);
        (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(1, "el titular conserva su lock");

        await holder.CommitAsync();
        var s = await StateAsync(_tenantA, bp, roles[RoleType.Employee]);
        (s.BpActive, s.RoleActive).Should().Be((true, true));
        await AssertLockFreeAsync(bp);
    }
}
