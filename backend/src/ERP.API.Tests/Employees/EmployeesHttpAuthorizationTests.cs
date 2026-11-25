using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Employees;

/// <summary>
/// EMP-E4 — autorización EFECTIVA de los endpoints de Empleados con el pipeline HTTP real
/// (JWT, <c>RuntimePermissionAuthorizer</c> y <c>CompanyScopeBehavior</c> contra PostgreSQL
/// desechable): sin credenciales 401; sin el permiso 403 sin efectos; con su permiso la solicitud
/// supera la autorización; Admin conserva su comportamiento oficial; la empresa operativa aísla
/// registros (404 indistinguible) y ningún perfil existente recibe permisos de Empleados.
/// Complementa a <c>EmployeesPermissionContractTests</c>, que solo evalúa atributos y policies.
/// </summary>
public sealed class EmployeesHttpAuthorizationFixture : IAsyncLifetime
{
    public const string Role = "Compras";
    private readonly PostgreSqlTestWebAppFactory _factory = new(useHttpCompanyContext: true);
    private Guid _adminId;
    private int _seq;

    public Guid TenantId { get; private set; }
    public Guid Company1 { get; private set; }
    public Guid Company2 { get; private set; }
    public Guid Branch1 { get; private set; }
    public Guid Branch2 { get; private set; }
    public Guid NoPermsUser { get; private set; }
    public Guid ViewUser { get; private set; }
    public Guid CreateUser { get; private set; }
    public Guid UpdateUser { get; private set; }
    public Guid CatalogsViewUser { get; private set; }
    public Guid CatalogsManageUser { get; private set; }
    public Guid FullUserC1 { get; private set; }
    public Guid FullUserC2 { get; private set; }
    public Guid AdminUser { get; private set; }
    public int SeededEmployeesPermissionRows { get; private set; }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
        await SeedAsync();

        _factory.MutableTenant.TenantId = TenantId;
        _factory.MutableUser.Role = Role;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    public HttpClient AnonymousClient() => _factory.CreateClient();

