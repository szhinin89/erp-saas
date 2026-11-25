using System.Data.Common;
using ERP.Application;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.Interfaces;
using ERP.Application.Modules.Employees.UseCases;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Security;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Employees;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Employees;

/// <summary>
/// EMP-E6b — <c>IEmployeeDirectory</c> sobre PostgreSQL 16 real (Testcontainers; base desechable): T-15,
/// integridad temporal (T-09), aislamiento tenant/empresa (T-06), datos mínimos (T-12), protección IAM-V1-F1,
/// número fijo de consultas y ausencia de escrituras. Los datos se crean con los handlers reales. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class EmployeeDirectoryPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_employee_directory_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenantA, _companyA1, _companyA2, _tenantB, _companyB1;
    private int _seq;

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

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

    private sealed class CommandLog : DbCommandInterceptor
    {
        public readonly List<string> Commands = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, ct);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, ct);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken ct = default)
        {
            Commands.Add(command.CommandText);
            return base.ScalarExecutingAsync(command, eventData, result, ct);
        }
    }

    private sealed class NoPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private ErpDbContext NewDb(Guid tenant, Guid company, CommandLog? log = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString());
        if (log is not null)
            options.AddInterceptors(log);
        return new ErpDbContext(
            options.Options,
            new FixedCurrentTenant(() => tenant),
            new NoPublisher(),
            new FixedCurrentCompany(() => company)
        );
    }

    private static IOperationalContext Ctx(Guid tenant, Guid company, Guid user)
    {
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(company);
        ctx.SetupGet(x => x.UserId).Returns(user);
        return ctx.Object;
    }

    private EmployeeDirectory Directory(ErpDbContext db, Guid? tenant = null, Guid? company = null) =>
        new(db, Ctx(tenant ?? _tenantA, company ?? _companyA1, _user), new EmployeeIdentityProjector(new AccessRepository(db)));

    private async Task<T> AskAsync<T>(Func<EmployeeDirectory, Task<T>> ask, Guid? tenant = null, Guid? company = null)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        await using var db = NewDb(t, c);
        return await ask(Directory(db, t, c));
    }

    private static EmployeeQueries Queries(ErpDbContext db) =>
        new(db, new EmployeeIdentityProjector(new AccessRepository(db)));

    private static string ValidCedula(int seq)
    {
        var digits = $"175{seq % 1_000_000:000000}".Select(ch => ch - '0').ToArray();
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var p = digits[i] * (i % 2 == 0 ? 2 : 1);
            sum += p > 9 ? p - 9 : p;
        }
        return string.Concat(digits) + ((10 - sum % 10) % 10);
    }

    private async Task<(Guid Employee, Guid Bp)> NewEmployeeAsync(
        DateOnly? start = null, Guid? tenant = null, Guid? company = null, Guid[]? specialties = null)
    {
        var (t, c) = (tenant ?? _tenantA, company ?? _companyA1);
        Guid bpId;
        await using (var db = NewDb(t, Guid.Empty))
        {
            var n = Interlocked.Increment(ref _seq);
            var bp = BusinessPartner.Create(t, "05", ValidCedula(n), null, $"Persona {n:000}", _user);
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
            .Handle(new CreateEmployeeCommand(bpId, start ?? D(1, 10), null, null, specialties), default);
        r.IsSuccess.Should().BeTrue(r.Error);
        return (r.Value!.Id, bpId);
    }

    private async Task<uint> VersionAsync(Guid employee, Guid? company = null)
    {
        var c = company ?? _companyA1;
        await using var db = NewDb(_tenantA, c);
        return (await Queries(db).GetByIdAsync(_tenantA, c, employee))!.RowVersion;
    }

    private async Task TerminateAsync(Guid employee, DateOnly end)
    {
        await using var db = NewDb(_tenantA, _companyA1);
        var r = await new TerminateEmployeeHandler(new EmployeeRepository(db), Queries(db), Ctx(_tenantA, _companyA1, _user), new UnitOfWork(db))
            .Handle(new TerminateEmployeeCommand(employee, await VersionAsync(employee), end, "motivo interno"), default);
        r.IsSuccess.Should().BeTrue(r.Error);
    }

    private async Task RehireAsync(Guid employee, DateOnly start)
    {
        await using var db = NewDb(_tenantA, _companyA1);
        var r = await new RehireEmployeeHandler(
                new EmployeeRepository(db), new EmployeeBusinessPartnerReader(db), new BusinessPartnerEmploymentGuard(db),
                Queries(db), Ctx(_tenantA, _companyA1, _user), new UnitOfWork(db))
            .Handle(new RehireEmployeeCommand(employee, await VersionAsync(employee), start), default);
        r.IsSuccess.Should().BeTrue(r.Error);
    }

    private async Task<Guid> AssignAsync(Guid employee, Guid branch, DateOnly start, DateOnly? end = null, Guid? company = null)
    {
        var c = company ?? _companyA1;
        await using var db = NewDb(_tenantA, c);
        var r = await new CreateBranchAssignmentHandler(
                new EmployeeRepository(db), new EmployeeBranchReader(db), Queries(db), Ctx(_tenantA, c, _user), new UnitOfWork(db))
            .Handle(new CreateBranchAssignmentCommand(employee, branch, start, end), default);
        r.IsSuccess.Should().BeTrue(r.Error);
        return r.Value!.Id;
    }

    private async Task<Guid> NewBranchAsync(Guid? company = null)
    {
        var c = company ?? _companyA1;
        await using var db = NewDb(_tenantA, c);
        var code = $"D{Interlocked.Increment(ref _seq):0000}";
        var branch = Branch.Create(
            _tenantA, $"Sucursal {code}", "Av. Principal 123", code,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, isMainBranch: false, _user, companyId: c);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.Id;
    }

    private async Task<Guid> NewSpecialtyAsync(Guid? company = null)
    {
        var c = company ?? _companyA1;
        await using var db = NewDb(_tenantA, c);
        var entry = Specialty.Create(_tenantA, c, $"Esp {Guid.NewGuid():N}"[..14], _user);
        db.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private async Task<Guid> NewCompanyAsync()
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        var n = Interlocked.Increment(ref _seq);
        var company = Company.CreateManaged(_tenantA, $"17920{n:00000}001", $"Aislada {n} S.A.", createdBy: _user);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private async Task<Guid> NewLinkedUserAsync(Guid employee, Guid? company = null)
    {
        var c = company ?? _companyA1;
        Guid userId;
        await using (var db = NewDb(Guid.Empty, Guid.Empty))
        {
            var name = $"usuario-{Guid.NewGuid():N}";
            var user = IdentityUser.Create(name, "Nombre", "Apellido", $"{name}@example.com", "hash", _user);
            db.IdentityUsers.Add(user);
            await db.SaveChangesAsync();
            db.CompanyUserMemberships.Add(CompanyUserMembership.Create(c, user.Id, "Compras", null, _user));
            await db.SaveChangesAsync();
            userId = user.Id;
        }
        await using var linkDb = NewDb(_tenantA, c);
        var r = await new LinkEmployeeUserHandler(
                new EmployeeRepository(linkDb), new EmployeeUserReader(linkDb), new AccessRepository(linkDb), Queries(linkDb),
                new PostgresDatabaseExceptionTranslator(), Ctx(_tenantA, c, _user), new UnitOfWork(linkDb))
            .Handle(new LinkEmployeeUserCommand(employee, userId, await VersionAsync(employee, c)), default);
        r.IsSuccess.Should().BeTrue(r.Error);
        return userId;
    }

    private async Task<int> ExecuteAsync(FormattableString sql)
    {
        await using var db = NewDb(Guid.Empty, Guid.Empty);
        return await db.Database.ExecuteSqlInterpolatedAsync(sql);
    }

    // ── T-15 / T-12 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task T15_el_resumen_expone_solo_los_datos_del_contrato_con_cargo_y_profesion_nulos()
    {
        var specialty = await NewSpecialtyAsync();
        var (id, bp) = await NewEmployeeAsync(specialties: [specialty]);
        var branch = await NewBranchAsync();
        var assignment = await AssignAsync(id, branch, D(2, 1), D(2, 28));
        var userId = await NewLinkedUserAsync(id);

        var summary = await AskAsync(d => d.GetByIdAsync(id));

        summary.Should().NotBeNull();
        summary!.EmployeeId.Should().Be(id);
        summary.CompanyId.Should().Be(_companyA1);
        summary.BusinessPartnerId.Should().Be(bp);
        summary.DisplayName.Should().StartWith("Persona");
        summary.IdentityUserId.Should().Be(userId);
        summary.IsActive.Should().BeTrue();
        (summary.JobPositionId, summary.ProfessionId).Should().Be((null, null));
        summary.SpecialtyIds.Should().Equal(specialty);
        summary.Periods.Should().Equal(new EmployeePeriodSummary(D(1, 10), null));
        summary.Assignments.Should().Equal(new EmployeeAssignmentSummary(assignment, branch, D(2, 1), D(2, 28)));
        typeof(EmployeeSummary).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "EmployeeId", "CompanyId", "BusinessPartnerId", "DisplayName", "IdentityUserId", "IsActive",
            "JobPositionId", "ProfessionId", "SpecialtyIds", "Periods", "Assignments");
        typeof(EmployeePeriodSummary).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("StartDate", "EndDate");

        (await AskAsync(d => d.GetByIdAsync(Guid.NewGuid()))).Should().BeNull();
    }

    // ── T-15 / T-09: integridad temporal ─────────────────────────────────────

    [Fact]
    public async Task Fechas_inclusivas_historial_huecos_baja_y_reincorporacion_sin_reabrir_asignaciones()
    {
        var (id, _) = await NewEmployeeAsync(D(1, 10));
        var b1 = await NewBranchAsync();
        var b2 = await NewBranchAsync();
        var past = await AssignAsync(id, b1, D(2, 1), D(2, 28));
        var openAtTermination = await AssignAsync(id, b2, D(3, 1));
        await TerminateAsync(id, D(6, 30));

        // Dado de baja: las fechas históricas siguen respondiendo según los registros.
        (await AskAsync(d => d.IsActiveOnAsync(id, D(6, 30)))).Should().BeTrue("último día inclusivo");
        (await AskAsync(d => d.IsActiveOnAsync(id, D(7, 1)))).Should().BeFalse();
        (await AskAsync(d => d.IsEligibleForBranchAsync(id, b2, D(4, 15)))).Should().BeTrue("histórica");
        (await AskAsync(d => d.IsEligibleForBranchAsync(id, b2, D(7, 1)))).Should().BeFalse();
        (await AskAsync(d => d.GetByIdAsync(id)))!.IsActive.Should().BeFalse();

        await RehireAsync(id, D(8, 1));

        var expectedActive = new (DateOnly Date, bool Active)[]
        {
            (D(1, 9), false), (D(1, 10), true), (D(6, 30), true), (D(7, 1), false), (D(7, 31), false),
            (D(8, 1), true), (D(1, 1, 2030), true),
        };
        foreach (var (date, active) in expectedActive)
            (await AskAsync(d => d.IsActiveOnAsync(id, date))).Should().Be(active, $"activo el {date}");

        var eligibility = new (Guid Branch, DateOnly Date, bool Eligible)[]
        {
            (b1, D(1, 31), false), (b1, D(2, 1), true), (b1, D(2, 28), true), (b1, D(3, 1), false),
            (b2, D(2, 28), false), (b2, D(3, 1), true), (b2, D(6, 30), true), (b2, D(7, 1), false),
            (b1, D(8, 15), false), (b2, D(8, 15), false), // reincorporado sin asignación (E-D4)
        };
        foreach (var (branch, date, eligible) in eligibility)
            (await AskAsync(d => d.IsEligibleForBranchAsync(id, branch, date))).Should().Be(eligible, $"elegible el {date}");

        var history = await AskAsync(d => d.GetBranchAssignmentsAsync(id));
        history.Select(a => (a.AssignmentId, a.EndDate)).Should().Equal(
            (past, (DateOnly?)D(2, 28)), (openAtTermination, D(6, 30)));

        // Asignación futura dentro del período nuevo: solo desde su inicio, y abierta hacia adelante.
        var future = await AssignAsync(id, b1, D(10, 1));
        (await AskAsync(d => d.IsActiveOnAsync(id, D(9, 30)))).Should().BeTrue();
        (await AskAsync(d => d.IsEligibleForBranchAsync(id, b1, D(9, 30)))).Should().BeFalse("todavía no iniciada");
        (await AskAsync(d => d.IsEligibleForBranchAsync(id, b1, D(10, 1)))).Should().BeTrue();
        (await AskAsync(d => d.IsEligibleForBranchAsync(id, b1, D(1, 1, 2030)))).Should().BeTrue();

        (await AskAsync(d => d.GetBranchAssignmentsAsync(id, b1))).Select(a => a.AssignmentId).Should().Equal(past, future);
        (await AskAsync(d => d.GetBranchAssignmentsAsync(id, date: D(4, 1)))).Select(a => a.AssignmentId).Should().Equal(openAtTermination);
        (await AskAsync(d => d.GetBranchAssignmentsAsync(id, b1, D(2, 15)))).Select(a => a.AssignmentId).Should().Equal(past);
        (await AskAsync(d => d.GetBranchAssignmentsAsync(id, b1, D(4, 1)))).Should().BeEmpty();
        (await AskAsync(d => d.GetByIdAsync(id)))!.Periods.Should().Equal(
            new EmployeePeriodSummary(D(1, 10), D(6, 30)), new EmployeePeriodSummary(D(8, 1), null));
    }

    [Fact]
    public async Task Sin_asignacion_no_es_elegible_y_con_varias_sucursales_cada_una_se_evalua_por_separado()
    {
        var (none, _) = await NewEmployeeAsync(D(1, 10));
        var (many, _) = await NewEmployeeAsync(D(1, 10));
        var b1 = await NewBranchAsync();
        var b2 = await NewBranchAsync();
        var b3 = await NewBranchAsync();
        await AssignAsync(many, b1, D(2, 1));
        await AssignAsync(many, b2, D(2, 1), D(3, 31));

        (await AskAsync(d => d.IsActiveOnAsync(none, D(3, 1)))).Should().BeTrue();
        (await AskAsync(d => d.IsEligibleForBranchAsync(none, b1, D(3, 1)))).Should().BeFalse();
        (await AskAsync(d => d.GetBranchAssignmentsAsync(none))).Should().BeEmpty();
        (await AskAsync(d => d.IsEligibleForBranchAsync(many, b1, D(3, 1)))).Should().BeTrue();
        (await AskAsync(d => d.IsEligibleForBranchAsync(many, b2, D(3, 1)))).Should().BeTrue();
        (await AskAsync(d => d.IsEligibleForBranchAsync(many, b3, D(3, 1)))).Should().BeFalse();
        (await AskAsync(d => d.IsEligibleForBranchAsync(many, b2, D(4, 1)))).Should().BeFalse("vencida");
    }

    [Fact]
    public async Task Sucursal_desactivada_despues_y_BP_o_usuario_no_alteran_la_elegibilidad_historica_D_E6b_2()
    {
        var (id, bp) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();
        var assignment = await AssignAsync(id, branch, D(2, 1));
        await ExecuteAsync($"UPDATE branches SET is_active = false WHERE id = {branch}");

        (await AskAsync(d => d.IsEligibleForBranchAsync(id, branch, D(3, 1)))).Should().BeTrue("la habilitación de la sucursal es del consumidor");
        (await AskAsync(d => d.GetBranchAssignmentsAsync(id))).Select(a => a.AssignmentId).Should().Equal(assignment);

        // Sin usuario vinculado y con el BP inactivo tras la baja, la referencia histórica sigue válida.
        await TerminateAsync(id, D(6, 30));
        await ExecuteAsync($"UPDATE master_business_partners SET is_active = false WHERE id = {bp}");
        (await AskAsync(d => d.IsEligibleForBranchAsync(id, branch, D(3, 1)))).Should().BeTrue();
        (await AskAsync(d => d.GetByIdAsync(id))).Should().NotBeNull();
    }

    // ── lista ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lista_sin_paginacion_con_includeInactive_y_specialtyId_solo_de_la_empresa_operativa()
    {
        var company = await NewCompanyAsync();
        var specialty = await NewSpecialtyAsync(company);
        var other = await NewSpecialtyAsync(company);
        var (active, _) = await NewEmployeeAsync(D(1, 10), company: company, specialties: [specialty]);
        var (plain, _) = await NewEmployeeAsync(D(1, 10), company: company, specialties: [other]);
        var (retired, _) = await NewEmployeeAsync(D(1, 10), company: company, specialties: [specialty]);
        await NewEmployeeAsync(D(1, 10)); // otra empresa: no debe aparecer
        await using (var db = NewDb(_tenantA, company))
        {
            var r = await new TerminateEmployeeHandler(new EmployeeRepository(db), Queries(db), Ctx(_tenantA, company, _user), new UnitOfWork(db))
                .Handle(new TerminateEmployeeCommand(retired, await VersionAsync(retired, company), D(6, 30), null), default);
            r.IsSuccess.Should().BeTrue(r.Error);
        }

        async Task<IEnumerable<Guid>> Ids(bool includeInactive, EmployeeDirectoryFilter? filter = null) =>
            (await AskAsync(d => d.ListAsync(company, includeInactive, filter), company: company)).Select(e => e.EmployeeId);

        (await Ids(false)).Should().BeEquivalentTo([active, plain]);
        (await Ids(true)).Should().BeEquivalentTo([active, plain, retired]);
        (await Ids(false, new EmployeeDirectoryFilter(specialty))).Should().BeEquivalentTo([active]);
        (await Ids(true, new EmployeeDirectoryFilter(specialty))).Should().BeEquivalentTo([active, retired], "inactivos históricos");
        (await Ids(true, new EmployeeDirectoryFilter(Guid.NewGuid()))).Should().BeEmpty();
        (await Ids(true, new EmployeeDirectoryFilter())).Should().HaveCount(3);

        // La especialidad retirada de la ficha deja de filtrar (vínculo inactivo).
        await ExecuteAsync($"UPDATE employee_specialty_links SET is_active = false WHERE employee_id = {active}");
        (await Ids(true, new EmployeeDirectoryFilter(specialty))).Should().BeEquivalentTo([retired]);

        // companyId distinto del operativo: vacío, aunque exista y sea del mismo tenant.
        (await AskAsync(d => d.ListAsync(company, true))).Should().BeEmpty();
        (await AskAsync(d => d.ListAsync(_companyA1, true), company: company)).Should().BeEmpty();
    }

    // ── T-06: aislamiento y contexto ─────────────────────────────────────────

    [Fact]
    public async Task Ficha_o_sucursal_de_otra_empresa_o_tenant_se_comporta_como_inexistente()
    {
        var (id, _) = await NewEmployeeAsync(D(1, 10));
        var branch = await NewBranchAsync();
        var foreignBranch = await NewBranchAsync(_companyA2);
        await AssignAsync(id, branch, D(2, 1));
        await NewLinkedUserAsync(id);

        foreach (var (t, c) in new[] { (_tenantA, _companyA2), (_tenantB, _companyB1) })
        {
            (await AskAsync(d => d.GetByIdAsync(id), t, c)).Should().BeNull();
            (await AskAsync(d => d.IsActiveOnAsync(id, D(3, 1)), t, c)).Should().BeFalse();
            (await AskAsync(d => d.GetBranchAssignmentsAsync(id), t, c)).Should().BeEmpty();
            (await AskAsync(d => d.IsEligibleForBranchAsync(id, branch, D(3, 1)), t, c)).Should().BeFalse();
            (await AskAsync(d => d.GetLinkedUserAsync(id), t, c)).Should().BeNull();
            (await AskAsync(d => d.ListAsync(c, true), t, c)).Select(e => e.EmployeeId).Should().NotContain(id);
        }
        (await AskAsync(d => d.IsEligibleForBranchAsync(id, foreignBranch, D(3, 1)))).Should().BeFalse("sucursal ajena");
        (await AskAsync(d => d.GetBranchAssignmentsAsync(id, foreignBranch))).Should().BeEmpty();

        // El predicado propio actúa solo: el filtro global del DbContext dejaría pasar la ficha de A1.
        await using var db = NewDb(_tenantA, _companyA1);
        var wrongTenant = Directory(db, _tenantB, _companyA1);
        (await wrongTenant.GetByIdAsync(id)).Should().BeNull();
        (await wrongTenant.IsEligibleForBranchAsync(id, branch, D(3, 1))).Should().BeFalse();
        var wrongCompany = Directory(db, _tenantA, _companyA2);
        (await wrongCompany.IsActiveOnAsync(id, D(3, 1))).Should().BeFalse();
        (await wrongCompany.GetBranchAssignmentsAsync(id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sin_contexto_operacional_los_seis_metodos_fallan_cerrados(bool missingTenant)
    {
        var (id, _) = await NewEmployeeAsync();
        var log = new CommandLog();
        await using var db = NewDb(_tenantA, _companyA1, log);
        var directory = missingTenant ? Directory(db, Guid.Empty, _companyA1) : Directory(db, _tenantA, Guid.Empty);

        Func<Task>[] calls =
        [
            () => directory.GetByIdAsync(id),
            () => directory.ListAsync(_companyA1, true),
            () => directory.ListAsync(Guid.Empty, true),
            () => directory.IsActiveOnAsync(id, D(3, 1)),
            () => directory.GetBranchAssignmentsAsync(id),
            () => directory.IsEligibleForBranchAsync(id, Guid.NewGuid(), D(3, 1)),
            () => directory.GetLinkedUserAsync(id),
        ];
        foreach (var call in calls)
            (await call.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().StartWith("Invariante violada");
        log.Commands.Should().BeEmpty("no se consulta nada sin contexto");
    }

    // ── usuario vinculado (IAM-V1-F1) ────────────────────────────────────────

    [Fact]
    public async Task GetLinkedUser_aplica_los_casos_aprobados_y_el_resumen_la_misma_proteccion()
    {
        async Task<(Guid? Linked, Guid? InList)> ReadAsync(Guid employee)
        {
            var linked = await AskAsync(d => d.GetLinkedUserAsync(employee));
            var one = (await AskAsync(d => d.GetByIdAsync(employee)))!.IdentityUserId;
            var inList = (await AskAsync(d => d.ListAsync(_companyA1, true))).Single(e => e.EmployeeId == employee).IdentityUserId;
            one.Should().Be(inList);
            return (linked, inList);
        }

        var (none, _) = await NewEmployeeAsync();
        (await ReadAsync(none)).Should().Be((null, null));

        var (active, _) = await NewEmployeeAsync();
        var u1 = await NewLinkedUserAsync(active);
        (await ReadAsync(active)).Should().Be((u1, u1));
        // El TenantId obsoleto no es autoridad.
        await ExecuteAsync($"UPDATE identity_users SET tenant_id = {_tenantB} WHERE id = {u1}");
        (await ReadAsync(active)).Should().Be((u1, u1));

        var (inactiveMembership, _) = await NewEmployeeAsync();
        var u2 = await NewLinkedUserAsync(inactiveMembership);
        await ExecuteAsync($"UPDATE company_user_memberships SET is_active = false WHERE identity_user_id = {u2}");
        (await ReadAsync(inactiveMembership)).Should().Be((u2, u2));

        var (inactiveUser, _) = await NewEmployeeAsync();
        var u3 = await NewLinkedUserAsync(inactiveUser);
        await ExecuteAsync($"UPDATE identity_users SET is_active = false WHERE id = {u3}");
        (await ReadAsync(inactiveUser)).Should().Be((u3, u3));

        var (global, _) = await NewEmployeeAsync();
        var u4 = await NewLinkedUserAsync(global);
        await using (var db = NewDb(Guid.Empty, Guid.Empty))
        {
            db.GlobalUserRoles.Add(GlobalUserRole.Create(u4, SecurityRoles.Admin, _user));
            await db.SaveChangesAsync();
        }
        (await ReadAsync(global)).Should().Be((null, null));

        // Sin membresía en la empresa (dato anómalo: las membresías no se borran) pero con otra en A2.
        var (noMembership, _) = await NewEmployeeAsync();
        var u5 = await NewLinkedUserAsync(noMembership);
        await ExecuteAsync($"UPDATE company_user_memberships SET company_id = {_companyA2} WHERE identity_user_id = {u5}");
        (await ReadAsync(noMembership)).Should().Be((null, null));
    }

    // ── rendimiento y solo lectura ───────────────────────────────────────────

    [Fact]
    public async Task El_numero_de_consultas_no_depende_de_la_cantidad_de_fichas()
    {
        async Task<Guid> SeedCompanyAsync(int count)
        {
            var company = await NewCompanyAsync();
            var specialty = await NewSpecialtyAsync(company);
            var branch = await NewBranchAsync(company);
            for (var i = 0; i < count; i++)
            {
                var (id, _) = await NewEmployeeAsync(D(1, 10), company: company, specialties: [specialty]);
                await AssignAsync(id, branch, D(2, 1), company: company);
                await NewLinkedUserAsync(id, company);
            }
            return company;
        }

        async Task<(int Commands, int Items)> ListAsync(Guid company)
        {
            var log = new CommandLog();
            await using var db = NewDb(_tenantA, company, log);
            var list = await Directory(db, company: company).ListAsync(company, true);
            list.Should().OnlyContain(e => e.IdentityUserId != null && e.Assignments.Count == 1 && e.SpecialtyIds.Count == 1);
            return (log.Commands.Count, list.Count);
        }

        var one = await ListAsync(await SeedCompanyAsync(1));
        var seven = await ListAsync(await SeedCompanyAsync(7));

        (one.Items, seven.Items).Should().Be((1, 7));
        seven.Commands.Should().Be(one.Commands).And.Be(6, "cabecera, períodos, especialidades, asignaciones, membresías y globales");
    }

    [Fact]
    public async Task Las_comprobaciones_temporales_son_una_sola_sentencia_EXISTS_filtrada_en_PostgreSQL()
    {
        var (id, _) = await NewEmployeeAsync();
        var branch = await NewBranchAsync();
        await AssignAsync(id, branch, D(2, 1));
        var log = new CommandLog();
        await using var db = NewDb(_tenantA, _companyA1, log);
        var directory = Directory(db);

        await directory.IsActiveOnAsync(id, D(3, 1));
        await directory.IsEligibleForBranchAsync(id, branch, D(3, 1));

        log.Commands.Should().HaveCount(2);
        log.Commands.Should().OnlyContain(sql => sql.Contains("EXISTS") && sql.Contains("tenant_id") && sql.Contains("company_id"));
    }

    [Fact]
    public async Task Ningun_metodo_escribe_ni_modifica_empleados_usuarios_membresias_permisos_ni_sucursales_de_usuario()
    {
        var (id, _) = await NewEmployeeAsync();
        var branch = await NewBranchAsync();
        await AssignAsync(id, branch, D(2, 1));
        await NewLinkedUserAsync(id);

        async Task<string> StateAsync()
        {
            var parts = new List<string>();
            foreach (var table in new[]
            {
                "employees", "employee_employment_periods", "employee_branch_assignments", "employee_specialty_links",
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

        var before = await StateAsync();
        var log = new CommandLog();
        await using var context = NewDb(_tenantA, _companyA1, log);
        var directory = Directory(context);
        await directory.GetByIdAsync(id);
        await directory.ListAsync(_companyA1, true);
        await directory.IsActiveOnAsync(id, D(3, 1));
        await directory.GetBranchAssignmentsAsync(id);
        await directory.IsEligibleForBranchAsync(id, branch, D(3, 1));
        await directory.GetLinkedUserAsync(id);

        log.Commands.Should().OnlyContain(sql => sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));
        context.Database.CurrentTransaction.Should().BeNull("sin transacciones");
        context.ChangeTracker.Entries().Should().OnlyContain(e => e.State == EntityState.Unchanged);
        context.ChangeTracker.Entries<Employee>().Should().BeEmpty("lecturas sin seguimiento");
        (await StateAsync()).Should().Be(before);
    }

    // ── resolución real mediante el registro oficial ─────────────────────────

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    [Fact]
    public async Task El_contenedor_oficial_resuelve_el_directorio_Scoped_con_sus_dependencias_y_consulta_con_contexto_valido()
    {
        var (id, _) = await NewEmployeeAsync(D(1, 10));
        var userId = await NewLinkedUserAsync(id);

        // Mismo registro que el host (ErpHostRegistration): AddInfrastructure + AddApplication.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["InstallData:Enabled"] = "false",
                ["Snowflake:WorkerId"] = "19",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(logging => logging.ClearProviders());
        services.AddDistributedMemoryCache();
        services.AddSingleton<IHostApplicationLifetime, TestLifetime>();
        services.AddInfrastructure(config);
        services.AddApplication();

        var registration = services.Where(d => d.ServiceType == typeof(IEmployeeDirectory)).Should().ContainSingle().Subject;
        registration.Lifetime.Should().Be(ServiceLifetime.Scoped);
        registration.ImplementationType.Should().Be(typeof(EmployeeDirectory));

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        FluentActions.Invoking(() => provider.GetRequiredService<IEmployeeDirectory>())
            .Should().Throw<InvalidOperationException>("un servicio Scoped no se resuelve desde la raíz");

        // Contexto oficial sin HTTP (trabajos en segundo plano): ICurrentTenant / ICurrentCompany lo leen.
        var (previousTenant, previousCompany) = (JobTenantContext.Current, JobCompanyContext.Current);
        try
        {
            JobTenantContext.Current = _tenantA;
            JobCompanyContext.Current = _companyA1;

            await using var scope = provider.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            var directory = sp.GetRequiredService<IEmployeeDirectory>();
            directory.Should().BeOfType<EmployeeDirectory>().And.BeSameAs(sp.GetRequiredService<IEmployeeDirectory>());
            sp.GetRequiredService<IOperationalContext>().Should().BeOfType<OperationalContextService>();
            sp.GetRequiredService<IEmployeeIdentityProjector>().Should().BeOfType<EmployeeIdentityProjector>();
            sp.GetRequiredService<IAccessRepository>().Should().BeOfType<AccessRepository>();
            sp.GetRequiredService<ErpDbContext>().Should().NotBeNull();
            await using (var other = provider.CreateAsyncScope())
                other.ServiceProvider.GetRequiredService<IEmployeeDirectory>().Should().NotBeSameAs(directory);

            var summary = await directory.GetByIdAsync(id);
            summary.Should().NotBeNull();
            summary!.IdentityUserId.Should().Be(userId);
            (await directory.ListAsync(_companyA1, false)).Select(e => e.EmployeeId).Should().Contain(id);
            (await directory.IsActiveOnAsync(id, D(3, 1))).Should().BeTrue();
            (await directory.GetLinkedUserAsync(id)).Should().Be(userId);

            JobCompanyContext.Current = Guid.Empty;
            await FluentActions.Awaiting(() => directory.GetByIdAsync(id)).Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            JobTenantContext.Current = previousTenant;
            JobCompanyContext.Current = previousCompany;
        }
    }
}
