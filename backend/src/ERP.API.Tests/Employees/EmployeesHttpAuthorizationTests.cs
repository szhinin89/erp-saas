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
    public Guid DisableUser { get; private set; }
    public Guid AssignmentsManageUser { get; private set; }
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
        DisableUser = await NewUserAsync(db, Company1, Branch1, "Baja y reincorporación", [EmployeesPermissions.Disable]);
        AssignmentsManageUser = await NewUserAsync(db, Company1, Branch1, "Asignaciones", [EmployeesPermissions.AssignmentsManage]);
        string[] all =
        [
            EmployeesPermissions.View, EmployeesPermissions.Create, EmployeesPermissions.Update,
            EmployeesPermissions.CatalogsView, EmployeesPermissions.CatalogsManage,
            EmployeesPermissions.Disable, EmployeesPermissions.AssignmentsManage,
        ];
        FullUserC1 = await NewUserAsync(db, Company1, Branch1, "Empleados completo C1", all);
        FullUserC2 = await NewUserAsync(db, Company2, Branch2, "Empleados completo C2", all);
        AdminUser = await NewUserAsync(db, Company1, Branch1, "Admin sin permisos explícitos", role: "Admin");

        SeededEmployeesPermissionRows = 7 + 7 + 7;
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
            { "POST", $"{Base}/{id}/terminate", """{"rowVersion":1,"endDate":"2026-06-30"}""", EmployeesPermissions.Disable },
            { "POST", $"{Base}/{id}/rehire", """{"rowVersion":1,"startDate":"2026-07-01"}""", EmployeesPermissions.Disable },
            { "GET", $"{Base}/{id}/branch-assignments", null, EmployeesPermissions.View },
            { "POST", $"{Base}/{id}/branch-assignments", $$"""{"branchId":"{{Guid.NewGuid()}}","startDate":"2026-03-01"}""", EmployeesPermissions.AssignmentsManage },
            { "POST", $"{Base}/{id}/branch-assignments/{Guid.NewGuid()}/close", """{"rowVersion":1,"endDate":"2026-06-30"}""", EmployeesPermissions.AssignmentsManage },
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
            (EmployeesPermissions.Disable, _f.DisableUser),
            (EmployeesPermissions.AssignmentsManage, _f.AssignmentsManageUser),
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

    [Fact]
    public async Task Flujo_HTTP_real_ciclo_laboral_y_asignaciones_con_aislamiento_y_sin_tocar_el_acceso_ERP()
    {
        // El usuario efectivo de la fábrica es un singleton mutable: cada cliente se crea justo antes de usarse.
        HttpClient C1() => _f.ClientFor(_f.FullUserC1, _f.Company1, _f.Branch1);
        HttpClient C2() => _f.ClientFor(_f.FullUserC2, _f.Company2, _f.Branch2);
        var accessBefore = await _f.WithDbAsync(db => db.Database
            .SqlQuery<int>($"SELECT ((SELECT count(*) FROM company_user_memberships) + (SELECT count(*) FROM company_user_branches) + (SELECT count(*) FROM access_profile_permissions))::int AS \"Value\"")
            .SingleAsync());

        var bp = await _f.NewEmployeeBpAsync();
        using var hire = await C1().PostAsJsonAsync(Base, new { businessPartnerId = bp, startDate = "2026-03-01" });
        hire.StatusCode.Should().Be(HttpStatusCode.Created, await hire.Content.ReadAsStringAsync());
        var employee = await DataAsync(hire);
        var id = employee.GetProperty("id").GetGuid();

        // Asignación: 201 con sucursal proyectada; sucursal de otra empresa → 422 EMPLOYEE_BRANCH_INVALID.
        using var assigned = await C1().PostAsJsonAsync($"{Base}/{id}/branch-assignments", new { branchId = _f.Branch1, startDate = "2026-03-01" });
        assigned.StatusCode.Should().Be(HttpStatusCode.Created, await assigned.Content.ReadAsStringAsync());
        var assignment = await DataAsync(assigned);
        assignment.GetProperty("branchName").GetString().Should().NotBeNullOrEmpty();
        using var badBranch = await C1().PostAsJsonAsync($"{Base}/{id}/branch-assignments", new { branchId = _f.Branch2, startDate = "2026-03-01" });
        badBranch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeAsync(badBranch)).Should().Be("EMPLOYEE_BRANCH_INVALID");
        using var overlap = await C1().PostAsJsonAsync($"{Base}/{id}/branch-assignments", new { branchId = _f.Branch1, startDate = "2026-04-01" });
        overlap.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeAsync(overlap)).Should().Be("EMPLOYEE_ASSIGNMENT_OVERLAP");

        // Lista y filtro por sucursal con fecha de referencia inclusiva.
        using var list = await C1().GetAsync($"{Base}/{id}/branch-assignments");
        (await DataAsync(list)).GetArrayLength().Should().Be(1);
        using var inRange = await C1().GetAsync($"{Base}?branchId={_f.Branch1}&asOf=2026-03-01");
        (await DataAsync(inRange)).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Contain(id);
        using var before = await C1().GetAsync($"{Base}?branchId={_f.Branch1}&asOf=2026-02-28");
        (await DataAsync(before)).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().NotContain(id);

        // Aislamiento: otra empresa no ve la ficha ni sus asignaciones (404 indistinguible).
        using var foreignList = await C2().GetAsync($"{Base}/{id}/branch-assignments");
        using var missingList = await C2().GetAsync($"{Base}/{Guid.NewGuid()}/branch-assignments");
        foreignList.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(foreignList)).Should().Be("EMPLOYEE_NOT_FOUND").And.Be(await CodeAsync(missingList));
        using var foreignTerminate = await C2().PostAsJsonAsync($"{Base}/{id}/terminate", new { rowVersion = 1, endDate = "2026-06-30" });
        foreignTerminate.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Baja: 200, warnings vacío; versión obsoleta → 409; el período se cierra y la asignación vigente también.
        var version = employee.GetProperty("rowVersion").GetUInt32();
        using var stale = await C1().PostAsJsonAsync($"{Base}/{id}/terminate", new { rowVersion = version + 9, endDate = "2026-06-30" });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var terminated = await C1().PostAsJsonAsync($"{Base}/{id}/terminate", new { rowVersion = version, endDate = "2026-06-30", reason = "Fin de contrato" });
        terminated.StatusCode.Should().Be(HttpStatusCode.OK, await terminated.Content.ReadAsStringAsync());
        var afterTerminate = await DataAsync(terminated);
        afterTerminate.GetProperty("isActive").GetBoolean().Should().BeFalse();
        afterTerminate.GetProperty("warnings").GetArrayLength().Should().Be(0);
        afterTerminate.GetProperty("assignments")[0].GetProperty("endDate").GetString().Should().Be("2026-06-30");

        // Cierre de una asignación ya cerrada con versión vigente → 422 DOMAIN_RULE_VIOLATION; obsoleta → 409.
        var assignmentId = assignment.GetProperty("id").GetGuid();
        var currentAssignmentVersion = afterTerminate.GetProperty("assignments")[0].GetProperty("rowVersion").GetUInt32();
        using var closeAgain = await C1().PostAsJsonAsync($"{Base}/{id}/branch-assignments/{assignmentId}/close", new { rowVersion = currentAssignmentVersion, endDate = "2026-07-01" });
        closeAgain.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeAsync(closeAgain)).Should().Be("DOMAIN_RULE_VIOLATION");
        using var closeStale = await C1().PostAsJsonAsync($"{Base}/{id}/branch-assignments/{assignmentId}/close", new { rowVersion = currentAssignmentVersion + 9, endDate = "2026-07-01" });
        closeStale.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Reincorporación: el mismo día de la baja → 400; el siguiente → 200.
        var terminatedVersion = afterTerminate.GetProperty("rowVersion").GetUInt32();
        using var sameDay = await C1().PostAsJsonAsync($"{Base}/{id}/rehire", new { rowVersion = terminatedVersion, startDate = "2026-06-30" });
        sameDay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeAsync(sameDay)).Should().Be("EMPLOYEE_PERIOD_OVERLAP");
        using var rehired = await C1().PostAsJsonAsync($"{Base}/{id}/rehire", new { rowVersion = terminatedVersion, startDate = "2026-07-01" });
        rehired.StatusCode.Should().Be(HttpStatusCode.OK, await rehired.Content.ReadAsStringAsync());
        var afterRehire = await DataAsync(rehired);
        afterRehire.GetProperty("isActive").GetBoolean().Should().BeTrue();
        afterRehire.GetProperty("periods").GetArrayLength().Should().Be(2);
        afterRehire.GetProperty("assignments").GetArrayLength().Should().Be(1, "no se reabren ni se crean asignaciones");

        // La baja y las asignaciones no tocan membresías, sucursales de usuario ni permisos.
        var accessAfter = await _f.WithDbAsync(db => db.Database
            .SqlQuery<int>($"SELECT ((SELECT count(*) FROM company_user_memberships) + (SELECT count(*) FROM company_user_branches) + (SELECT count(*) FROM access_profile_permissions))::int AS \"Value\"")
            .SingleAsync());
        accessAfter.Should().Be(accessBefore);
    }
}
