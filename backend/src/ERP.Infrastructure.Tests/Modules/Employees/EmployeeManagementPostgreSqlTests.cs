using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.MasterData.UseCases.DeactivateBusinessPartner;
using ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.EventHandlers;
using ERP.Application.Modules.Employees.UseCases;
using ERP.Domain.Audit;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Enums;
using ERP.Domain.Modules.Employees.Events;
using ERP.Domain.Modules.Employees.Interfaces;
using ERP.Domain.Modules.Employees.Services;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Employees;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Employees;

/// <summary>
/// EMP-E4 — alta y edición de la ficha, especialidades, catálogos, consultas, aislamiento
/// tenant/empresa (observación B), inmutabilidad de CompanyId/TenantId (observación A), concurrencia
/// (T-04, T-05 con los handlers reales, rowVersion) y auditoría (T-11) sobre PostgreSQL 16 real
/// (Testcontainers; base desechable). Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class EmployeeManagementPostgreSqlTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly DateOnly Start = new(2026, 3, 1);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_employee_management_test")
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

    private sealed class DirectAudit(ErpDbContext db) : IAuditService
    {
        public Task RecordAsync<TAudit>(TAudit record, CancellationToken ct = default)
            where TAudit : AuditRecordBase => db.Set<TAudit>().AddAsync(record, ct).AsTask();
    }

    /// <summary>Publica los eventos de Employees a los handlers de auditoría reales (misma transacción).</summary>
    private sealed class AuditPublisher(Func<ErpDbContext> db, Func<IAuditContext> context) : IPublisher
    {
        public Task Publish(object notification, CancellationToken ct = default)
        {
            var audit = new DirectAudit(db());
            return notification switch
            {
                EmployeeCreatedEvent e => new EmployeeAuditHandler(audit, context()).Handle(e, ct),
                EmployeeFieldChangedEvent e => new EmployeeAuditHandler(audit, context()).Handle(e, ct),
                EmployeeCatalogChangedEvent e => new EmployeeCatalogAuditHandler(audit, context()).Handle(e, ct),
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

    private CreateEmployeeHandler CreateH(
        ErpDbContext db, Guid tenant, Guid company,
        IBusinessPartnerEmploymentGuard? guard = null, IEmployeeCatalogRepository? catalogs = null) =>
        new(new EmployeeRepository(db), catalogs ?? new EmployeeCatalogRepository(db),
            new EmployeeBusinessPartnerReader(db), guard ?? new BusinessPartnerEmploymentGuard(db),
            new IdentificationUsageValidator(db), new EmployeeQueries(db),
            new PostgresDatabaseExceptionTranslator(), Ctx(tenant, company, _user), new UnitOfWork(db));

    private UpdateEmployeeHandler UpdateH(
        ErpDbContext db, Guid tenant, Guid company, IEmployeeCatalogRepository? catalogs = null) =>
        new(new EmployeeRepository(db), catalogs ?? new EmployeeCatalogRepository(db),
            new EmployeeQueries(db), Ctx(tenant, company, _user), new UnitOfWork(db));

    private SetEmployeeSpecialtiesHandler SpecialtiesH(ErpDbContext db, Guid tenant, Guid company) =>
        new(new EmployeeRepository(db), new EmployeeCatalogRepository(db),
            new EmployeeQueries(db), Ctx(tenant, company, _user), new UnitOfWork(db));

    private DeactivateBusinessPartnerHandler DeactivateH(
        ErpDbContext db, Guid tenant, Guid company, IBusinessPartnerEmploymentGuard? guard = null) =>
        new(new BusinessPartnerRepository(db), Ctx(tenant, company, _user),
            guard ?? new BusinessPartnerEmploymentGuard(db), new UnitOfWork(db));

    private RevokeBusinessPartnerRoleHandler RevokeH(
        ErpDbContext db, Guid tenant, Guid company, IBusinessPartnerEmploymentGuard? guard = null) =>
        new(new BusinessPartnerRoleRepository(db), Ctx(tenant, company, _user),
            guard ?? new BusinessPartnerEmploymentGuard(db), new UnitOfWork(db));

    private async Task<Result<EmployeeDto>> CreateAsync(
        Guid bp, Guid? job = null, Guid? profession = null, Guid[]? specialties = null,
        Guid? tenant = null, Guid? company = null, DateOnly? start = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await CreateH(db, t, c).Handle(
            new CreateEmployeeCommand(bp, start ?? Start, job, profession, specialties), default);
    }

    private async Task<Result<EmployeeDto>> UpdateAsync(
        Guid id, uint version, OptionalField<Guid?> job = default, OptionalField<Guid?> profession = default,
        Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await UpdateH(db, t, c).Handle(new UpdateEmployeeCommand(id, version, job, profession), default);
    }

    private async Task<Result<EmployeeDto>> SetSpecialtiesAsync(
        Guid id, uint version, Guid[] ids, Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await SpecialtiesH(db, t, c).Handle(new SetEmployeeSpecialtiesCommand(id, version, ids), default);
    }

    private async Task<EmployeeDto> GetAsync(Guid id, Guid? tenant = null, Guid? company = null)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c);
        var result = await new GetEmployeeByIdHandler(new EmployeeQueries(db), Ctx(t, c, _user))
            .Handle(new GetEmployeeByIdQuery(id), default);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!;
    }

    private static string ValidCedula(int seq)
    {
        var digits = $"171{seq % 1_000_000:000000}".Select(ch => ch - '0').ToArray();
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var p = digits[i] * (i % 2 == 0 ? 2 : 1);
            sum += p > 9 ? p - 9 : p;
        }
        return string.Concat(digits) + ((10 - sum % 10) % 10);
    }

    /// <summary>BP con identificación cédula (05) por defecto y los roles indicados.</summary>
    private async Task<(Guid Bp, Guid? EmployeeRoleId)> NewBpAsync(
        Guid? tenant = null, string idType = "05", bool employeeRole = true, bool active = true,
        bool revokedRole = false)
    {
        var t = tenant ?? _tenantA;
        await using var db = NewDb(t, Guid.Empty);
        var n = Interlocked.Increment(ref _seq);
        var number = idType == "05" ? ValidCedula(n) : idType == "06" ? $"PAS{n:00000}" : $"1791352{n:000000}"[..13];
        var bp = BusinessPartner.Create(t, idType, number, idType == "06" ? 1 : null, $"Persona {n}", _user);
        db.BusinessPartners.Add(bp);
        Guid? roleId = null;
        if (employeeRole)
        {
            var role = BusinessPartnerRole.Create(t, bp.Id, RoleType.Employee, _user);
            db.BusinessPartnerRoles.Add(role);
            roleId = role.Id;
            if (revokedRole)
                role.Revoke(_user);
        }
        if (!active)
            bp.Deactivate(_user);
        await db.SaveChangesAsync();
        return (bp.Id, roleId);
    }

    private async Task<Guid> NewCatalogAsync(
        EmployeeCatalogType type, Guid? tenant = null, Guid? company = null, string? name = null, bool active = true)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c);
        var entry = EmployeeCatalogFactory.Create(type, t, c, name ?? $"{type} {Guid.NewGuid():N}", _user);
        db.Add(entry);
        await db.SaveChangesAsync();
        if (!active)
        {
            entry.Deactivate(_user);
            await db.SaveChangesAsync();
        }
        return entry.Id;
    }

    private async Task<T> ScalarAsync<T>(FormattableString sql)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        return await db.Database.SqlQuery<T>(sql).SingleAsync();
    }

    private Task<int> CountEmployeesAsync(Guid bp) =>
        ScalarAsync<int>($"SELECT count(*)::int AS \"Value\" FROM employees WHERE business_partner_id = {bp}");

    private Task<int> CountPeriodsAsync(Guid employeeId) =>
        ScalarAsync<int>($"SELECT count(*)::int AS \"Value\" FROM employee_employment_periods WHERE employee_id = {employeeId}");

    private Task<int> CountLinksAsync(Guid employeeId, bool? active = null) =>
        active is null
            ? ScalarAsync<int>($"SELECT count(*)::int AS \"Value\" FROM employee_specialty_links WHERE employee_id = {employeeId}")
            : ScalarAsync<int>($"SELECT count(*)::int AS \"Value\" FROM employee_specialty_links WHERE employee_id = {employeeId} AND is_active = {active}");

    private async Task<Guid> NewEmployeeAsync(
        Guid? job = null, Guid? profession = null, Guid[]? specialties = null,
        Guid? tenant = null, Guid? company = null)
    {
        var (bp, _) = await NewBpAsync(tenant);
        var r = await CreateAsync(bp, job, profession, specialties, tenant, company);
        r.IsSuccess.Should().BeTrue(r.Error);
        return r.Value!.Id;
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

    // ── alta (EMP-BR-01/02/07, P-01) ─────────────────────────────────────────

    [Fact]
    public async Task Alta_valida_crea_la_ficha_y_el_primer_periodo_con_proyeccion_minima_del_BP()
    {
        var job = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var prof = await NewCatalogAsync(EmployeeCatalogType.Profession);
        var spec = await NewCatalogAsync(EmployeeCatalogType.Specialty);
        var (bp, _) = await NewBpAsync();

        var r = await CreateAsync(bp, job, prof, [spec]);

        r.IsSuccess.Should().BeTrue(r.Error);
        var dto = r.Value!;
        dto.CompanyId.Should().Be(_companyA1);
        dto.IsActive.Should().BeTrue();
        dto.BusinessPartner.Id.Should().Be(bp);
        dto.BusinessPartner.IdentificationType.Should().Be("05");
        dto.BusinessPartner.DisplayName.Should().StartWith("Persona");
        dto.JobPosition!.Id.Should().Be(job);
        dto.Profession!.Id.Should().Be(prof);
        dto.Specialties.Should().ContainSingle(s => s.Id == spec);
        dto.Periods.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { StartDate = Start, EndDate = (DateOnly?)null });
        dto.IdentityUser.Should().BeNull();
        dto.Assignments.Should().BeEmpty();
        dto.RowVersion.Should().BeGreaterThan(0u);
        (await CountEmployeesAsync(bp)).Should().Be(1);
        (await CountPeriodsAsync(dto.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Alta_sin_cargo_ni_profesion_es_valida_T12_P01_y_acepta_pasaporte()
    {
        var (bp, _) = await NewBpAsync(idType: "06");
        var r = await CreateAsync(bp);
        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.JobPosition.Should().BeNull();
        r.Value.Profession.Should().BeNull();
        r.Value.Specialties.Should().BeEmpty();
    }

    [Fact]
    public async Task Alta_duplicada_devuelve_EMPLOYEE_ALREADY_EXISTS_y_no_crea_otra_ficha()
    {
        var (bp, _) = await NewBpAsync();
        (await CreateAsync(bp)).IsSuccess.Should().BeTrue();

        var second = await CreateAsync(bp);

        second.IsSuccess.Should().BeFalse();
        second.Code.Should().Be(ApiResponseCodes.Employees.AlreadyExists);
        (await CountEmployeesAsync(bp)).Should().Be(1);
    }

    [Fact]
    public async Task Alta_del_mismo_BP_en_otra_empresa_del_tenant_es_valida()
    {
        var (bp, _) = await NewBpAsync();
        (await CreateAsync(bp)).IsSuccess.Should().BeTrue();
        var other = await CreateAsync(bp, company: _companyA2);
        other.IsSuccess.Should().BeTrue(other.Error);
        other.Value!.CompanyId.Should().Be(_companyA2);
    }

    [Fact]
    public async Task Alta_BP_inactivo_rol_ausente_o_revocado_y_RUC_se_rechazan_T07()
    {
        var inactive = (await NewBpAsync(active: false)).Bp;
        (await CreateAsync(inactive)).Code.Should().Be(ApiResponseCodes.Employees.BpInactive);

        var noRole = (await NewBpAsync(employeeRole: false)).Bp;
        (await CreateAsync(noRole)).Code.Should().Be(ApiResponseCodes.Employees.BpRoleMissing);

        var revoked = (await NewBpAsync(revokedRole: true)).Bp;
        (await CreateAsync(revoked)).Code.Should().Be(ApiResponseCodes.Employees.BpRoleMissing);

        var ruc = (await NewBpAsync(idType: "04")).Bp;
        (await CreateAsync(ruc)).Code.Should().Be(ApiResponseCodes.Employees.IdentificationNotAllowed);

        foreach (var bp in new[] { inactive, noRole, revoked, ruc })
            (await CountEmployeesAsync(bp)).Should().Be(0);
    }

    [Fact]
    public async Task Alta_con_fecha_por_defecto_devuelve_EMPLOYEE_INVALID_DATE()
    {
        var (bp, _) = await NewBpAsync();
        var r = await CreateAsync(bp, start: default(DateOnly));
        r.Code.Should().Be(ApiResponseCodes.Employees.InvalidDate);
        (await CountEmployeesAsync(bp)).Should().Be(0);
    }

    [Fact]
    public async Task Alta_acepta_fechas_pasadas_y_futuras_sin_cotas_D1()
    {
        foreach (var date in new[] { new DateOnly(1990, 1, 1), new DateOnly(2035, 12, 31) })
        {
            var (bp, _) = await NewBpAsync();
            (await CreateAsync(bp, start: date)).IsSuccess.Should().BeTrue();
        }
    }

    // ── observación B: referencias y aislamiento ─────────────────────────────

    [Fact]
    public async Task Alta_con_BP_de_otro_tenant_o_inexistente_devuelve_NOT_FOUND_sin_persistir()
    {
        var (foreign, _) = await NewBpAsync(_tenantB);
        var foreignResult = await CreateAsync(foreign);
        var missingResult = await CreateAsync(Guid.NewGuid());

        foreignResult.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        missingResult.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        foreignResult.Error.Should().Be(missingResult.Error, "indistinguibles");
        (await CountEmployeesAsync(foreign)).Should().Be(0);
    }

    [Theory]
    [InlineData(EmployeeCatalogType.JobPosition)]
    [InlineData(EmployeeCatalogType.Profession)]
    [InlineData(EmployeeCatalogType.Specialty)]
    public async Task Alta_con_catalogo_de_otra_empresa_otro_tenant_o_inexistente_devuelve_422_sin_persistir(
        EmployeeCatalogType type)
    {
        var otherCompany = await NewCatalogAsync(type, company: _companyA2);
        var otherTenant = await NewCatalogAsync(type, _tenantB, _companyB1);
        var invalidReferences = new[] { otherCompany, otherTenant, Guid.NewGuid() };
        var messages = new List<string?>();

        foreach (var reference in invalidReferences)
        {
            var (bp, _) = await NewBpAsync();
            var r = await CreateAsync(
                bp,
                job: type == EmployeeCatalogType.JobPosition ? reference : null,
                profession: type == EmployeeCatalogType.Profession ? reference : null,
                specialties: type == EmployeeCatalogType.Specialty ? [reference] : null);
            r.IsSuccess.Should().BeFalse();
            r.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
            messages.Add(r.Error);
            (await CountEmployeesAsync(bp)).Should().Be(0, "sin persistencia parcial");
        }
        messages.Distinct().Should().HaveCount(1, "no se distingue inexistente de fuera de ámbito");
    }

    [Fact]
    public async Task Alta_con_catalogo_propio_inactivo_devuelve_EMPLOYEE_CATALOG_INACTIVE()
    {
        var job = await NewCatalogAsync(EmployeeCatalogType.JobPosition, active: false);
        var spec = await NewCatalogAsync(EmployeeCatalogType.Specialty, active: false);
        var (bp, _) = await NewBpAsync();

        (await CreateAsync(bp, job: job)).Code.Should().Be(ApiResponseCodes.Employees.CatalogInactive);
        (await CreateAsync(bp, specialties: [spec])).Code.Should().Be(ApiResponseCodes.Employees.CatalogInactive);
        (await CountEmployeesAsync(bp)).Should().Be(0);
    }

    [Fact]
    public async Task Ficha_de_otra_empresa_o_tenant_en_GET_PATCH_y_PUT_es_EMPLOYEE_NOT_FOUND_indistinguible()
    {
        var id = await NewEmployeeAsync();
        var version = (await GetAsync(id)).RowVersion;

        var inexistent = await UpdateAsync(Guid.NewGuid(), version);
        var otherCompany = await UpdateAsync(id, version, company: _companyA2);
        var otherTenant = await UpdateAsync(id, version, tenant: _tenantB, company: _companyB1);
        var putCompany = await SetSpecialtiesAsync(id, version, [], company: _companyA2);
        var putTenant = await SetSpecialtiesAsync(id, version, [], tenant: _tenantB, company: _companyB1);

        foreach (var r in new[] { inexistent, otherCompany, otherTenant, putCompany, putTenant })
        {
            r.IsSuccess.Should().BeFalse();
            r.Code.Should().Be(ApiResponseCodes.Employees.NotFound);
            r.Error.Should().Be(inexistent.Error);
        }

        await using var db = NewDb(_tenantA, _companyA2);
        var get = await new GetEmployeeByIdHandler(new EmployeeQueries(db), Ctx(_tenantA, _companyA2, _user))
            .Handle(new GetEmployeeByIdQuery(id), default);
        get.Code.Should().Be(ApiResponseCodes.Employees.NotFound);
        (await GetAsync(id)).RowVersion.Should().Be(version, "ninguna escritura ajena");
    }

    [Fact]
    public async Task Los_repositorios_aplican_tenant_y_empresa_como_predicado_propio_T13()
    {
        var id = await NewEmployeeAsync();
        var catalog = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        await using var db = NewDb(_tenantA, _companyA1); // el filtro global deja pasar estos registros
        var employees = new EmployeeRepository(db);
        var catalogs = new EmployeeCatalogRepository(db);

        (await employees.GetByIdAsync(_tenantA, _companyA1, id)).Should().NotBeNull();
        (await employees.GetByIdAsync(_tenantB, _companyA1, id)).Should().BeNull("predicado de tenant");
        (await employees.GetByIdAsync(_tenantA, _companyA2, id)).Should().BeNull("predicado de empresa");
        (await catalogs.GetByIdAsync(EmployeeCatalogType.JobPosition, _tenantA, _companyA1, catalog)).Should().NotBeNull();
        (await catalogs.GetByIdAsync(EmployeeCatalogType.JobPosition, _tenantB, _companyA1, catalog)).Should().BeNull();
        (await catalogs.GetByIdAsync(EmployeeCatalogType.JobPosition, _tenantA, _companyA2, catalog)).Should().BeNull();

        await using var tx = await db.Database.BeginTransactionAsync();
        (await catalogs.ResolveForAssignmentAsync(EmployeeCatalogType.JobPosition, _tenantA, _companyA2, [catalog]))
            .Should().BeEmpty("empresa distinta");
        (await catalogs.ResolveForAssignmentAsync(EmployeeCatalogType.JobPosition, _tenantB, _companyA1, [catalog]))
            .Should().BeEmpty("tenant distinto");
        (await new EmployeeQueries(db).GetByIdAsync(_tenantB, _companyA1, id)).Should().BeNull();
        (await new EmployeeQueries(db).GetByIdAsync(_tenantA, _companyA2, id)).Should().BeNull();
        await tx.RollbackAsync();
    }

    // ── observación A: inmutabilidad de CompanyId y TenantId ─────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompanyId_y_TenantId_no_se_pueden_modificar_por_EF_y_no_se_persiste_nada(bool company)
    {
        var id = await NewEmployeeAsync();
        await using var db = NewDb(_tenantA, _companyA1);
        var employee = await db.Employees.SingleAsync(e => e.Id == id);
        var entry = db.Entry(employee);
        if (company) entry.Property(e => e.CompanyId).CurrentValue = _companyA2;
        else entry.Property(e => e.TenantId).CurrentValue = _tenantB;

        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();

        var row = await ScalarAsync<string>($"SELECT tenant_id::text || '|' || company_id::text AS \"Value\" FROM employees WHERE id = {id}");
        row.Should().Be($"{_tenantA}|{_companyA1}");
    }

    [Fact]
    public async Task CompanyId_de_un_catalogo_tambien_es_inmutable()
    {
        var id = await NewCatalogAsync(EmployeeCatalogType.Specialty);
        await using var db = NewDb(_tenantA, _companyA1);
        var entry = await db.EmployeeSpecialties.SingleAsync(e => e.Id == id);
        db.Entry(entry).Property(e => e.CompanyId).CurrentValue = _companyA2;
        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
    }

    // ── edición de cargo y profesión (D-2, D-3) ──────────────────────────────

    [Fact]
    public async Task Patch_cambia_cargo_y_profesion_y_distingue_omitido_de_null_explicito()
    {
        var job = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var prof = await NewCatalogAsync(EmployeeCatalogType.Profession);
        var id = await NewEmployeeAsync();

        var set = await UpdateAsync(id, (await GetAsync(id)).RowVersion, OptionalField<Guid?>.Of(job), OptionalField<Guid?>.Of(prof));
        set.IsSuccess.Should().BeTrue(set.Error);
        (set.Value!.JobPosition!.Id, set.Value.Profession!.Id).Should().Be((job, prof));

        // job omitido: se conserva; profesión null explícito: se elimina (D-3)
        var clear = await UpdateAsync(id, set.Value.RowVersion, default, OptionalField<Guid?>.Of(null));
        clear.IsSuccess.Should().BeTrue(clear.Error);
        clear.Value!.JobPosition!.Id.Should().Be(job);
        clear.Value.Profession.Should().BeNull();
        clear.Value.RowVersion.Should().NotBe(set.Value.RowVersion);
    }

    [Fact]
    public async Task Patch_conserva_un_catalogo_inactivo_ya_asignado_pero_rechaza_asignar_uno_inactivo_D2()
    {
        var job = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var otherInactive = await NewCatalogAsync(EmployeeCatalogType.JobPosition, active: false);
        var id = await NewEmployeeAsync(job: job);
        await using (var db = NewDb(_tenantA, _companyA1))
        {
            var h = new SetEmployeeCatalogEntryActiveHandler(new EmployeeCatalogRepository(db), Ctx(_tenantA, _companyA1, _user));
            (await h.Handle(new SetEmployeeCatalogEntryActiveCommand(EmployeeCatalogType.JobPosition, job, await CatalogVersionAsync(EmployeeCatalogType.JobPosition, job), false), default))
                .IsSuccess.Should().BeTrue();
        }

        var version = (await GetAsync(id)).RowVersion;
        var keep = await UpdateAsync(id, version, OptionalField<Guid?>.Of(job));
        keep.IsSuccess.Should().BeTrue("misma referencia inactiva: no se modifica");
        keep.Value!.JobPosition!.IsActive.Should().BeFalse();

        var change = await UpdateAsync(id, keep.Value.RowVersion, OptionalField<Guid?>.Of(otherInactive));
        change.Code.Should().Be(ApiResponseCodes.Employees.CatalogInactive);
        (await GetAsync(id)).JobPosition!.Id.Should().Be(job, "sin cambios parciales");
    }

    [Fact]
    public async Task Patch_con_referencias_invalidas_devuelve_422_y_no_persiste()
    {
        var id = await NewEmployeeAsync();
        var version = (await GetAsync(id)).RowVersion;
        var foreign = await NewCatalogAsync(EmployeeCatalogType.Profession, company: _companyA2);

        var r = await UpdateAsync(id, version, OptionalField<Guid?>.Of(Guid.NewGuid()), OptionalField<Guid?>.Of(foreign));

        r.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        var after = await GetAsync(id);
        after.RowVersion.Should().Be(version);
        (after.JobPosition, after.Profession).Should().Be((null, null));
    }

    // ── especialidades (EMP-BR-06) ───────────────────────────────────────────

    [Fact]
    public async Task Especialidades_retirar_y_reasignar_alterna_is_active_en_la_misma_fila()
    {
        var a = await NewCatalogAsync(EmployeeCatalogType.Specialty);
        var b = await NewCatalogAsync(EmployeeCatalogType.Specialty);
        var id = await NewEmployeeAsync();

        var first = await SetSpecialtiesAsync(id, (await GetAsync(id)).RowVersion, [a, b]);
        first.IsSuccess.Should().BeTrue(first.Error);
        first.Value!.Specialties.Select(s => s.Id).Should().BeEquivalentTo([a, b]);

        var retire = await SetSpecialtiesAsync(id, first.Value.RowVersion, [b]);
        retire.Value!.Specialties.Select(s => s.Id).Should().BeEquivalentTo([b]);
        (await CountLinksAsync(id)).Should().Be(2, "la fila se conserva");
        (await CountLinksAsync(id, active: true)).Should().Be(1);

        var back = await SetSpecialtiesAsync(id, retire.Value.RowVersion, [a, b]);
        back.Value!.Specialties.Select(s => s.Id).Should().BeEquivalentTo([a, b]);
        (await CountLinksAsync(id)).Should().Be(2, "se reactiva la misma fila (UX-SPL-01)");

        var clear = await SetSpecialtiesAsync(id, back.Value.RowVersion, []);
        clear.Value!.Specialties.Should().BeEmpty();
        (await CountLinksAsync(id)).Should().Be(2);
    }

    [Fact]
    public async Task Especialidades_nueva_inactiva_o_ajena_se_rechaza_sin_cambios_parciales_y_la_activa_inactivada_se_conserva()
    {
        var keep = await NewCatalogAsync(EmployeeCatalogType.Specialty);
        var inactive = await NewCatalogAsync(EmployeeCatalogType.Specialty, active: false);
        var foreign = await NewCatalogAsync(EmployeeCatalogType.Specialty, company: _companyA2);
        var id = await NewEmployeeAsync(specialties: [keep]);

        var version = (await GetAsync(id)).RowVersion;
        (await SetSpecialtiesAsync(id, version, [keep, inactive])).Code.Should().Be(ApiResponseCodes.Employees.CatalogInactive);
        (await SetSpecialtiesAsync(id, version, [keep, foreign])).Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        (await SetSpecialtiesAsync(id, version, [keep, Guid.NewGuid()])).Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        (await CountLinksAsync(id)).Should().Be(1);
        (await GetAsync(id)).RowVersion.Should().Be(version);

        await using (var db = NewDb(_tenantA, _companyA1))
            await new SetEmployeeCatalogEntryActiveHandler(new EmployeeCatalogRepository(db), Ctx(_tenantA, _companyA1, _user))
                .Handle(new SetEmployeeCatalogEntryActiveCommand(EmployeeCatalogType.Specialty, keep, await CatalogVersionAsync(EmployeeCatalogType.Specialty, keep), false), default);

        var kept = await SetSpecialtiesAsync(id, version, [keep]);
        kept.IsSuccess.Should().BeTrue("la especialidad ya activa en la ficha se conserva aunque el catálogo la inactive (D-2)");
        kept.Value!.Specialties.Should().ContainSingle(s => s.Id == keep && !s.IsActive);
    }

    // ── consultas ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lista_pagina_con_skip_take_filtra_y_solo_muestra_la_empresa_operativa()
    {
        var job = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var spec = await NewCatalogAsync(EmployeeCatalogType.Specialty);
        var company = (await NewCompanyAsync()).Id; // empresa aislada: su lista parte vacía
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var jobId = await NewCatalogAsync(EmployeeCatalogType.JobPosition, company: company);
            var specId = await NewCatalogAsync(EmployeeCatalogType.Specialty, company: company);
            ids.Add(await NewEmployeeAsync(i == 0 ? jobId : null, null, i == 0 ? [specId] : null, company: company));
            if (i == 0) { job = jobId; spec = specId; }
        }
        await NewEmployeeAsync(); // empresa A1: no debe aparecer en `company`

        async Task<PagedResult<EmployeeListItemDto>> List(ListEmployeesQuery q)
        {
            await using var db = NewDb(_tenantA, company);
            var r = await new ListEmployeesHandler(new EmployeeQueries(db), Ctx(_tenantA, company, _user)).Handle(q, default);
            r.IsSuccess.Should().BeTrue(r.Error);
            return r.Value!;
        }

        var all = await List(new ListEmployeesQuery());
        all.TotalCount.Should().Be(3);
        all.Items.Select(i => i.Id).Should().BeEquivalentTo(ids);
        all.Items.Should().OnlyContain(i => i.CompanyId == company);

        var page = await List(new ListEmployeesQuery(Skip: 2, Take: 2));
        page.Items.Should().HaveCount(1);
        page.TotalCount.Should().Be(3);
        page.PageSize.Should().Be(2);

        (await List(new ListEmployeesQuery(JobPositionId: job))).Items.Should().ContainSingle(i => i.Id == ids[0]);
        (await List(new ListEmployeesQuery(SpecialtyId: spec))).Items.Should().ContainSingle(i => i.Id == ids[0]);
        (await List(new ListEmployeesQuery(IsActive: false))).TotalCount.Should().Be(0);
        (await List(new ListEmployeesQuery(Search: "Persona"))).TotalCount.Should().Be(3);
        (await List(new ListEmployeesQuery(Search: "zzz-inexistente"))).TotalCount.Should().Be(0);
    }

    private async Task<Company> NewCompanyAsync()
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        var n = Interlocked.Increment(ref _seq);
        var company = Company.CreateManaged(_tenantA, $"17900{n:00000}001", $"Aislada {n} S.A.", createdBy: _user);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    // ── catálogos (EMP-BR-15/16) ─────────────────────────────────────────────

    private async Task<Result<EmployeeCatalogEntryDto>> CatalogCreateAsync(
        EmployeeCatalogType type, string name, Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await new CreateEmployeeCatalogEntryHandler(new EmployeeCatalogRepository(db),
            new PostgresDatabaseExceptionTranslator(), Ctx(t, c, _user))
            .Handle(new CreateEmployeeCatalogEntryCommand(type, name), default);
    }

    [Theory]
    [InlineData(EmployeeCatalogType.JobPosition)]
    [InlineData(EmployeeCatalogType.Profession)]
    [InlineData(EmployeeCatalogType.Specialty)]
    public async Task Catalogo_crea_normaliza_rechaza_duplicados_y_se_aisla_por_empresa(EmployeeCatalogType type)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var created = await CatalogCreateAsync(type, $"  Médico   General {suffix} ");
        created.IsSuccess.Should().BeTrue(created.Error);
        created.Value!.Name.Should().Be($"Médico General {suffix}");
        created.Value.IsActive.Should().BeTrue();

        var dup = await CatalogCreateAsync(type, $"MEDICO general {suffix}");
        dup.Code.Should().Be(ApiResponseCodes.Employees.CatalogDuplicate);

        (await CatalogCreateAsync(type, $"medico general {suffix}", company: _companyA2)).IsSuccess
            .Should().BeTrue("la unicidad es por empresa");

        await using var db = NewDb(_tenantA, _companyA2);
        var foreign = await new GetEmployeeCatalogEntryHandler(new EmployeeCatalogRepository(db), Ctx(_tenantA, _companyA2, _user))
            .Handle(new GetEmployeeCatalogEntryQuery(type, created.Value.Id), default);
        foreign.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Catalogo_renombra_activa_e_inactiva_sin_borrar_y_la_lista_pagina()
    {
        var type = EmployeeCatalogType.JobPosition;
        var company = (await NewCompanyAsync()).Id;
        var a = (await CatalogCreateAsync(type, "Cajero", company: company)).Value!;
        var b = (await CatalogCreateAsync(type, "Bodeguero", company: company)).Value!;

        await using var db = NewDb(_tenantA, company);
        var ctx = Ctx(_tenantA, company, _user);
        var repo = new EmployeeCatalogRepository(db);

        var rename = await new RenameEmployeeCatalogEntryHandler(repo, new PostgresDatabaseExceptionTranslator(), ctx)
            .Handle(new RenameEmployeeCatalogEntryCommand(type, a.Id, await CatalogVersionAsync(type, a.Id), "Cajero Principal"), default);
        rename.Value!.Name.Should().Be("Cajero Principal");
        (await new RenameEmployeeCatalogEntryHandler(repo, new PostgresDatabaseExceptionTranslator(), ctx)
            .Handle(new RenameEmployeeCatalogEntryCommand(type, a.Id, await CatalogVersionAsync(type, a.Id), "BODEGUERO"), default))
            .Code.Should().Be(ApiResponseCodes.Employees.CatalogDuplicate);

        var setActive = new SetEmployeeCatalogEntryActiveHandler(repo, ctx);
        (await setActive.Handle(new SetEmployeeCatalogEntryActiveCommand(type, b.Id, await CatalogVersionAsync(type, b.Id), false), default)).Value!.IsActive.Should().BeFalse();
        await FluentActions.Awaiting(async () => await setActive.Handle(new SetEmployeeCatalogEntryActiveCommand(type, b.Id, await CatalogVersionAsync(type, b.Id), false), default))
            .Should().ThrowAsync<DomainRuleViolationException>();
        (await setActive.Handle(new SetEmployeeCatalogEntryActiveCommand(type, b.Id, await CatalogVersionAsync(type, b.Id), true), default)).Value!.IsActive.Should().BeTrue();

        var list = new ListEmployeeCatalogHandler(repo, ctx);
        var all = (await list.Handle(new ListEmployeeCatalogQuery(type), default)).Value!;
        all.TotalCount.Should().Be(2, "nada se elimina");
        (await list.Handle(new ListEmployeeCatalogQuery(type, Skip: 1, Take: 1), default)).Value!.Items.Should().HaveCount(1);
        (await list.Handle(new ListEmployeeCatalogQuery(type, Search: "caj"), default)).Value!.TotalCount.Should().Be(1);
        await setActive.Handle(new SetEmployeeCatalogEntryActiveCommand(type, b.Id, await CatalogVersionAsync(type, b.Id), false), default);
        (await list.Handle(new ListEmployeeCatalogQuery(type, IsActive: false), default)).Value!.TotalCount.Should().Be(1);
    }

    // ── T-04: altas simultáneas ──────────────────────────────────────────────

    [Fact]
    public async Task T04_altas_simultaneas_del_mismo_BP_dejan_una_sola_ficha_y_la_otra_ALREADY_EXISTS()
    {
        for (var round = 0; round < 3; round++)
        {
            var (bp, _) = await NewBpAsync();
            var results = await Task.WhenAll(CreateAsync(bp), CreateAsync(bp), CreateAsync(bp));

            results.Count(r => r.IsSuccess).Should().Be(1);
            results.Where(r => !r.IsSuccess).Should().OnlyContain(r => r.Code == ApiResponseCodes.Employees.AlreadyExists);
            (await CountEmployeesAsync(bp)).Should().Be(1);
        }
    }

    // ── T-05 (E4): alta real vs desactivar / revocar, ambos órdenes ──────────

    /// <summary>Decorador de la guardia que pausa tras adquirir el lock o tras comprobar la ficha activa.</summary>
    private sealed class PausingGuard(IBusinessPartnerEmploymentGuard inner, bool pauseAfterLock)
        : IBusinessPartnerEmploymentGuard
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
    public async Task T05_alta_real_contra_desactivar_o_revocar_nunca_deja_ficha_activa_con_BP_inactivo_o_sin_rol(
        bool revoke, bool createFirst)
    {
        var (bp, roleId) = await NewBpAsync();
        var (ns, key) = EmployeesAdvisoryLocks.BpEmploymentKey(_tenantA, bp);

        await using var createDb = NewDb(_tenantA, _companyA1);
        await using var otherDb = NewDb(_tenantA, _companyA1);
        // En «alta primero» la alta retiene el lock tras adquirirlo; en «baja primero» la baja lo retiene
        // tras comprobar la guardia. En ambos casos el segundo espera de verdad (pg_locks).
        var guard = new PausingGuard(
            createFirst ? new BusinessPartnerEmploymentGuard(createDb) : new BusinessPartnerEmploymentGuard(otherDb),
            pauseAfterLock: createFirst);

        Task<Result<EmployeeDto>> create;
        Task<Result<bool>> block;
        if (createFirst)
        {
            create = CreateH(createDb, _tenantA, _companyA1, guard).Handle(new CreateEmployeeCommand(bp, Start), default);
            await guard.Reached.Task.WaitAsync(Timeout);
            block = revoke
                ? RevokeH(otherDb, _tenantA, _companyA1).Handle(new RevokeBusinessPartnerRoleCommand(bp, roleId!.Value), default)
                : DeactivateH(otherDb, _tenantA, _companyA1).Handle(new DeactivateBusinessPartnerCommand(bp), default);
        }
        else
        {
            block = revoke
                ? RevokeH(otherDb, _tenantA, _companyA1, guard).Handle(new RevokeBusinessPartnerRoleCommand(bp, roleId!.Value), default)
                : DeactivateH(otherDb, _tenantA, _companyA1, guard).Handle(new DeactivateBusinessPartnerCommand(bp), default);
            await guard.Reached.Task.WaitAsync(Timeout);
            create = CreateH(createDb, _tenantA, _companyA1).Handle(new CreateEmployeeCommand(bp, Start), default);
        }

        // El segundo está esperando el lock del primero (espera efectiva, no simulada).
        await WaitUntilAsync(async () => await AdvisoryCountAsync(ns, key, granted: false) == 1);
        (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(1);
        (createFirst ? (Task)block : create).IsCompleted.Should().BeFalse();

        guard.Proceed.SetResult();
        var createResult = await create.WaitAsync(Timeout);
        var blockResult = await block.WaitAsync(Timeout);

        (await AdvisoryCountAsync(ns, key, granted: true)).Should().Be(0);
        await using var verify = NewDb(_tenantA, _companyA1);
        var bpActive = await verify.BusinessPartners.AsNoTracking().SingleAsync(b => b.Id == bp);
        var roleActive = await verify.BusinessPartnerRoles.AsNoTracking().SingleAsync(r => r.Id == roleId);
        var activeFichas = await ScalarAsync<int>($"SELECT count(*)::int AS \"Value\" FROM employees WHERE business_partner_id = {bp} AND is_active");

        if (createFirst)
        {
            createResult.IsSuccess.Should().BeTrue(createResult.Error);
            blockResult.IsSuccess.Should().BeFalse();
            blockResult.Code.Should().Be(ApiResponseCodes.MasterData.BpHasActiveEmployment);
            (bpActive.IsActive, roleActive.IsActive, activeFichas).Should().Be((true, true, 1));
        }
        else
        {
            blockResult.IsSuccess.Should().BeTrue(blockResult.Error);
            createResult.IsSuccess.Should().BeFalse();
            createResult.Code.Should().Be(revoke ? ApiResponseCodes.Employees.BpRoleMissing : ApiResponseCodes.Employees.BpInactive,
                "el alta revalida el estado confirmado DESPUÉS del lock");
            activeFichas.Should().Be(0);
            (revoke ? !roleActive.IsActive : !bpActive.IsActive).Should().BeTrue();
        }
        (await CountEmployeesAsync(bp)).Should().Be(createFirst ? 1 : 0);
    }

    // ── rowVersion (xmin) ────────────────────────────────────────────────────

    [Fact]
    public async Task RowVersion_valida_se_acepta_y_cambia_una_obsoleta_da_CONCURRENCY_CONFLICT_sin_cambios_parciales()
    {
        var job = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var other = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var spec = await NewCatalogAsync(EmployeeCatalogType.Specialty);
        var id = await NewEmployeeAsync();
        var v0 = (await GetAsync(id)).RowVersion;

        var ok = await UpdateAsync(id, v0, OptionalField<Guid?>.Of(job));
        ok.IsSuccess.Should().BeTrue(ok.Error);
        var v1 = ok.Value!.RowVersion;
        v1.Should().NotBe(v0);

        var stalePatch = await UpdateAsync(id, v0, OptionalField<Guid?>.Of(other));
        stalePatch.Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        var staleSpecs = await SetSpecialtiesAsync(id, v0, [spec]);
        staleSpecs.Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);

        var after = await GetAsync(id);
        after.JobPosition!.Id.Should().Be(job);
        after.Specialties.Should().BeEmpty();
        after.RowVersion.Should().Be(v1);
        (await CountLinksAsync(id)).Should().Be(0, "sin persistencia parcial del PUT");

        (await SetSpecialtiesAsync(id, v1, [spec])).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RowVersion_ediciones_concurrentes_con_la_misma_version_solo_aplican_una()
    {
        var specA = await NewCatalogAsync(EmployeeCatalogType.Specialty);
        var jobA = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var jobB = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var profession = await NewCatalogAsync(EmployeeCatalogType.Profession);

        var patchVsPatch = await NewEmployeeAsync();
        var v = (await GetAsync(patchVsPatch)).RowVersion;
        var p = await Task.WhenAll(
            UpdateAsync(patchVsPatch, v, OptionalField<Guid?>.Of(jobA)),
            UpdateAsync(patchVsPatch, v, OptionalField<Guid?>.Of(jobB)));
        p.Count(r => r.IsSuccess).Should().Be(1);
        p.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);

        var patchVsPut = await NewEmployeeAsync();
        v = (await GetAsync(patchVsPut)).RowVersion;
        var q = await Task.WhenAll(
            UpdateAsync(patchVsPut, v, OptionalField<Guid?>.Of(null), OptionalField<Guid?>.Of(profession)),
            SetSpecialtiesAsync(patchVsPut, v, [specA]));
        q.Count(r => r.IsSuccess).Should().Be(1);
        q.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        var final = await GetAsync(patchVsPut);
        (final.Profession is not null).Should().Be(q[0].IsSuccess, "solo se aplicó el PATCH ganador");
        (final.Specialties.Count == 1).Should().Be(q[1].IsSuccess, "solo se aplicó el PUT ganador");
    }

    // ── asignación vs inactivación de catálogo ───────────────────────────────

    private sealed class PausingCatalogs(IEmployeeCatalogRepository inner) : IEmployeeCatalogRepository
    {
        public readonly TaskCompletionSource Reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Proceed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyDictionary<Guid, bool>> ResolveForAssignmentAsync(
            EmployeeCatalogType type, Guid tenantId, Guid companyId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        {
            var r = await inner.ResolveForAssignmentAsync(type, tenantId, companyId, ids, ct);
            Reached.TrySetResult();
            await Proceed.Task.WaitAsync(Timeout, ct);
            return r;
        }

        public Task<EmployeeCatalogEntry?> GetByIdAsync(EmployeeCatalogType type, Guid t, Guid c, Guid id, CancellationToken ct = default) =>
            inner.GetByIdAsync(type, t, c, id, ct);
        public Task<(IReadOnlyList<EmployeeCatalogRow> Items, int Total)> ListAsync(
            EmployeeCatalogType type, Guid t, Guid c, bool? a, string? s, int skip, int take, CancellationToken ct = default) =>
            inner.ListAsync(type, t, c, a, s, skip, take, ct);
        public Task<bool> ExistsByNormalizedNameAsync(EmployeeCatalogType type, Guid t, Guid c, string n, Guid? ex, CancellationToken ct = default) =>
            inner.ExistsByNormalizedNameAsync(type, t, c, n, ex, ct);
        public Task AddAsync(EmployeeCatalogEntry entry, CancellationToken ct = default) => inner.AddAsync(entry, ct);
        public uint GetRowVersion(EmployeeCatalogEntry entry) => inner.GetRowVersion(entry);
        public void SetExpectedRowVersion(EmployeeCatalogEntry entry, uint rowVersion) => inner.SetExpectedRowVersion(entry, rowVersion);
        public Task<bool> SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Asignacion_en_curso_hace_esperar_a_la_inactivacion_del_catalogo_y_esta_no_invalida_la_asignacion()
    {
        var job = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var id = await NewEmployeeAsync();
        var version = (await GetAsync(id)).RowVersion;

        await using var assignDb = NewDb(_tenantA, _companyA1);
        var pausing = new PausingCatalogs(new EmployeeCatalogRepository(assignDb));
        var assign = UpdateH(assignDb, _tenantA, _companyA1, pausing)
            .Handle(new UpdateEmployeeCommand(id, version, OptionalField<Guid?>.Of(job)), default);
        await pausing.Reached.Task.WaitAsync(Timeout);

        await using var deactivateDb = NewDb(_tenantA, _companyA1);
        var deactivate = new SetEmployeeCatalogEntryActiveHandler(
                new EmployeeCatalogRepository(deactivateDb), Ctx(_tenantA, _companyA1, _user))
            .Handle(new SetEmployeeCatalogEntryActiveCommand(EmployeeCatalogType.JobPosition, job, await CatalogVersionAsync(EmployeeCatalogType.JobPosition, job), false), default);
        await WaitUntilAsync(async () => await BlockedRowLockWaitersAsync() >= 1);
        deactivate.IsCompleted.Should().BeFalse("la fila está bloqueada FOR SHARE por la asignación");

        pausing.Proceed.SetResult();
        (await assign.WaitAsync(Timeout)).IsSuccess.Should().BeTrue();
        (await deactivate.WaitAsync(Timeout)).IsSuccess.Should().BeTrue();

        var after = await GetAsync(id);
        after.JobPosition!.Id.Should().Be(job);
        after.JobPosition.IsActive.Should().BeFalse("la inactivación posterior no altera la ficha (D-2)");
    }

    [Fact]
    public async Task Inactivacion_en_curso_hace_esperar_a_la_asignacion_que_luego_se_rechaza_con_CATALOG_INACTIVE()
    {
        var job = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var id = await NewEmployeeAsync();
        var version = (await GetAsync(id)).RowVersion;

        await using var deactivateDb = NewDb(_tenantA, _companyA1);
        await using var tx = await deactivateDb.Database.BeginTransactionAsync();
        var entry = await deactivateDb.EmployeeJobPositions.SingleAsync(e => e.Id == job);
        entry.Deactivate(_user);
        await deactivateDb.SaveChangesAsync(); // UPDATE sin confirmar: fila bloqueada

        var assign = UpdateAsync(id, version, OptionalField<Guid?>.Of(job));
        await WaitUntilAsync(async () => await BlockedRowLockWaitersAsync() >= 1);
        assign.IsCompleted.Should().BeFalse();

        await tx.CommitAsync();
        var result = await assign.WaitAsync(Timeout);

        result.Code.Should().Be(ApiResponseCodes.Employees.CatalogInactive, "re-evalúa el estado confirmado");
        (await GetAsync(id)).JobPosition.Should().BeNull();
    }

    // ── T-11: auditoría ──────────────────────────────────────────────────────

    [Fact]
    public async Task T11_cada_operacion_deja_su_registro_oficial_de_auditoria()
    {
        var job = await NewCatalogAsync(EmployeeCatalogType.JobPosition);
        var prof = await NewCatalogAsync(EmployeeCatalogType.Profession);
        var spec = await NewCatalogAsync(EmployeeCatalogType.Specialty);
        var (bp, _) = await NewBpAsync();

        var created = await CreateAsync(bp, audit: true);
        var id = created.Value!.Id;
        var u1 = await UpdateAsync(id, created.Value.RowVersion, OptionalField<Guid?>.Of(job), OptionalField<Guid?>.Of(prof), audit: true);
        var u2 = await UpdateAsync(id, u1.Value!.RowVersion, OptionalField<Guid?>.Of(null), audit: true);
        var s1 = await SetSpecialtiesAsync(id, u2.Value!.RowVersion, [spec], audit: true);
        await SetSpecialtiesAsync(id, s1.Value!.RowVersion, [], audit: true);

        await using var db = NewDb(_tenantA, _companyA1);
        var rows = await db.EmployeeAudits.AsNoTracking().Where(a => a.EntityId == id).OrderBy(a => a.OccurredAtUtc).ToListAsync();
        rows.Select(r => (r.Action, r.ChangedField, r.OldValue, r.NewValue)).Should().Equal(
            ("Created", "StartDate", null, "2026-03-01"),
            ("Updated", "JobPosition", null, job.ToString()),
            ("Updated", "Profession", null, prof.ToString()),
            ("Updated", "JobPosition", job.ToString(), null),
            ("Updated", "Specialty", null, spec.ToString()),
            ("Updated", "Specialty", spec.ToString(), null));
        rows.Should().OnlyContain(r => r.BusinessPartnerId == bp && r.CompanyId == _companyA1);

        // Un PATCH sin cambios reales no genera auditoría de campo.
        var before = rows.Count;
        await UpdateAsync(id, (await GetAsync(id)).RowVersion, audit: true);
        (await db.EmployeeAudits.AsNoTracking().CountAsync(a => a.EntityId == id)).Should().Be(before);
    }

    [Fact]
    public async Task T11_catalogos_auditan_creacion_renombre_inactivacion_y_activacion()
    {
        var type = EmployeeCatalogType.Specialty;
        var created = await CatalogCreateAsync(type, $"Audit {Guid.NewGuid():N}"[..14], audit: true);
        var id = created.Value!.Id;

        await using var db = NewDb(_tenantA, _companyA1, audit: true);
        var ctx = Ctx(_tenantA, _companyA1, _user);
        var repo = new EmployeeCatalogRepository(db);
        await new RenameEmployeeCatalogEntryHandler(repo, new PostgresDatabaseExceptionTranslator(), ctx)
            .Handle(new RenameEmployeeCatalogEntryCommand(type, id, await CatalogVersionAsync(type, id), "Renombrada"), default);
        var set = new SetEmployeeCatalogEntryActiveHandler(repo, ctx);
        await set.Handle(new SetEmployeeCatalogEntryActiveCommand(type, id, await CatalogVersionAsync(type, id), false), default);
        await set.Handle(new SetEmployeeCatalogEntryActiveCommand(type, id, await CatalogVersionAsync(type, id), true), default);

        var actions = await db.EmployeeCatalogAudits.AsNoTracking()
            .Where(a => a.EntityId == id).OrderBy(a => a.OccurredAtUtc).Select(a => a.Action).ToListAsync();
        actions.Should().Equal("Created", "Renamed", "Deactivated", "Activated");
    }

    // ── rowVersion de catálogos (xmin) ───────────────────────────────────────

    private static string CatalogTable(EmployeeCatalogType type) =>
        type switch
        {
            EmployeeCatalogType.JobPosition => "employee_job_positions",
            EmployeeCatalogType.Profession => "employee_professions",
            _ => "employee_specialties",
        };

    private async Task<uint> CatalogVersionAsync(EmployeeCatalogType type, Guid id)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        var v = await db.Database
            .SqlQueryRaw<long>($"SELECT xmin::text::bigint AS \"Value\" FROM {CatalogTable(type)} WHERE id = '{id}'")
            .SingleAsync();
        return (uint)v;
    }

    private async Task<(string Name, bool Active)> CatalogStateAsync(EmployeeCatalogType type, Guid id)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        var name = await db.Database.SqlQueryRaw<string>($"SELECT name AS \"Value\" FROM {CatalogTable(type)} WHERE id = '{id}'").SingleAsync();
        var active = await db.Database.SqlQueryRaw<bool>($"SELECT is_active AS \"Value\" FROM {CatalogTable(type)} WHERE id = '{id}'").SingleAsync();
        return (name, active);
    }

    private async Task<int> CatalogAuditCountAsync(Guid id)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        return await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM employee_catalog_audit WHERE entity_id = {id}").SingleAsync();
    }

    private async Task<Result<EmployeeCatalogEntryDto>> RenameAsync(
        EmployeeCatalogType type, Guid id, uint version, string name,
        Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await new RenameEmployeeCatalogEntryHandler(new EmployeeCatalogRepository(db),
            new PostgresDatabaseExceptionTranslator(), Ctx(t, c, _user))
            .Handle(new RenameEmployeeCatalogEntryCommand(type, id, version, name), default);
    }

    private async Task<Result<EmployeeCatalogEntryDto>> SetActiveAsync(
        EmployeeCatalogType type, Guid id, uint version, bool active,
        Guid? tenant = null, Guid? company = null, bool audit = false)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c, audit);
        return await new SetEmployeeCatalogEntryActiveHandler(new EmployeeCatalogRepository(db), Ctx(t, c, _user))
            .Handle(new SetEmployeeCatalogEntryActiveCommand(type, id, version, active), default);
    }

    [Theory]
    [InlineData(EmployeeCatalogType.JobPosition)]
    [InlineData(EmployeeCatalogType.Profession)]
    [InlineData(EmployeeCatalogType.Specialty)]
    public async Task Catalogo_las_consultas_devuelven_rowVersion(EmployeeCatalogType type)
    {
        var company = (await NewCompanyAsync()).Id;
        var created = (await CatalogCreateAsync(type, "Con version", company: company)).Value!;
        created.RowVersion.Should().BeGreaterThan(0u).And.Be(await CatalogVersionAsync(type, created.Id));

        await using var db = NewDb(_tenantA, company);
        var ctx = Ctx(_tenantA, company, _user);
        var repo = new EmployeeCatalogRepository(db);
        (await new GetEmployeeCatalogEntryHandler(repo, ctx).Handle(new GetEmployeeCatalogEntryQuery(type, created.Id), default))
            .Value!.RowVersion.Should().Be(created.RowVersion);
        (await new ListEmployeeCatalogHandler(repo, ctx).Handle(new ListEmployeeCatalogQuery(type), default))
            .Value!.Items.Should().ContainSingle(i => i.Id == created.Id && i.RowVersion == created.RowVersion);
    }

    [Theory]
    [InlineData(EmployeeCatalogType.JobPosition)]
    [InlineData(EmployeeCatalogType.Profession)]
    [InlineData(EmployeeCatalogType.Specialty)]
    public async Task Catalogo_renombrar_con_version_vigente_cambia_el_token_y_con_obsoleta_da_CONCURRENCY_CONFLICT(
        EmployeeCatalogType type)
    {
        var id = await NewCatalogAsync(type, name: $"Base {Guid.NewGuid():N}"[..14]);
        var v0 = await CatalogVersionAsync(type, id);

        var ok = await RenameAsync(type, id, v0, $"Nuevo {Guid.NewGuid():N}"[..14], audit: true);
        ok.IsSuccess.Should().BeTrue(ok.Error);
        ok.Value!.RowVersion.Should().NotBe(v0).And.Be(await CatalogVersionAsync(type, id));
        var auditAfterOk = await CatalogAuditCountAsync(id);
        var stateAfterOk = await CatalogStateAsync(type, id);

        var stale = await RenameAsync(type, id, v0, "Obsoleto", audit: true);
        stale.IsSuccess.Should().BeFalse();
        stale.Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await CatalogStateAsync(type, id)).Should().Be(stateAfterOk, "sin cambios parciales");
        (await CatalogAuditCountAsync(id)).Should().Be(auditAfterOk, "sin auditoría de éxito ante conflicto");
        (await CatalogVersionAsync(type, id)).Should().Be(ok.Value.RowVersion);

        // Mismo nombre con versión obsoleta: también conflicto; con vigente: éxito que renueva el token.
        (await RenameAsync(type, id, v0, ok.Value.Name)).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        var same = await RenameAsync(type, id, ok.Value.RowVersion, ok.Value.Name);
        same.IsSuccess.Should().BeTrue(same.Error);
        same.Value!.RowVersion.Should().NotBe(ok.Value.RowVersion);
    }

    [Theory]
    [InlineData(EmployeeCatalogType.JobPosition)]
    [InlineData(EmployeeCatalogType.Profession)]
    [InlineData(EmployeeCatalogType.Specialty)]
    public async Task Catalogo_activar_e_inactivar_exigen_la_version_vigente(EmployeeCatalogType type)
    {
        var id = await NewCatalogAsync(type);
        var v0 = await CatalogVersionAsync(type, id);

        var off = await SetActiveAsync(type, id, v0, false, audit: true);
        off.IsSuccess.Should().BeTrue(off.Error);
        off.Value!.IsActive.Should().BeFalse();
        off.Value.RowVersion.Should().NotBe(v0);
        var audits = await CatalogAuditCountAsync(id);

        // Obsoleta: conflicto (no 422 de estado), sin cambios ni auditoría.
        (await SetActiveAsync(type, id, v0, true, audit: true)).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await SetActiveAsync(type, id, v0, false, audit: true)).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await CatalogStateAsync(type, id)).Active.Should().BeFalse();
        (await CatalogAuditCountAsync(id)).Should().Be(audits);

        var on = await SetActiveAsync(type, id, off.Value.RowVersion, true, audit: true);
        on.IsSuccess.Should().BeTrue(on.Error);
        on.Value!.IsActive.Should().BeTrue();
        (await CatalogAuditCountAsync(id)).Should().Be(audits + 1);
        // Con versión vigente, repetir el estado sigue siendo violación de regla de dominio.
        await FluentActions.Awaiting(() => SetActiveAsync(type, id, on.Value.RowVersion, true))
            .Should().ThrowAsync<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData(EmployeeCatalogType.JobPosition)]
    [InlineData(EmployeeCatalogType.Profession)]
    [InlineData(EmployeeCatalogType.Specialty)]
    public async Task Catalogo_dos_modificaciones_concurrentes_con_la_misma_version_solo_aplican_una(
        EmployeeCatalogType type)
    {
        var a = await NewCatalogAsync(type);
        var v = await CatalogVersionAsync(type, a);
        var renames = await Task.WhenAll(
            RenameAsync(type, a, v, $"Uno {Guid.NewGuid():N}"[..12], audit: true),
            RenameAsync(type, a, v, $"Dos {Guid.NewGuid():N}"[..12], audit: true));
        renames.Count(r => r.IsSuccess).Should().Be(1);
        renames.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        (await CatalogAuditCountAsync(a)).Should().Be(1, "solo la modificación ganadora audita (el alta no se audita en esta ruta)");

        var b = await NewCatalogAsync(type);
        var vb = await CatalogVersionAsync(type, b);
        var mixed = await Task.WhenAll(
            RenameAsync(type, b, vb, $"Tres {Guid.NewGuid():N}"[..12]),
            SetActiveAsync(type, b, vb, false));
        mixed.Count(r => r.IsSuccess).Should().Be(1);
        mixed.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.ConcurrencyConflict);
        var state = await CatalogStateAsync(type, b);
        (state.Active == false).Should().Be(mixed[1].IsSuccess, "solo se aplicó la operación ganadora");
    }

    [Theory]
    [InlineData(EmployeeCatalogType.JobPosition)]
    [InlineData(EmployeeCatalogType.Profession)]
    [InlineData(EmployeeCatalogType.Specialty)]
    public async Task Catalogo_con_version_correcta_pero_de_otra_empresa_o_tenant_es_NOT_FOUND_y_no_cambia(
        EmployeeCatalogType type)
    {
        var id = await NewCatalogAsync(type);
        var v = await CatalogVersionAsync(type, id);
        var before = await CatalogStateAsync(type, id);

        foreach (var (t, c) in new[] { (_tenantA, _companyA2), (_tenantB, _companyB1) })
        {
            (await RenameAsync(type, id, v, "Intruso", t, c)).Code.Should().Be(ApiResponseCodes.Common.NotFound);
            (await SetActiveAsync(type, id, v, false, t, c)).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        }
        (await CatalogStateAsync(type, id)).Should().Be(before);
        (await CatalogVersionAsync(type, id)).Should().Be(v);
    }

    [Theory]
    [InlineData(EmployeeCatalogType.JobPosition)]
    [InlineData(EmployeeCatalogType.Profession)]
    [InlineData(EmployeeCatalogType.Specialty)]
    public async Task Catalogo_con_version_vigente_conserva_normalizacion_y_unicidad(EmployeeCatalogType type)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var a = await NewCatalogAsync(type, name: $"Alfa {suffix}");
        await NewCatalogAsync(type, name: $"Beta {suffix}");

        var dup = await RenameAsync(type, a, await CatalogVersionAsync(type, a), $"  BETA   {suffix} ");
        dup.Code.Should().Be(ApiResponseCodes.Employees.CatalogDuplicate);

        var ok = await RenameAsync(type, a, await CatalogVersionAsync(type, a), $"  Álfa   Prima {suffix} ");
        ok.Value!.Name.Should().Be($"Álfa Prima {suffix}");
    }
}
