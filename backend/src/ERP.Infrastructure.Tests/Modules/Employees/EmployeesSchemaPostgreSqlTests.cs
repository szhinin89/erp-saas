using ERP.Application;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Employees;

/// <summary>
/// EMP-E2 — restricciones reales de PostgreSQL 16 (Testcontainers; BD desechable) del modelo de
/// Empleados: unicidades, exclusiones temporales con fechas inclusivas, CHECK, catálogos,
/// aislamiento tenant/empresa, concurrencia <c>xmin</c>, coherencia del snapshot y auditoría oficial.
/// Las filas históricas (períodos cerrados) se insertan con SQL porque la baja pertenece a EMP-E5.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class EmployeesSchemaPostgreSqlTests : IAsyncLifetime
{
    private const string UniqueViolation = "23505";
    private const string ExclusionViolation = "23P01";
    private const string CheckViolation = "23514";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_employees_schema_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();
    private ServiceProvider _sp = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _otherCompanyId;
    private Guid _branchId;
    private Guid _branch2Id;
    private Guid _identityUserId;
    private Guid _identityUser2Id;
    private readonly List<Guid> _partners = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped(typeof(IAuditReader<>), typeof(EfAuditReader<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => _tenantId,
            () => _companyId,
            _userId
        ));
        services.AddDbContext<ErpDbContext>(
            (sp, options) => options.UseNpgsql(_postgres.GetConnectionString())
        );
        services.AddScoped<ICurrentTenant>(_ => new FixedCurrentTenant(() => _tenantId));
        services.AddScoped<ICurrentCompany>(_ => new FixedCurrentCompany(() => _companyId));
        _sp = services.BuildServiceProvider();

        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
        var other = Company.CreateManaged(tenant.Id, "1790016919001", "Otra S.A.", createdBy: _userId);
        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, other);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;
        _otherCompanyId = other.Id;

        var b1 = NewBranch("001");
        var b2 = NewBranch("002");
        db.Branches.AddRange(b1, b2);
        _branchId = b1.Id;
        _branch2Id = b2.Id;

        for (var i = 1; i <= 60; i++)
        {
            var number = $"1791352688{i:000}";
            var bp = BusinessPartner.Create(_tenantId, "04", number, null, $"BP {number}", _userId);
            db.BusinessPartners.Add(bp);
            _partners.Add(bp.Id);
        }

        var u1 = IdentityUser.Create("emp.user1", "Uno", "Prueba", null, "hash", _userId);
        var u2 = IdentityUser.Create("emp.user2", "Dos", "Prueba", null, "hash", _userId);
        db.IdentityUsers.AddRange(u1, u2);
        _identityUserId = u1.Id;
        _identityUser2Id = u2.Id;
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _sp.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private Branch NewBranch(string code) =>
        Branch.Create(_tenantId, $"Sucursal {code}", "Dirección", code,
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null,
            email: null, website: null, managerName: null, managerPosition: null, managerEmail: null,
            managerPhone: null, countryId: null, provinceId: null, cantonId: null, parishId: null,
            latitude: null, longitude: null, openingDate: null, internalNotes: null,
            isMainBranch: false, createdBy: _userId, companyId: _companyId);

    private static string? Sql(Exception ex) =>
        (ex as DbUpdateException)?.InnerException is PostgresException pg
            ? pg.SqlState
            : (ex as PostgresException)?.SqlState;

    private async Task<Guid> NewEmployeeAsync(Guid? companyId = null, int partner = -1)
    {
        var company = companyId ?? _companyId;
        var bp = partner >= 0 ? _partners[partner] : await NewPartnerAsync();
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var previous = _companyId;
        _companyId = company;
        try
        {
            var e = Employee.Create(_tenantId, company, bp, new DateOnly(2026, 1, 1), _userId);
            db.Employees.Add(e);
            await db.SaveChangesAsync();
            return e.Id;
        }
        finally
        {
            _companyId = previous;
        }
    }

    private int _nextPartner = 10;

    // Los índices 0..9 se reservan para las pruebas que fijan el BP; el resto se reparte sin repetir.
    private Task<Guid> NewPartnerAsync() =>
        Task.FromResult(_partners[Interlocked.Increment(ref _nextPartner)]);

    private async Task InsertPeriodAsync(Guid employeeId, DateOnly start, DateOnly? end)
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO employee_employment_periods
                (id, tenant_id, company_id, employee_id, start_date, end_date, created_at, created_by)
            VALUES ({Guid.NewGuid()}, {_tenantId}, {_companyId}, {employeeId}, {start}, {end}, now(), {_userId})
            """);
    }

    private async Task InsertAssignmentAsync(Guid employeeId, Guid branchId, DateOnly start, DateOnly? end)
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO employee_branch_assignments
                (id, tenant_id, company_id, employee_id, branch_id, start_date, end_date, created_at, created_by)
            VALUES ({Guid.NewGuid()}, {_tenantId}, {_companyId}, {employeeId}, {branchId}, {start}, {end}, now(), {_userId})
            """);
    }

    private async Task<string?> SqlStateOf(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex) when (Sql(ex) is not null || ex is PostgresException)
        {
            return Sql(ex);
        }
    }

    [Fact]
    public async Task Modelo_EF_y_snapshot_de_la_baseline_son_coherentes()
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        db.Database.HasPendingModelChanges()
            .Should()
            .BeFalse("la baseline, su Designer y el snapshot deben reflejar el modelo de Employees");
    }

    [Fact]
    public async Task Ficha_nueva_abre_su_primer_periodo_y_queda_activa()
    {
        var id = await NewEmployeeAsync();

        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var e = await db.Employees.Include(x => x.Periods).SingleAsync(x => x.Id == id);

        e.IsActive.Should().BeTrue();
        e.JobPositionId.Should().BeNull("cargo y profesión son opcionales (P-01)");
        e.Periods.Should().ContainSingle().Which.EndDate.Should().BeNull();
    }

    [Fact]
    public async Task T01_segunda_ficha_del_mismo_BP_en_la_empresa_falla_pero_en_otra_empresa_se_permite()
    {
        await NewEmployeeAsync(partner: 0);

        var state = await SqlStateOf(() => NewEmployeeAsync(partner: 0));
        state.Should().Be(UniqueViolation);

        var act = async () => await NewEmployeeAsync(_otherCompanyId, partner: 0);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task T01_un_usuario_solo_se_vincula_a_una_ficha_por_empresa()
    {
        var a = await NewEmployeeAsync(partner: 1);
        var b = await NewEmployeeAsync(partner: 2);
        var c = await NewEmployeeAsync(_otherCompanyId, partner: 1);

        await SetUserAsync(a, _identityUserId);
        (await SqlStateOf(() => SetUserAsync(b, _identityUserId))).Should().Be(UniqueViolation);
        await SetUserAsync(b, _identityUser2Id);
        await SetUserAsync(c, _identityUserId, _otherCompanyId);

        // Varias fichas sin usuario no colisionan (índice parcial).
        var act = async () => await NewEmployeeAsync(partner: 3);
        await act.Should().NotThrowAsync();
    }

    private async Task SetUserAsync(Guid employeeId, Guid userId, Guid? company = null)
    {
        var previous = _companyId;
        _companyId = company ?? _companyId;
        try
        {
            await using var scope = _sp.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var e = await db.Employees.SingleAsync(x => x.Id == employeeId);
            db.Entry(e).Property(x => x.IdentityUserId).CurrentValue = userId;
            await db.SaveChangesAsync();
        }
        finally
        {
            _companyId = previous;
        }
    }

    [Fact]
    public async Task T02_segundo_periodo_abierto_falla()
    {
        var id = await NewEmployeeAsync();
        var state = await SqlStateOf(() => InsertPeriodAsync(id, new DateOnly(2027, 1, 1), null));
        state.Should().BeOneOf(UniqueViolation, ExclusionViolation);
    }

    [Fact]
    public async Task T02_bordes_de_periodos_con_fechas_inclusivas()
    {
        var id = await NewEmployeeAsync();
        // Ficha abierta desde 2026-01-01: se cierra con SQL (la baja es de EMP-E5) para poder probar el historial.
        await using (var scope = _sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE employee_employment_periods SET end_date = {new DateOnly(2026, 6, 30)} WHERE employee_id = {id}");
        }

        // Reingreso el mismo día de la baja (D): solapa → rechazado.
        (await SqlStateOf(() => InsertPeriodAsync(id, new DateOnly(2026, 6, 30), null)))
            .Should().Be(ExclusionViolation);
        // Dentro de un período cerrado → rechazado.
        (await SqlStateOf(() => InsertPeriodAsync(id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5))))
            .Should().Be(ExclusionViolation);
        // end_date < start_date → CHECK.
        (await SqlStateOf(() => InsertPeriodAsync(id, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 9))))
            .Should().Be(CheckViolation);
        // D+1 → aceptado.
        await InsertPeriodAsync(id, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        // Período de un solo día (start = end) → válido.
        await InsertPeriodAsync(id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));
        // Abierto posterior sin solapar → aceptado (el único abierto).
        await InsertPeriodAsync(id, new DateOnly(2026, 10, 1), null);
        // Las exclusiones son por empleado: otro empleado con un período sobre las mismas fechas no interfiere
        // (su ficha ya trae un período abierto desde 2026-01-01, que se cierra para repetir las fechas).
        var other = await NewEmployeeAsync();
        await using (var scope = _sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE employee_employment_periods SET end_date = {new DateOnly(2026, 6, 30)} WHERE employee_id = {other}");
        }
        await InsertPeriodAsync(other, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
    }

    [Fact]
    public async Task T03_asignaciones_solapadas_de_la_misma_sucursal_fallan_y_las_contiguas_o_de_otra_sucursal_se_permiten()
    {
        var id = await NewEmployeeAsync();
        await InsertAssignmentAsync(id, _branchId, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        (await SqlStateOf(() => InsertAssignmentAsync(id, _branchId, new DateOnly(2026, 3, 31), null)))
            .Should().Be(ExclusionViolation);
        (await SqlStateOf(() => InsertAssignmentAsync(id, _branchId, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 2))))
            .Should().Be(ExclusionViolation);
        (await SqlStateOf(() => InsertAssignmentAsync(id, _branchId, new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 1))))
            .Should().Be(CheckViolation);

        // Varias sucursales simultáneas (P-04) y día siguiente en la misma sucursal.
        await InsertAssignmentAsync(id, _branch2Id, new DateOnly(2026, 1, 1), null);
        await InsertAssignmentAsync(id, _branchId, new DateOnly(2026, 4, 1), null);
    }

    [Fact]
    public async Task UXCAT01_nombre_normalizado_unico_por_empresa_en_cada_catalogo_e_independiente_entre_catalogos()
    {
        await AddCatalogAsync(JobPosition.Create(_tenantId, _companyId, "Médico General", _userId));
        await AddCatalogAsync(Profession.Create(_tenantId, _companyId, "Médico General", _userId));
        await AddCatalogAsync(Specialty.Create(_tenantId, _companyId, "Médico General", _userId));

        (await SqlStateOf(() => AddCatalogAsync(JobPosition.Create(_tenantId, _companyId, "  medico   general ", _userId))))
            .Should().Be(UniqueViolation);
        (await SqlStateOf(() => AddCatalogAsync(Profession.Create(_tenantId, _companyId, "MÉDICO GENERAL", _userId))))
            .Should().Be(UniqueViolation);
        (await SqlStateOf(() => AddCatalogAsync(Specialty.Create(_tenantId, _companyId, "Medico General", _userId))))
            .Should().Be(UniqueViolation);

        var previous = _companyId;
        _companyId = _otherCompanyId;
        try
        {
            var act = async () => await AddCatalogAsync(JobPosition.Create(_tenantId, _otherCompanyId, "Médico General", _userId));
            await act.Should().NotThrowAsync();
        }
        finally
        {
            _companyId = previous;
        }
    }

    private async Task AddCatalogAsync(EmployeeCatalogEntry entry)
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Add(entry);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UXSPL01_una_especialidad_por_empleado_una_sola_fila()
    {
        var employee = await NewEmployeeAsync();
        var specialty = Specialty.Create(_tenantId, _companyId, "Cardiología", _userId);
        await AddCatalogAsync(specialty);

        await AddLinkAsync(employee, specialty.Id);
        (await SqlStateOf(() => AddLinkAsync(employee, specialty.Id))).Should().Be(UniqueViolation);
    }

    private async Task AddLinkAsync(Guid employeeId, Guid specialtyId)
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.EmployeeSpecialtyLinks.Add(
            EmployeeSpecialty.Create(_tenantId, _companyId, employeeId, specialtyId, _userId));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Catalogos_y_ficha_usan_claves_foraneas_con_restriccion()
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var bad = Employee.Create(_tenantId, _companyId, Guid.NewGuid(), new DateOnly(2026, 1, 1), _userId);
        db.Employees.Add(bad);
        var act = async () => await db.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        Sql(ex.Which).Should().Be("23503", "business_partner_id referencia master_business_partners");
    }

    [Fact]
    public async Task Aislamiento_una_ficha_no_es_visible_desde_otra_empresa()
    {
        var id = await NewEmployeeAsync(partner: 3);

        var previous = _companyId;
        _companyId = _otherCompanyId;
        try
        {
            await using var scope = _sp.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            (await db.Employees.AnyAsync(x => x.Id == id)).Should().BeFalse();
            (await db.EmployeeEmploymentPeriods.AnyAsync(x => x.EmployeeId == id)).Should().BeFalse();
        }
        finally
        {
            _companyId = previous;
        }

        await using var own = _sp.CreateAsyncScope();
        (await own.ServiceProvider.GetRequiredService<ErpDbContext>().Employees.AnyAsync(x => x.Id == id))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Xmin_rechaza_una_version_obsoleta()
    {
        var id = await NewEmployeeAsync();
        await using var s1 = _sp.CreateAsyncScope();
        await using var s2 = _sp.CreateAsyncScope();
        var db1 = s1.ServiceProvider.GetRequiredService<ErpDbContext>();
        var db2 = s2.ServiceProvider.GetRequiredService<ErpDbContext>();
        var e1 = await db1.Employees.SingleAsync(x => x.Id == id);
        var e2 = await db2.Employees.SingleAsync(x => x.Id == id);

        var job1 = JobPosition.Create(_tenantId, _companyId, $"Cargo {Guid.NewGuid():N}", _userId);
        var job2 = JobPosition.Create(_tenantId, _companyId, $"Cargo {Guid.NewGuid():N}", _userId);
        db1.Add(job1);
        db2.Add(job2);
        db1.Entry(e1).Property(x => x.JobPositionId).CurrentValue = job1.Id;
        db2.Entry(e2).Property(x => x.JobPositionId).CurrentValue = job2.Id;
        await db1.SaveChangesAsync();

        var act = async () => await db2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task T11_alta_y_catalogos_dejan_su_registro_de_auditoria_oficial()
    {
        var id = await NewEmployeeAsync();
        var catalog = JobPosition.Create(_tenantId, _companyId, "Recepcionista", _userId);
        await AddCatalogAsync(catalog);

        await using (var scope = _sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var c = await db.EmployeeJobPositions.SingleAsync(x => x.Id == catalog.Id);
            c.Rename("Recepcionista Senior", _userId);
            await db.SaveChangesAsync();
            var again = await db.EmployeeJobPositions.SingleAsync(x => x.Id == catalog.Id);
            again.Deactivate(_userId);
            await db.SaveChangesAsync();
            again.Activate(_userId);
            await db.SaveChangesAsync();
        }

        await using var verify = _sp.CreateAsyncScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();

        var audit = await vdb.EmployeeAudits.SingleAsync(a => a.EntityId == id);
        audit.Action.Should().Be("Created");
        audit.CompanyId.Should().Be(_companyId);
        audit.NewValue.Should().Be("2026-01-01");
        audit.UserId.Should().Be(_userId);

        var rows = await vdb.EmployeeCatalogAudits
            .Where(a => a.EntityId == catalog.Id)
            .OrderBy(a => a.OccurredAtUtc)
            .ToListAsync();
        rows.Select(r => r.Action).Should().Equal("Created", "Renamed", "Deactivated", "Activated");
        rows.Should().OnlyContain(r => r.CatalogType == EmployeeCatalogType.JobPosition);
        rows[1].OldName.Should().Be("Recepcionista");
        rows[1].NewName.Should().Be("Recepcionista Senior");
        rows[2].IsActive.Should().BeFalse();
    }
}