    public HttpClient ClientFor(Guid userId, Guid companyId, Guid branchId, string role = Role)
    {
        _factory.MutableUser.UserId = userId;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, userId, role: role)
        );
        client.DefaultRequestHeaders.Add("X-Company-Id", companyId.ToString());
        client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<ErpDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    /// <summary>BP del tenant con cédula válida y rol Employee activo.</summary>
    public async Task<Guid> NewEmployeeBpAsync()
    {
        var n = Interlocked.Increment(ref _seq);
        return await WithDbAsync(async db =>
        {
            var digits = $"172{n % 1_000_000:000000}".Select(ch => ch - '0').ToArray();
            var sum = 0;
            for (var i = 0; i < 9; i++)
            {
                var p = digits[i] * (i % 2 == 0 ? 2 : 1);
                sum += p > 9 ? p - 9 : p;
            }
            var cedula = string.Concat(digits) + ((10 - sum % 10) % 10);
            var bp = BusinessPartner.Create(TenantId, "05", cedula, null, $"Empleado HTTP {n}", _adminId);
            db.BusinessPartners.Add(bp);
            db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(TenantId, bp.Id, RoleType.Employee, _adminId));
            await db.SaveChangesAsync();
            return bp.Id;
        });
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-EmployeesHttp", $"zh-eh-{Guid.NewGuid():N}"[..16], _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var c1 = Company.CreateManaged(TenantId, "1790000000001", "Empresa Uno S.A.", createdBy: _adminId);
        var c2 = Company.CreateManaged(TenantId, "1790016919001", "Empresa Dos S.A.", createdBy: _adminId);
        db.Companies.AddRange(c1, c2);
        await db.SaveChangesAsync();
        (Company1, Company2) = (c1.Id, c2.Id);

        Branch1 = await NewBranchAsync(db, Company1, "SUC-E1");
        Branch2 = await NewBranchAsync(db, Company2, "SUC-E2");

        NoPermsUser = await NewUserAsync(db, Company1, Branch1, "Sin permisos de empleados", [PurchasePermissions.View]);
        ViewUser = await NewUserAsync(db, Company1, Branch1, "Empleados consulta", [EmployeesPermissions.View]);
        CreateUser = await NewUserAsync(db, Company1, Branch1, "Empleados alta", [EmployeesPermissions.Create]);
        UpdateUser = await NewUserAsync(db, Company1, Branch1, "Empleados edición", [EmployeesPermissions.Update]);
        CatalogsViewUser = await NewUserAsync(db, Company1, Branch1, "Catálogos consulta", [EmployeesPermissions.CatalogsView]);
        CatalogsManageUser = await NewUserAsync(db, Company1, Branch1, "Catálogos gestión", [EmployeesPermissions.CatalogsManage]);
        string[] all =
        [
            EmployeesPermissions.View, EmployeesPermissions.Create, EmployeesPermissions.Update,
            EmployeesPermissions.CatalogsView, EmployeesPermissions.CatalogsManage,
        ];
        FullUserC1 = await NewUserAsync(db, Company1, Branch1, "Empleados completo C1", all);
        FullUserC2 = await NewUserAsync(db, Company2, Branch2, "Empleados completo C2", all);
        AdminUser = await NewUserAsync(db, Company1, Branch1, "Admin sin permisos explícitos", role: "Admin");

        SeededEmployeesPermissionRows = 1 + 1 + 1 + 1 + 1 + 5 + 5;
    }

    private async Task<Guid> NewBranchAsync(ErpDbContext db, Guid companyId, string code)
    {
        var branch = Branch.Create(
            TenantId, $"Matriz {code}", "Av. Principal 123", code,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, isMainBranch: true, _adminId, companyId: companyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.Id;
    }

    private async Task<Guid> NewUserAsync(
        ErpDbContext db, Guid companyId, Guid branchId, string profileName,
        string[]? permissions = null, string role = Role)
    {
        var profile = AccessProfile.Create(TenantId, companyId, profileName, null, _adminId);
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();
        foreach (var key in permissions ?? [])
            db.AccessProfilePermissions.Add(AccessProfilePermission.Create(TenantId, profile.Id, key, true, _adminId));
        var user = IdentityUser.Create(
            $"usuario-{Guid.NewGuid():N}", "Usuario", "Prueba",
            $"usuario-{Guid.NewGuid():N}@example.com", "hash", _adminId);
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        var membership = CompanyUserMembership.Create(companyId, user.Id, role, profile.Id, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();
        db.CompanyUserBranches.Add(CompanyUserBranch.Create(TenantId, companyId, membership.Id, branchId, _adminId));
        await db.SaveChangesAsync();
        return user.Id;
    }
}

[Trait("Category", "PostgreSql")]
public sealed class EmployeesHttpAuthorizationTests : IClassFixture<EmployeesHttpAuthorizationFixture>
{
    private const string Base = "/api/v1/employees";
    private readonly EmployeesHttpAuthorizationFixture _f;

    public EmployeesHttpAuthorizationTests(EmployeesHttpAuthorizationFixture fixture) => _f = fixture;

    /// <summary>Las 11 operaciones: método, ruta, cuerpo y permiso exigido.</summary>
    public static TheoryData<string, string, string?, string> Routes()
    {
        var id = Guid.NewGuid();
        return new TheoryData<string, string, string?, string>
        {
            { "GET", Base, null, EmployeesPermissions.View },
            { "GET", $"{Base}/{id}", null, EmployeesPermissions.View },
            { "POST", Base, $$"""{"businessPartnerId":"{{Guid.NewGuid()}}","startDate":"2026-03-01"}""", EmployeesPermissions.Create },
            { "PATCH", $"{Base}/{id}", """{"rowVersion":1}""", EmployeesPermissions.Update },
            { "PUT", $"{Base}/{id}/specialties", """{"rowVersion":1,"specialtyIds":[]}""", EmployeesPermissions.Update },
            { "GET", $"{Base}/job-positions", null, EmployeesPermissions.CatalogsView },
            { "GET", $"{Base}/professions/{id}", null, EmployeesPermissions.CatalogsView },
            { "POST", $"{Base}/specialties", """{"name":"Autorizacion HTTP"}""", EmployeesPermissions.CatalogsManage },
            { "PATCH", $"{Base}/job-positions/{id}", """{"rowVersion":1,"name":"x"}""", EmployeesPermissions.CatalogsManage },
            { "POST", $"{Base}/professions/{id}/activate", """{"rowVersion":1}""", EmployeesPermissions.CatalogsManage },
            { "POST", $"{Base}/specialties/{id}/deactivate", """{"rowVersion":1}""", EmployeesPermissions.CatalogsManage },
        };
    }

    private static HttpRequestMessage Req(string method, string url, string? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    private static bool PassedAuthorization(HttpStatusCode status) =>
        status is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Sin_credenciales_responde_401_en_cada_operacion(string method, string url, string? body, string _)
    {
        using var client = _f.AnonymousClient();
        using var response = await client.SendAsync(Req(method, url, body));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Perfil_sin_permisos_de_empleados_recibe_403_en_cada_operacion_sin_efectos(
        string method, string url, string? body, string _)
    {
        var catalogsBefore = await CatalogRowsAsync();
        using var client = _f.ClientFor(_f.NoPermsUser, _f.Company1, _f.Branch1);

        using var response = await client.SendAsync(Req(method, url, body));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await CatalogRowsAsync()).Should().Be(catalogsBefore);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Cada_permiso_habilita_solo_sus_operaciones(string method, string url, string? body, string required)
    {
        var users = new (string Permission, Guid User)[]
        {
            (EmployeesPermissions.View, _f.ViewUser),
            (EmployeesPermissions.Create, _f.CreateUser),
            (EmployeesPermissions.Update, _f.UpdateUser),
            (EmployeesPermissions.CatalogsView, _f.CatalogsViewUser),
            (EmployeesPermissions.CatalogsManage, _f.CatalogsManageUser),
        };
        foreach (var (permission, user) in users)
        {
            using var client = _f.ClientFor(user, _f.Company1, _f.Branch1);
            using var response = await client.SendAsync(Req(method, url, body));
            PassedAuthorization(response.StatusCode).Should().Be(
                permission == required,
                $"{permission} sobre {method} {url} → {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Admin_conserva_su_comportamiento_oficial_sin_permisos_explicitos()
    {
        using var client = _f.ClientFor(_f.AdminUser, _f.Company1, _f.Branch1, role: "Admin");
        using var response = await client.GetAsync(Base);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ningun_perfil_existente_recibe_permisos_de_empleados_solo_los_sembrados_por_la_prueba()
    {
        var rows = await _f.WithDbAsync(db =>
            db.AccessProfilePermissions.IgnoreQueryFilters().CountAsync(p => p.PermissionKey.StartsWith("employees.")));
        rows.Should().Be(_f.SeededEmployeesPermissionRows);
        var withoutExplicit = await _f.WithDbAsync(db =>
            db.AccessProfilePermissions.IgnoreQueryFilters()
                .CountAsync(p => p.PermissionKey.StartsWith("employees.") && p.PermissionKey == EmployeesPermissions.Disable));
        withoutExplicit.Should().Be(0);
    }

    private Task<int> CatalogRowsAsync() =>
        _f.WithDbAsync(db => db.Database
            .SqlQuery<int>($"SELECT (SELECT count(*) FROM employee_specialties)::int AS \"Value\"")
            .SingleAsync());

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data");

    private static async Task<string> CodeAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString()!;

    [Fact]
    public async Task Flujo_HTTP_real_catalogos_ficha_PATCH_con_null_y_aislamiento_por_empresa()
    {
        using var c1 = _f.ClientFor(_f.FullUserC1, _f.Company1, _f.Branch1);

        // Catálogo: alta → 201 con rowVersion; renombre vigente → 200; versión obsoleta → 409.
        using var created = await c1.PostAsJsonAsync($"{Base}/job-positions", new { name = "Cajero HTTP" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var catalog = await DataAsync(created);
        var catalogId = catalog.GetProperty("id").GetGuid();
        var v0 = catalog.GetProperty("rowVersion").GetUInt32();

        using var renamed = await c1.PatchAsJsonAsync($"{Base}/job-positions/{catalogId}", new { rowVersion = v0, name = "Cajero Principal" });
        renamed.StatusCode.Should().Be(HttpStatusCode.OK, await renamed.Content.ReadAsStringAsync());
        using var stale = await c1.PatchAsJsonAsync($"{Base}/job-positions/{catalogId}", new { rowVersion = v0, name = "Otro" });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(stale)).Should().Be("CONCURRENCY_CONFLICT");

        // Ficha: alta → 201; PATCH con jobPositionId, luego null explícito, luego omitido.
        var bp = await _f.NewEmployeeBpAsync();
        using var hire = await c1.PostAsJsonAsync(Base, new { businessPartnerId = bp, startDate = "2026-03-01", jobPositionId = catalogId });
        hire.StatusCode.Should().Be(HttpStatusCode.Created, await hire.Content.ReadAsStringAsync());
        var employee = await DataAsync(hire);
        var employeeId = employee.GetProperty("id").GetGuid();
        employee.GetProperty("jobPosition").GetProperty("id").GetGuid().Should().Be(catalogId);
        employee.GetProperty("periods").GetArrayLength().Should().Be(1);

        var version = employee.GetProperty("rowVersion").GetUInt32();
        using var cleared = await c1.PatchAsync($"{Base}/{employeeId}",
            new StringContent($$"""{"rowVersion":{{version}},"jobPositionId":null}""", Encoding.UTF8, "application/json"));
        cleared.StatusCode.Should().Be(HttpStatusCode.OK, await cleared.Content.ReadAsStringAsync());
        var afterClear = await DataAsync(cleared);
        afterClear.GetProperty("jobPosition").ValueKind.Should().Be(JsonValueKind.Null, "null explícito elimina la referencia");

        version = afterClear.GetProperty("rowVersion").GetUInt32();
        using var omitted = await c1.PatchAsync($"{Base}/{employeeId}",
            new StringContent($$"""{"rowVersion":{{version}}}""", Encoding.UTF8, "application/json"));
        omitted.StatusCode.Should().Be(HttpStatusCode.OK);

        // Aislamiento: otra empresa del tenant no ve la ficha ni el catálogo (404 indistinguible de inexistente).
        using var c2 = _f.ClientFor(_f.FullUserC2, _f.Company2, _f.Branch2);
        using var foreignGet = await c2.GetAsync($"{Base}/{employeeId}");
        using var missingGet = await c2.GetAsync($"{Base}/{Guid.NewGuid()}");
        foreignGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(foreignGet)).Should().Be("EMPLOYEE_NOT_FOUND").And.Be(await CodeAsync(missingGet));
        using var foreignPatch = await c2.PatchAsync($"{Base}/{employeeId}",
            new StringContent($$"""{"rowVersion":{{version}},"jobPositionId":null}""", Encoding.UTF8, "application/json"));
        foreignPatch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var foreignCatalog = await c2.GetAsync($"{Base}/job-positions/{catalogId}");
        foreignCatalog.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var foreignList = await c2.GetAsync(Base);
        (await DataAsync(foreignList)).GetProperty("items").GetArrayLength().Should().Be(0);

        // Referencia de catálogo de otra empresa en el alta → 422.
        var otherBp = await _f.NewEmployeeBpAsync();
        using var badRef = await c2.PostAsJsonAsync(Base, new { businessPartnerId = otherBp, startDate = "2026-03-01", jobPositionId = catalogId });
        badRef.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
