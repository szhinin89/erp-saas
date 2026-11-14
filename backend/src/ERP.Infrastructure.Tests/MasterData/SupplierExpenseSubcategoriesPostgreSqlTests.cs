using ERP.Application;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.Services;
using ERP.Application.MasterData.UseCases.GetSupplierExpenseSubcategories;
using ERP.Application.MasterData.UseCases.SetSupplierExpenseSubcategories;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.MasterData;

/// <summary>
/// EXPENSE-SUPPLIER-SUBCATEGORIES-01 — PostgreSQL real (Testcontainers, baseline completa): reemplazo
/// atómico, baja lógica y reactivación sin duplicados, los tres modos, aislamiento tenant/empresa,
/// concurrencia y auditoría ADR-022. Handlers y repositorios reales; solo el contexto operativo es fake.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SupplierExpenseSubcategoriesPostgreSqlTests : IAsyncLifetime
{
    private static long _tax = 1790077000;
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_bpes_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _user = Guid.NewGuid();
    private ServiceProvider _sp = null!;
    private Guid _tenant;
    private Guid _companyA;
    private Guid _companyB;
    private Guid _activeCompany;
    private Guid _supplier;
    private Guid _sub1;
    private Guid _sub2;
    private Guid _sub3;
    private Guid _subOfB;
    private Guid _categoryA;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped(typeof(IAuditReader<>), typeof(EfAuditReader<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => _tenant,
            () => _activeCompany,
            _user
        ));
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<ICurrentTenant>(_ => new FixedCurrentTenant(() => _tenant));
        services.AddScoped<ICurrentCompany>(_ => new FixedCurrentCompany(() => _activeCompany));
        _sp = services.BuildServiceProvider();

        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("BPES", "bpes-" + Guid.NewGuid().ToString("N")[..8], _user);
        var a = Company.CreateManaged(tenant.Id, Interlocked.Increment(ref _tax) + "001", "A S.A.", createdBy: _user);
        var b = Company.CreateManaged(tenant.Id, Interlocked.Increment(ref _tax) + "001", "B S.A.", createdBy: _user);
        db.Tenants.Add(tenant);
        db.Companies.AddRange(a, b);
        await db.SaveChangesAsync();
        _tenant = tenant.Id;
        _companyA = a.Id;
        _companyB = b.Id;
        _activeCompany = _companyA;

        var bp = BusinessPartner.Create(_tenant, "04", "1790016919001", null, "Proveedor BPES S.A.", _user);
        db.BusinessPartners.Add(bp);
        await db.SaveChangesAsync();
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, bp.Id, RoleType.Supplier, _user));
        _supplier = bp.Id;

        (_categoryA, var subsA) = SeedTree(db, _companyA, "A", 3);
        _sub1 = subsA[0];
        _sub2 = subsA[1];
        _sub3 = subsA[2];
        (_, var subsB) = SeedTree(db, _companyB, "B", 1);
        _subOfB = subsB[0];
        await db.SaveChangesAsync();
    }

    private (Guid CategoryId, List<Guid> Subs) SeedTree(ErpDbContext db, Guid company, string p, int count)
    {
        var account = Account.Create(_tenant, company, AccountCode.Create("6.1.01"), "Gasto " + p, null,
            AccountType.Expense, AccountNature.Debit, true, _user);
        db.Accounts.Add(account);
        var type = ExpenseCategoryNode.CreateType(_tenant, company, "T" + p, "Tipo " + p, _user);
        var cat = ExpenseCategoryNode.CreateCategory(_tenant, company, type, "C" + p, "Categoría " + p, _user);
        db.ExpenseCategoryNodes.AddRange(type, cat);
        var ids = new List<Guid>();
        for (var i = 1; i <= count; i++)
        {
            var sub = ExpenseCategoryNode.CreateSubcategory(_tenant, company, cat, $"S{p}{i}", $"Sub {p}{i}",
                account.Id, _user);
            db.ExpenseCategoryNodes.Add(sub);
            ids.Add(sub.Id);
        }
        return (cat.Id, ids);
    }

    public async Task DisposeAsync()
    {
        await _sp.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private IOperationalContext Ctx(Guid company)
    {
        var m = new Mock<IOperationalContext>();
        m.SetupGet(x => x.TenantId).Returns(_tenant);
        m.SetupGet(x => x.CompanyId).Returns(company);
        m.SetupGet(x => x.UserId).Returns(_user);
        m.SetupGet(x => x.HasCompany).Returns(true);
        m.SetupGet(x => x.HasTenant).Returns(true);
        return m.Object;
    }

    private async Task<Result<SupplierExpenseSubcategoriesDto>> SetAsync(Guid company, params Guid[] ids)
    {
        _activeCompany = company;
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var handler = new SetSupplierExpenseSubcategoriesHandler(
            new SupplierExpenseSubcategoryRepository(db), new BusinessPartnerRepository(db),
            new BusinessPartnerRoleRepository(db), Ctx(company), new PostgresDatabaseExceptionTranslator());
        return await handler.Handle(new SetSupplierExpenseSubcategoriesCommand(_supplier, ids), CancellationToken.None);
    }

    private async Task<Result<SupplierExpenseSubcategoriesDto>> GetAsync(Guid company)
    {
        _activeCompany = company;
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var handler = new GetSupplierExpenseSubcategoriesHandler(
            new SupplierExpenseSubcategoryRepository(db), new BusinessPartnerRepository(db), Ctx(company));
        return await handler.Handle(new GetSupplierExpenseSubcategoriesQuery(_supplier), CancellationToken.None);
    }

    private async Task<SupplierExpenseSubcategoryScope> ScopeAsync(Guid company)
    {
        _activeCompany = company;
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await new SupplierExpenseSubcategoryPolicy(new SupplierExpenseSubcategoryRepository(db))
            .GetScopeAsync(_supplier);
    }

    private async Task<T> RawAsync<T>(Func<ErpDbContext, Task<T>> read)
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await read(db);
    }

    [Fact]
    public async Task Sin_asignaciones_el_modo_es_Unconfigured_y_permite_todo()
    {
        var scope = await ScopeAsync(_companyA);
        scope.Mode.Should().Be(SupplierExpenseSubcategoryMode.Unconfigured);
        scope.Allows(Guid.NewGuid()).Should().BeTrue();
        (await GetAsync(_companyA)).Value!.Mode.Should().Be("Unconfigured");
    }

    [Fact]
    public async Task Put_asigna_reemplaza_y_retira_sin_borrar_filas_y_reactiva_el_mismo_registro()
    {
        (await SetAsync(_companyA, _sub1, _sub2)).IsSuccess.Should().BeTrue();
        var first = (await GetAsync(_companyA)).Value!;
        first.Mode.Should().Be("Restricted");
        first.AvailableSubcategoryIds.Should().BeEquivalentTo([_sub1, _sub2]);

        var rowSub2 = await RawAsync(async db =>
        {
            _activeCompany = _companyA;
            return await db.SupplierExpenseSubcategories.AsNoTracking().SingleAsync(x => x.ExpenseSubcategoryId == _sub2);
        });

        (await SetAsync(_companyA, _sub1, _sub3)).IsSuccess.Should().BeTrue(); // retira sub2, crea sub3
        var second = (await GetAsync(_companyA)).Value!;
        second.AvailableSubcategoryIds.Should().BeEquivalentTo([_sub1, _sub3]);
        second.Items.Should().HaveCount(3, "la fila retirada se conserva (baja lógica)");
        second.Items.Single(i => i.ExpenseSubcategoryId == _sub2).IsActive.Should().BeFalse();

        (await SetAsync(_companyA, _sub1, _sub2, _sub3)).IsSuccess.Should().BeTrue(); // reactiva sub2
        var third = (await GetAsync(_companyA)).Value!;
        third.Items.Should().HaveCount(3, "reactivar no inserta otra fila");
        var reactivated = await RawAsync(async db =>
        {
            _activeCompany = _companyA;
            return await db.SupplierExpenseSubcategories.AsNoTracking().SingleAsync(x => x.ExpenseSubcategoryId == _sub2);
        });
        reactivated.Id.Should().Be(rowSub2.Id, "se reutiliza el registro existente");
        reactivated.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Put_repetido_con_el_mismo_conjunto_es_idempotente_y_no_audita_no_ops()
    {
        (await SetAsync(_companyA, _sub1)).IsSuccess.Should().BeTrue();
        var before = await RawAsync(db => db.SupplierExpenseSubcategoryAudits.CountAsync());
        (await SetAsync(_companyA, _sub1)).IsSuccess.Should().BeTrue();
        (await RawAsync(db => db.SupplierExpenseSubcategoryAudits.CountAsync())).Should().Be(before);
    }

    [Fact]
    public async Task Put_vacio_retira_todas_y_deja_Unconfigured_sin_borrar_filas()
    {
        (await SetAsync(_companyA, _sub1, _sub2)).IsSuccess.Should().BeTrue();
        (await SetAsync(_companyA)).IsSuccess.Should().BeTrue();

        var dto = (await GetAsync(_companyA)).Value!;
        dto.Mode.Should().Be("Unconfigured");
        dto.Items.Should().HaveCount(2).And.OnlyContain(i => !i.IsActive);
        (await ScopeAsync(_companyA)).Allows(_sub3).Should().BeTrue();
        (await SetAsync(_companyA)).IsSuccess.Should().BeTrue("un PUT vacío repetido es no-op");
    }

    [Fact]
    public async Task Subcategoria_asignada_que_se_desactiva_en_el_catalogo_deja_RestrictedEmpty_y_al_reactivarla_vuelve_Restricted()
    {
        (await SetAsync(_companyA, _sub1)).IsSuccess.Should().BeTrue();
        await ToggleNodeAsync(_sub1, active: false);

        var empty = await ScopeAsync(_companyA);
        empty.Mode.Should().Be(SupplierExpenseSubcategoryMode.RestrictedEmpty);
        empty.Allows(_sub1).Should().BeFalse();
        empty.Allows(_sub2).Should().BeFalse("RestrictedEmpty nunca abre el catálogo");

        await ToggleNodeAsync(_sub1, active: true);
        (await ScopeAsync(_companyA)).Mode.Should().Be(SupplierExpenseSubcategoryMode.Restricted);

        (await SetAsync(_companyA)).IsSuccess.Should().BeTrue();
        (await ScopeAsync(_companyA)).Mode.Should().Be(SupplierExpenseSubcategoryMode.Unconfigured);
    }

    private async Task ToggleNodeAsync(Guid nodeId, bool active)
    {
        _activeCompany = _companyA;
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var node = await db.ExpenseCategoryNodes.SingleAsync(x => x.Id == nodeId);
        node.SetActive(active, _user);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Subcategoria_de_otra_empresa_no_es_visible_y_la_configuracion_no_cruza_empresas()
    {
        var cross = await SetAsync(_companyA, _subOfB);
        cross.IsSuccess.Should().BeFalse();
        cross.Code.Should().Be(ApiResponseCodes.Common.NotFound);

        (await SetAsync(_companyA, _sub1)).IsSuccess.Should().BeTrue();
        (await ScopeAsync(_companyA)).Mode.Should().Be(SupplierExpenseSubcategoryMode.Restricted);
        (await ScopeAsync(_companyB)).Mode.Should().Be(SupplierExpenseSubcategoryMode.Unconfigured,
            "la configuración de A no existe para B");
        (await GetAsync(_companyB)).Value!.Items.Should().BeEmpty();

        (await SetAsync(_companyB, _subOfB)).IsSuccess.Should().BeTrue();
        (await ScopeAsync(_companyA)).AvailableIds.Should().BeEquivalentTo([_sub1]);
        (await ScopeAsync(_companyB)).AvailableIds.Should().BeEquivalentTo([_subOfB]);
    }

    [Fact]
    public async Task Rechaza_nodos_que_no_son_subcategoria_inactivos_o_inexistentes()
    {
        var notSub = await SetAsync(_companyA, _categoryA);
        notSub.IsSuccess.Should().BeFalse();
        notSub.Code.Should().Be(ApiResponseCodes.Common.ValidationError);

        var missing = await SetAsync(_companyA, Guid.NewGuid());
        missing.Code.Should().Be(ApiResponseCodes.Common.NotFound);

        await ToggleNodeAsync(_sub3, active: false);
        var inactive = await SetAsync(_companyA, _sub3);
        inactive.IsSuccess.Should().BeFalse();
        inactive.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        await ToggleNodeAsync(_sub3, active: true);
    }

    [Fact]
    public async Task Indice_unico_impide_duplicados_a_nivel_de_base_de_datos()
    {
        (await SetAsync(_companyA, _sub1)).IsSuccess.Should().BeTrue();
        var act = async () =>
        {
            _activeCompany = _companyA;
            await using var scope = _sp.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            db.SupplierExpenseSubcategories.Add(SupplierExpenseSubcategory.Create(
                _tenant, _companyA, _supplier, _sub1, _user));
            await db.SaveChangesAsync();
        };
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.ConstraintName.Should().Be("uq_bpes_company_bp_subcategory");
    }

    [Fact]
    public async Task Dos_PUT_concurrentes_con_el_mismo_conjunto_dejan_una_sola_fila_y_el_perdedor_recibe_Conflict()
    {
        var results = await Task.WhenAll(SetAsync(_companyA, _sub2), SetAsync(_companyA, _sub2));
        results.Count(r => r.IsSuccess).Should().BeGreaterThanOrEqualTo(1);
        results.Where(r => !r.IsSuccess).Should().OnlyContain(r => r.Code == ApiResponseCodes.Common.Conflict);
        var rows = await RawAsync(async db =>
        {
            _activeCompany = _companyA;
            return await db.SupplierExpenseSubcategories.AsNoTracking().CountAsync(x => x.ExpenseSubcategoryId == _sub2);
        });
        rows.Should().Be(1);
    }

    [Fact]
    public async Task Altas_bajas_y_reactivaciones_se_auditan_con_ADR022()
    {
        (await SetAsync(_companyA, _sub1)).IsSuccess.Should().BeTrue();   // Assigned
        (await SetAsync(_companyA)).IsSuccess.Should().BeTrue();          // Disabled
        (await SetAsync(_companyA, _sub1)).IsSuccess.Should().BeTrue();   // Enabled

        var audits = await RawAsync(async db =>
        {
            _activeCompany = _companyA;
            var row = await db.SupplierExpenseSubcategories.AsNoTracking().SingleAsync(x => x.ExpenseSubcategoryId == _sub1);
            return await db.SupplierExpenseSubcategoryAudits.AsNoTracking()
                .Where(a => a.EntityId == row.Id).OrderBy(a => a.OccurredAtUtc).ToListAsync();
        });
        audits.Select(a => a.Action).Should().Equal("Assigned", "Disabled", "Enabled");
        audits.Should().OnlyContain(a =>
            a.TenantId == _tenant && a.CompanyId == _companyA && a.BusinessPartnerId == _supplier
            && a.ExpenseSubcategoryId == _sub1 && a.UserId == _user);
    }
}
