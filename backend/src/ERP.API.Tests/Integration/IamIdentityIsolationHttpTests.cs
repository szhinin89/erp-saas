using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// IAM-V1-F1 — protección de identidades globales y aislamiento estricto por TenantId + CompanyId
/// en la administración empresarial (PostgreSQL real).
///
/// Tenant T1: empresa A (primera empresa: Admin global + Admin propio + operativo), empresa B
/// (usuarios vinculables a A), empresa D (administrada solo por el global, First Run).
/// Tenant T2: empresa C (Admin y usuario propios).
/// </summary>
public sealed class IamIdentityIsolationHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);

    internal PostgreSqlTestWebAppFactory Factory => _baseFactory;
    public HttpClient Client { get; private set; } = null!;

    public Guid Tenant1Id { get; private set; }
    public Guid Tenant2Id { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid CompanyCId { get; private set; }
    public Guid CompanyDId { get; private set; }

    public IdentityUser Global { get; private set; } = null!;
    public IdentityUser AdminA { get; private set; } = null!;
    public IdentityUser OperA { get; private set; } = null!;
    public IdentityUser UserB { get; private set; } = null!;
    public IdentityUser LinkB { get; private set; } = null!;
    public IdentityUser FirstAdminD { get; private set; } = null!;
    public IdentityUser AdminC { get; private set; } = null!;
    public IdentityUser AdminB { get; private set; } = null!;
    public IdentityUser SupportA { get; private set; } = null!;
    public IdentityUser PwdTargetA { get; private set; } = null!;
    public Guid OperativeProfileAId { get; private set; }
    public IdentityUser UserC { get; private set; } = null!;

    public Guid GlobalMembershipAId { get; private set; }
    public Guid OperMembershipAId { get; private set; }
    public Guid GlobalSessionAId { get; private set; }
    public Guid BranchAId { get; private set; }
    public Guid BranchBId { get; private set; }
    public Guid BranchCId { get; private set; }
    public Guid BusinessPartnerEntityId { get; } = Guid.NewGuid();
    public Guid UnsupportedEntityId { get; } = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        Client = _baseFactory.CreateClient();
        _baseFactory.MutableTenant.TenantId = Tenant1Id;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seedBy = Guid.NewGuid();

        var t1 = Tenant.Create("ZH-IAM-T1", $"zh-iam-t1-{Guid.NewGuid():N}", seedBy);
        var t2 = Tenant.Create("ZH-IAM-T2", $"zh-iam-t2-{Guid.NewGuid():N}", seedBy);
        db.Tenants.AddRange(t1, t2);
        await db.SaveChangesAsync();
        Tenant1Id = t1.Id;
        Tenant2Id = t2.Id;

        var a = NewCompany(t1.Id, "Empresa A IAM", seedBy);
        var b = NewCompany(t1.Id, "Empresa B IAM", seedBy);
        var d = NewCompany(t1.Id, "Empresa D IAM", seedBy);
        var c = NewCompany(t2.Id, "Empresa C IAM", seedBy);
        db.Companies.AddRange(a, b, c, d);
        await db.SaveChangesAsync();
        CompanyAId = a.Id;
        CompanyBId = b.Id;
        CompanyCId = c.Id;
        CompanyDId = d.Id;

        var branchA = NewBranch(t1.Id, a.Id, "SUC-A", seedBy);
        var branchB = NewBranch(t1.Id, b.Id, "SUC-B", seedBy);
        var branchC = NewBranch(t2.Id, c.Id, "SUC-C", seedBy);
        db.Branches.AddRange(branchA, branchB, branchC);
        BranchAId = branchA.Id;
        BranchBId = branchB.Id;
        BranchCId = branchC.Id;

        Global = NewUser("iam-global", seedBy);
        AdminA = NewUser("iam-admin-a", seedBy);
        OperA = NewUser("iam-oper-a", seedBy);
        UserB = NewUser("iam-user-b", seedBy);
        LinkB = NewUser("iam-link-b", seedBy);
        FirstAdminD = NewUser("iam-first-d", seedBy);
        AdminC = NewUser("iam-admin-c", seedBy);
        UserC = NewUser("iam-user-c", seedBy);
        AdminB = NewUser("iam-admin-b", seedBy);
        SupportA = NewUser("iam-support-a", seedBy);
        PwdTargetA = NewUser("iam-pwd-a", seedBy);
        db.IdentityUsers.AddRange(
            Global,
            AdminA,
            OperA,
            UserB,
            LinkB,
            FirstAdminD,
            AdminC,
            UserC,
            AdminB,
            SupportA,
            PwdTargetA
        );
        await db.SaveChangesAsync();

        // IAM-V1-F2: perfil de soporte con SOLO contraseña temporal (sin crear usuarios) y un
        // perfil operativo para las vinculaciones de usuarios operativos.
        var supportProfile = AccessProfile.Create(t1.Id, a.Id, "Soporte A", null, seedBy);
        var operativeProfile = AccessProfile.Create(t1.Id, a.Id, "Operativo A", null, seedBy);
        db.AccessProfiles.AddRange(supportProfile, operativeProfile);
        await db.SaveChangesAsync();
        OperativeProfileAId = operativeProfile.Id;
        db.AccessProfilePermissions.AddRange(
            AccessProfilePermission.Create(
                t1.Id,
                supportProfile.Id,
                AccessPermissions.MembershipsView,
                true,
                seedBy
            ),
            AccessProfilePermission.Create(
                t1.Id,
                supportProfile.Id,
                AccessPermissions.IdentityUsersAssignTemporaryPassword,
                true,
                seedBy
            )
        );

        var globalA = CompanyUserMembership.Create(a.Id, Global.Id, "Admin", null, seedBy);
        var operA = CompanyUserMembership.Create(a.Id, OperA.Id, "User", null, seedBy);
        db.CompanyUserMemberships.AddRange(
            globalA,
            CompanyUserMembership.Create(a.Id, AdminA.Id, "Admin", null, seedBy),
            operA,
            CompanyUserMembership.Create(b.Id, UserB.Id, "User", null, seedBy),
            CompanyUserMembership.Create(b.Id, AdminB.Id, "Admin", null, seedBy),
            CompanyUserMembership.Create(a.Id, SupportA.Id, "User", supportProfile.Id, seedBy),
            CompanyUserMembership.Create(
                a.Id,
                PwdTargetA.Id,
                "User",
                operativeProfile.Id,
                seedBy
            ),
            CompanyUserMembership.Create(b.Id, LinkB.Id, "User", null, seedBy),
            CompanyUserMembership.Create(b.Id, FirstAdminD.Id, "User", null, seedBy),
            CompanyUserMembership.Create(d.Id, Global.Id, "Admin", null, seedBy),
            CompanyUserMembership.Create(c.Id, AdminC.Id, "Admin", null, seedBy),
            CompanyUserMembership.Create(c.Id, UserC.Id, "User", null, seedBy)
        );
        db.GlobalUserRoles.Add(GlobalUserRole.Create(Global.Id, "Admin", seedBy));
        await db.SaveChangesAsync();
        GlobalMembershipAId = globalA.Id;
        OperMembershipAId = operA.Id;

        var globalSession = UserSession.Create(t1.Id, a.Id, Global.Id, branchA.Id, "dev-global");
        db.UserSessions.AddRange(
            globalSession,
            UserSession.Create(t1.Id, a.Id, OperA.Id, branchA.Id, "dev-oper-a"),
            UserSession.Create(t1.Id, b.Id, UserB.Id, branchB.Id, "dev-user-b")
        );
        GlobalSessionAId = globalSession.Id;

        db.UserActivities.AddRange(
            UserActivity.Create(
                t1.Id,
                Global.Id,
                Global.Email?.Value,
                Global.FullName,
                "branches",
                "Update",
                "Branch",
                branchA.Id,
                "Cambio hecho por la cuenta global"
            ),
            UserActivity.Create(
                t1.Id,
                AdminA.Id,
                AdminA.Email?.Value,
                AdminA.FullName,
                "branches",
                "Update",
                "Branch",
                branchA.Id,
                "Cambio hecho por el admin de A"
            ),
            UserActivity.Create(
                t1.Id,
                AdminA.Id,
                AdminA.Email?.Value,
                AdminA.FullName,
                "Access",
                "Update",
                "IdentityUser",
                Global.Id,
                "Actividad sobre la identidad global"
            ),
            UserActivity.Create(
                t1.Id,
                AdminB.Id,
                AdminB.Email?.Value,
                AdminB.FullName,
                "branches",
                "Update",
                "Branch",
                branchB.Id,
                "Cambio en la sucursal de B"
            ),
            UserActivity.Create(
                t1.Id,
                AdminB.Id,
                AdminB.Email?.Value,
                AdminB.FullName,
                "masterdata",
                "Create",
                "BusinessPartner",
                BusinessPartnerEntityId,
                "Socio de negocio del tenant"
            ),
            UserActivity.Create(
                t1.Id,
                AdminA.Id,
                AdminA.Email?.Value,
                AdminA.FullName,
                "inventory",
                "Update",
                "Item",
                UnsupportedEntityId,
                "Tipo sin verificación de empresa"
            ),
            UserActivity.Create(
                t2.Id,
                AdminC.Id,
                AdminC.Email?.Value,
                AdminC.FullName,
                "branches",
                "Update",
                "Branch",
                branchC.Id,
                "Cambio en la sucursal de C"
            )
        );
        await db.SaveChangesAsync();
    }

    private static Company NewCompany(Guid tenantId, string legalName, Guid createdBy) =>
        Company.CreateManaged(
            tenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: legalName,
            createdBy: createdBy
        );

    private static Branch NewBranch(Guid tenantId, Guid companyId, string code, Guid createdBy) =>
        Branch.Create(
            tenantId,
            $"Matriz {code}",
            "Av. Principal 123",
            code,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            isMainBranch: true,
            createdBy,
            companyId: companyId
        );

    private static IdentityUser NewUser(string prefix, Guid createdBy) =>
        IdentityUser.Create(
            $"{prefix}-{Guid.NewGuid():N}"[..30],
            "Usuario",
            prefix,
            $"{prefix}-{Guid.NewGuid():N}@example.com",
            "hash-original",
            createdBy
        );
}

[Trait("Category", "PostgreSql")]
public sealed class IamIdentityIsolationHttpTests : IClassFixture<IamIdentityIsolationHttpFixture>
{
    private const string Memberships = "/api/v1/admin/iam/memberships";
    private const string Users = "/api/v1/admin/iam/users";
    private const string Sessions = "/api/v1/admin/access/sessions";
    private const string NonexistentUsername = "iam-no-existe-jamas";
    private const string TempPassword = "Temporal#2026Seguro";

    private readonly IamIdentityIsolationHttpFixture _f;

    public IamIdentityIsolationHttpTests(IamIdentityIsolationHttpFixture fixture) => _f = fixture;

    // ── Infraestructura de request ─────────────────────────────────────────────────────────

    private sealed record Actor(Guid TenantId, Guid UserId, Guid CompanyId, string Jwt);

    private Actor AdminA() =>
        new(_f.Tenant1Id, _f.AdminA.Id, _f.CompanyAId, Jwt(_f.Tenant1Id, _f.AdminA.Id, "Admin"));

    private Actor OperA() =>
        new(_f.Tenant1Id, _f.OperA.Id, _f.CompanyAId, Jwt(_f.Tenant1Id, _f.OperA.Id, "User"));

    private Actor AdminC() =>
        new(_f.Tenant2Id, _f.AdminC.Id, _f.CompanyCId, Jwt(_f.Tenant2Id, _f.AdminC.Id, "Admin"));

    private Actor SupportA() =>
        new(_f.Tenant1Id, _f.SupportA.Id, _f.CompanyAId, Jwt(_f.Tenant1Id, _f.SupportA.Id, "User"));

    private Actor AdminB() =>
        new(_f.Tenant1Id, _f.AdminB.Id, _f.CompanyBId, Jwt(_f.Tenant1Id, _f.AdminB.Id, "Admin"));

    private Actor GlobalIn(Guid companyId) =>
        new(_f.Tenant1Id, _f.Global.Id, companyId, Jwt(_f.Tenant1Id, _f.Global.Id, "Admin"));

    private Actor GlobalOperatorInA() =>
        new(
            _f.Tenant1Id,
            _f.Global.Id,
            _f.CompanyAId,
            TestJwtFactory.CreateSessionJwt(
                _f.Tenant1Id,
                _f.Global.Id,
                role: "Admin",
                extraClaims:
                [
                    new Claim("operator_mode", "true"),
                    new Claim("global_admin_user_id", _f.Global.Id.ToString()),
                ]
            )
        );

    private static string Jwt(Guid tenantId, Guid userId, string role) =>
        TestJwtFactory.CreateSessionJwt(tenantId, userId, role: role);

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(
        Actor actor,
        HttpMethod method,
        string url,
        object? body = null
    )
    {
        var previousUser = _f.Factory.MutableUser.UserId;
        var previousTenant = _f.Factory.MutableTenant.TenantId;
        _f.Factory.MutableUser.UserId = actor.UserId;
        _f.Factory.MutableTenant.TenantId = actor.TenantId;
        try
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", actor.Jwt);
            request.Headers.Add("X-Company-Id", actor.CompanyId.ToString());
            if (body is not null)
                request.Content = JsonContent.Create(body);
            using var response = await _f.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            _f.Factory.MutableUser.UserId = previousUser;
            _f.Factory.MutableTenant.TenantId = previousTenant;
        }
    }

    /// <summary>Cuerpo sin campos volátiles (trazas/tiempos) para comparar respuestas.</summary>
    private static string Normalize(string body)
    {
        var node = JsonNode.Parse(body);
        Strip(node);
        return node?.ToJsonString() ?? string.Empty;

        static void Strip(JsonNode? n)
        {
            if (n is JsonObject obj)
            {
                foreach (
                    var key in obj.Select(p => p.Key)
                        .Where(k =>
                            k.Contains("trace", StringComparison.OrdinalIgnoreCase)
                            || k.Contains("timestamp", StringComparison.OrdinalIgnoreCase)
                            || k.Contains("correlation", StringComparison.OrdinalIgnoreCase)
                            || k.Contains("requestId", StringComparison.OrdinalIgnoreCase)
                        )
                        .ToList()
                )
                    obj.Remove(key);
                foreach (var child in obj.Select(p => p.Value).ToList())
                    Strip(child);
            }
            else if (n is JsonArray arr)
            {
                foreach (var child in arr)
                    Strip(child);
            }
        }
    }

    private static void ShouldBeIdentical(
        (HttpStatusCode Status, string Body) actual,
        (HttpStatusCode Status, string Body) reference
    )
    {
        actual.Status.Should().Be(reference.Status, actual.Body);
        Normalize(actual.Body).Should().Be(Normalize(reference.Body));
    }

    private static IReadOnlyList<Guid> IdsOf(string body, string idProperty)
    {
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        var items = data.ValueKind == JsonValueKind.Array ? data : data.GetProperty("items");
        return items.EnumerateArray().Select(e => e.GetProperty(idProperty).GetGuid()).ToList();
    }

    private async Task<T> InDbAsync<T>(Func<ErpDbContext, Task<T>> query)
    {
        using var scope = _f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await query(db);
    }

    private Task<CompanyUserMembership?> MembershipAsync(Guid companyId, Guid userId) =>
        InDbAsync(db =>
            db.CompanyUserMemberships.IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(m => m.CompanyId == companyId && m.IdentityUserId == userId)
        );

    // ── Listados y búsquedas ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listado_de_A_excluye_global_otras_empresas_y_otros_tenants()
    {
        var list = await SendAsync(AdminA(), HttpMethod.Get, $"{Memberships}?onlyActive=false");

        list.Status.Should().Be(HttpStatusCode.OK, list.Body);
        var ids = IdsOf(list.Body, "identityUserId");
        ids.Should().Contain([_f.AdminA.Id, _f.OperA.Id]);
        ids.Should().NotContain([_f.Global.Id, _f.UserB.Id, _f.UserC.Id, _f.AdminC.Id]);
        list.Body.Should().NotContain(_f.Global.Username);
    }

    [Fact]
    public async Task Global_conserva_su_acceso_a_la_primera_empresa_pero_no_se_ve_a_si_mismo()
    {
        foreach (var actor in new[] { GlobalIn(_f.CompanyAId), GlobalOperatorInA() })
        {
            var list = await SendAsync(actor, HttpMethod.Get, Memberships);
            list.Status.Should().Be(HttpStatusCode.OK, list.Body);
            IdsOf(list.Body, "identityUserId").Should().NotContain(_f.Global.Id);
        }

        var stored = await MembershipAsync(_f.CompanyAId, _f.Global.Id);
        stored!.IsActive.Should().BeTrue();
        stored.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Lookup_de_global_y_de_otro_tenant_es_identico_al_de_un_inexistente()
    {
        var reference = await SendAsync(
            AdminA(),
            HttpMethod.Get,
            $"{Memberships}/lookup?username={NonexistentUsername}"
        );
        reference.Status.Should().Be(HttpStatusCode.OK, reference.Body);

        foreach (var actor in new[] { AdminA(), GlobalOperatorInA() })
        {
            ShouldBeIdentical(
                await SendAsync(actor, HttpMethod.Get, $"{Memberships}/lookup?username={_f.Global.Username}"),
                reference
            );
            ShouldBeIdentical(
                await SendAsync(actor, HttpMethod.Get, $"{Memberships}/lookup?username={_f.UserC.Username}"),
                reference
            );
        }

        // Desde el otro tenant, los usuarios de T1 tampoco existen.
        var fromC = await SendAsync(
            AdminC(),
            HttpMethod.Get,
            $"{Memberships}/lookup?username={_f.OperA.Username}"
        );
        var referenceC = await SendAsync(
            AdminC(),
            HttpMethod.Get,
            $"{Memberships}/lookup?username={NonexistentUsername}"
        );
        ShouldBeIdentical(fromC, referenceC);
    }

    [Fact]
    public async Task Lookup_de_usuario_de_otra_empresa_del_mismo_tenant_permite_vincularlo()
    {
        var lookup = await SendAsync(
            AdminA(),
            HttpMethod.Get,
            $"{Memberships}/lookup?username={_f.UserB.Username}"
        );

        lookup.Status.Should().Be(HttpStatusCode.OK, lookup.Body);
        using var doc = JsonDocument.Parse(lookup.Body);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("identityUserExists").GetBoolean().Should().BeTrue();
        data.GetProperty("membership").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ── Vinculación, modificación y revocación ─────────────────────────────────────────────

    [Fact]
    public async Task Vincular_usuario_del_mismo_tenant_funciona_y_crea_la_membership()
    {
        var link = await SendAsync(
            AdminA(),
            HttpMethod.Post,
            Memberships,
            new
            {
                username = _f.LinkB.Username,
                role = "User",
                profileId = _f.OperativeProfileAId,
            }
        );

        link.Status.Should().Be(HttpStatusCode.OK, link.Body);
        var created = await MembershipAsync(_f.CompanyAId, _f.LinkB.Id);
        created.Should().NotBeNull();
        created!.IsActive.Should().BeTrue();
        // La membership en B no cambia.
        (await MembershipAsync(_f.CompanyBId, _f.LinkB.Id))!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Vincular_o_modificar_global_y_otro_tenant_responde_igual_que_inexistente()
    {
        var reference = await SendAsync(
            AdminA(),
            HttpMethod.Post,
            Memberships,
            new { username = NonexistentUsername, role = "User" }
        );
        reference.Status.Should().Be(HttpStatusCode.NotFound, reference.Body);

        ShouldBeIdentical(
            await SendAsync(
                AdminA(),
                HttpMethod.Post,
                Memberships,
                new { username = _f.UserC.Username, role = "User" }
            ),
            reference
        );
        ShouldBeIdentical(
            await SendAsync(
                AdminA(),
                HttpMethod.Post,
                Memberships,
                new { username = _f.Global.Username, role = "User" }
            ),
            reference
        );

        (await MembershipAsync(_f.CompanyAId, _f.UserC.Id)).Should().BeNull();
        var global = await MembershipAsync(_f.CompanyAId, _f.Global.Id);
        global!.Role.Should().Be("Admin");
        global.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Revocar_global_o_usuario_ajeno_responde_igual_que_inexistente()
    {
        var reference = await SendAsync(
            AdminA(),
            HttpMethod.Post,
            $"{Memberships}/revoke",
            new { username = NonexistentUsername }
        );
        reference.Status.Should().Be(HttpStatusCode.NotFound, reference.Body);

        foreach (var username in new[] { _f.Global.Username, _f.UserB.Username, _f.UserC.Username })
            ShouldBeIdentical(
                await SendAsync(
                    AdminA(),
                    HttpMethod.Post,
                    $"{Memberships}/revoke",
                    new { username }
                ),
                reference
            );

        (await MembershipAsync(_f.CompanyAId, _f.Global.Id))!.IsActive.Should().BeTrue();
        (await MembershipAsync(_f.CompanyBId, _f.UserB.Id))!.IsActive.Should().BeTrue();
        (await MembershipAsync(_f.CompanyCId, _f.UserC.Id))!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Contrasena_temporal_a_global_u_otro_tenant_responde_igual_que_inexistente()
    {
        var reference = await SendAsync(
            AdminA(),
            HttpMethod.Post,
            $"{Users}/{NonexistentUsername}/assign-temporary-password",
            new { temporaryPassword = TempPassword }
        );
        reference.Status.Should().Be(HttpStatusCode.NotFound, reference.Body);

        foreach (var username in new[] { _f.Global.Username, _f.UserB.Username, _f.UserC.Username })
            ShouldBeIdentical(
                await SendAsync(
                    AdminA(),
                    HttpMethod.Post,
                    $"{Users}/{username}/assign-temporary-password",
                    new { temporaryPassword = TempPassword }
                ),
                reference
            );

        var hashes = await InDbAsync(db =>
            db.IdentityUsers.AsNoTracking()
                .Where(u => u.Id == _f.Global.Id || u.Id == _f.UserC.Id || u.Id == _f.UserB.Id)
                .Select(u => u.PasswordHash)
                .ToListAsync()
        );
        hashes.Should().AllBe("hash-original");
    }

    [Fact]
    public async Task Sucursales_y_preferencias_de_la_membership_global_responden_igual_que_inexistente()
    {
        var randomId = Guid.NewGuid();
        var cases = new (HttpMethod Method, Func<Guid, string> Url, object? Body)[]
        {
            (HttpMethod.Get, id => $"{Memberships}/{id}/branches", null),
            (
                HttpMethod.Put,
                id => $"{Memberships}/{id}/branches",
                new { authorizedBranchIds = Array.Empty<Guid>() }
            ),
            (HttpMethod.Get, id => $"/api/v1/admin/iam/company-users/{id}/preferences", null),
            (
                HttpMethod.Put,
                id => $"/api/v1/admin/iam/company-users/{id}/preferences",
                new { loginMode = "AskBranch", defaultBranchId = (Guid?)null }
            ),
        };

        foreach (var (method, url, body) in cases)
        {
            var reference = await SendAsync(AdminA(), method, url(randomId), body);
            reference.Status.Should().Be(HttpStatusCode.NotFound, reference.Body);
            ShouldBeIdentical(
                await SendAsync(AdminA(), method, url(_f.GlobalMembershipAId), body),
                reference
            );
        }

        // Control positivo: un operativo de A sí es administrable.
        var own = await SendAsync(
            AdminA(),
            HttpMethod.Get,
            $"{Memberships}/{_f.OperMembershipAId}/branches"
        );
        own.Status.Should().Be(HttpStatusCode.OK, own.Body);
    }

    [Fact]
    public async Task Crear_usuario_con_username_o_email_ocupados_usa_un_unico_mensaje_generico()
    {
        object Body(string username, string? email) =>
            new
            {
                username,
                firstName = "Nuevo",
                lastName = "Usuario",
                email,
                password = TempPassword,
                role = "User",
            };

        var byGlobalUsername = await SendAsync(
            AdminA(),
            HttpMethod.Post,
            Users,
            Body(_f.Global.Username, null)
        );
        var byOtherTenantUsername = await SendAsync(
            AdminA(),
            HttpMethod.Post,
            Users,
            Body(_f.UserC.Username, null)
        );
        var byGlobalEmail = await SendAsync(
            AdminA(),
            HttpMethod.Post,
            Users,
            Body($"iam-nuevo-{Guid.NewGuid():N}"[..20], _f.Global.Email!.Value)
        );

        byGlobalUsername.Status.Should().Be(HttpStatusCode.Conflict, byGlobalUsername.Body);
        ShouldBeIdentical(byOtherTenantUsername, byGlobalUsername);
        ShouldBeIdentical(byGlobalEmail, byGlobalUsername);
        byGlobalUsername.Body.Should().NotContain("membership");
    }

    // ── Sesiones administrativas ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Sesiones_de_A_no_incluyen_global_ni_otras_empresas_aunque_se_pida_companyId()
    {
        foreach (
            var url in new[]
            {
                $"{Sessions}?pageSize=100",
                $"{Sessions}?pageSize=100&companyId={_f.CompanyBId}",
                $"{Sessions}?pageSize=100&identityUserId={_f.Global.Id}",
            }
        )
        {
            var list = await SendAsync(AdminA(), HttpMethod.Get, url);
            list.Status.Should().Be(HttpStatusCode.OK, list.Body);
            list.Body.Should().NotContain(_f.Global.Id.ToString());
            list.Body.Should().NotContain(_f.UserB.Id.ToString());
            list.Body.Should().NotContain(_f.CompanyBId.ToString());
        }

        var stats = await SendAsync(
            AdminA(),
            HttpMethod.Get,
            $"{Sessions}/statistics?companyId={_f.CompanyBId}"
        );
        stats.Status.Should().Be(HttpStatusCode.OK, stats.Body);
        using var doc = JsonDocument.Parse(stats.Body);
        doc.RootElement.GetProperty("data").GetProperty("active").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cerrar_la_sesion_del_global_responde_igual_que_inexistente_y_no_la_cierra()
    {
        var reference = await SendAsync(AdminA(), HttpMethod.Post, $"{Sessions}/{Guid.NewGuid()}/close");
        reference.Status.Should().Be(HttpStatusCode.NotFound, reference.Body);

        ShouldBeIdentical(
            await SendAsync(AdminA(), HttpMethod.Post, $"{Sessions}/{_f.GlobalSessionAId}/close"),
            reference
        );

        var stored = await InDbAsync(db =>
            db.UserSessions.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == _f.GlobalSessionAId)
        );
        stored.Status.Should().Be(ERP.Domain.Access.Enums.UserSessionStatus.Active);
    }

    // ── Actividad ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Actividad_conserva_el_registro_pero_no_revela_la_identidad_global()
    {
        var onGlobal = await SendAsync(
            AdminA(),
            HttpMethod.Get,
            $"/api/v1/admin/activity/entity?entityType=IdentityUser&entityId={_f.Global.Id}"
        );
        onGlobal.Status.Should().Be(HttpStatusCode.OK, onGlobal.Body);
        using (var doc = JsonDocument.Parse(onGlobal.Body))
            doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);

        var onItem = await SendAsync(
            AdminA(),
            HttpMethod.Get,
            $"/api/v1/admin/activity/entity?entityType=Branch&entityId={_f.BranchAId}"
        );
        onItem.Status.Should().Be(HttpStatusCode.OK, onItem.Body);
        using (var doc = JsonDocument.Parse(onItem.Body))
            doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(2);
        onItem.Body.Should().Contain("Administración del sistema");
        onItem.Body.Should().NotContain(_f.Global.Email!.Value);
        onItem.Body.Should().NotContain(_f.Global.FullName);

        // El registro de auditoría del global se conserva intacto en base.
        var stored = await InDbAsync(db =>
            db.UserActivities.AsNoTracking().CountAsync(a => a.UserId == _f.Global.Id)
        );
        stored.Should().Be(1);
    }

    // ── Actividad por entidad: aislamiento por empresa y tenant (gate final) ───────────────

    private async Task<IReadOnlyList<string>> ActivityDescriptionsAsync(
        Actor actor,
        string entityType,
        Guid entityId
    )
    {
        var response = await SendAsync(
            actor,
            HttpMethod.Get,
            $"/api/v1/admin/activity/entity?entityType={entityType}&entityId={entityId}"
        );
        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        using var doc = JsonDocument.Parse(response.Body);
        return doc
            .RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("description").GetString()!)
            .ToList();
    }

    [Fact]
    public async Task Actividad_de_una_entidad_de_otra_empresa_del_mismo_tenant_no_es_consultable()
    {
        // A conoce el id de la sucursal de B: no ve su actividad (tampoco el operador en A).
        (await ActivityDescriptionsAsync(AdminA(), "Branch", _f.BranchBId)).Should().BeEmpty();
        (await ActivityDescriptionsAsync(GlobalOperatorInA(), "Branch", _f.BranchBId))
            .Should()
            .BeEmpty();

        // B ve la suya y no ve la de A.
        (await ActivityDescriptionsAsync(AdminB(), "Branch", _f.BranchBId))
            .Should()
            .Equal("Cambio en la sucursal de B");
        (await ActivityDescriptionsAsync(AdminB(), "Branch", _f.BranchAId)).Should().BeEmpty();

        // El operador global autorizado conserva la actividad de la empresa que opera.
        (await ActivityDescriptionsAsync(GlobalOperatorInA(), "Branch", _f.BranchAId))
            .Should()
            .HaveCount(2);

        // El registro histórico de B sigue intacto en base.
        (
            await InDbAsync(db =>
                db.UserActivities.AsNoTracking().CountAsync(a => a.EntityId == _f.BranchBId)
            )
        )
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task Actividad_de_otro_tenant_no_es_consultable_aunque_se_conozca_el_id()
    {
        (await ActivityDescriptionsAsync(AdminA(), "Branch", _f.BranchCId)).Should().BeEmpty();
        (await ActivityDescriptionsAsync(AdminC(), "Branch", _f.BranchCId))
            .Should()
            .Equal("Cambio en la sucursal de C");
        (await ActivityDescriptionsAsync(AdminC(), "Branch", _f.BranchAId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Actividad_de_dato_tenant_se_comparte_y_un_tipo_sin_verificacion_es_fail_closed()
    {
        // BusinessPartner es dato TENANT: visible para las empresas del tenant, nunca para otro.
        (await ActivityDescriptionsAsync(AdminA(), "BusinessPartner", _f.BusinessPartnerEntityId))
            .Should()
            .Equal("Socio de negocio del tenant");
        (await ActivityDescriptionsAsync(AdminC(), "BusinessPartner", _f.BusinessPartnerEntityId))
            .Should()
            .BeEmpty();

        // Tipo sin verificación de empresa: nunca se expone, aunque existan registros.
        (await ActivityDescriptionsAsync(AdminA(), "Item", _f.UnsupportedEntityId))
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Actividad_sin_permiso_o_sin_membership_en_la_empresa_se_rechaza()
    {
        var operativo = await SendAsync(
            OperA(),
            HttpMethod.Get,
            $"/api/v1/admin/activity/entity?entityType=Branch&entityId={_f.BranchAId}"
        );
        operativo.Status.Should().Be(HttpStatusCode.Forbidden, operativo.Body);

        // Admin de A declarando la empresa B, donde no tiene membership.
        var foreignHeader = await SendAsync(
            AdminA() with { CompanyId = _f.CompanyBId },
            HttpMethod.Get,
            $"/api/v1/admin/activity/entity?entityType=Branch&entityId={_f.BranchBId}"
        );
        foreignHeader.Status.Should().Be(HttpStatusCode.Forbidden, foreignHeader.Body);
        foreignHeader.Body.Should().NotContain("Cambio en la sucursal de B");
    }

    // ── IAM-V1-F2: contraseña temporal y perfil obligatorio ────────────────────────────────

    [Fact]
    public async Task IAM_V1_F2_contrasena_temporal_solo_exige_su_propio_permiso()
    {
        // Perfil con assign_temporary_password y SIN access.identity_users.create.
        var ok = await SendAsync(
            SupportA(),
            HttpMethod.Post,
            $"{Users}/{_f.PwdTargetA.Username}/assign-temporary-password",
            new { temporaryPassword = TempPassword }
        );
        ok.Status.Should().Be(HttpStatusCode.OK, ok.Body);
        var stored = await InDbAsync(db =>
            db.IdentityUsers.AsNoTracking().SingleAsync(u => u.Id == _f.PwdTargetA.Id)
        );
        stored.PasswordHash.Should().NotBe("hash-original");
        stored.RequirePasswordReset.Should().BeTrue();

        // Ese mismo perfil sigue sin poder crear usuarios.
        var create = await SendAsync(
            SupportA(),
            HttpMethod.Post,
            Users,
            new
            {
                username = $"iam-nuevo-{Guid.NewGuid():N}"[..20],
                firstName = "Nuevo",
                lastName = "Usuario",
                email = (string?)null,
                password = TempPassword,
                role = "User",
                profileId = _f.OperativeProfileAId,
            }
        );
        create.Status.Should().Be(HttpStatusCode.Forbidden, create.Body);

        // Un operativo sin el permiso no puede asignarla.
        var denied = await SendAsync(
            OperA(),
            HttpMethod.Post,
            $"{Users}/{_f.PwdTargetA.Username}/assign-temporary-password",
            new { temporaryPassword = TempPassword }
        );
        denied.Status.Should().Be(HttpStatusCode.Forbidden, denied.Body);
    }

    [Fact]
    public async Task IAM_V1_F2_alta_o_vinculacion_operativa_sin_perfil_se_rechaza_sin_persistir()
    {
        var newUsername = $"iam-sinperfil-{Guid.NewGuid():N}"[..24];
        var create = await SendAsync(
            AdminA(),
            HttpMethod.Post,
            Users,
            new
            {
                username = newUsername,
                firstName = "Sin",
                lastName = "Perfil",
                email = (string?)null,
                password = TempPassword,
                role = "User",
            }
        );
        ((int)create.Status).Should().BeInRange(400, 499, create.Body);
        create.Body.Should().Contain("requiere un perfil de acceso");
        (
            await InDbAsync(db =>
                db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Username == newUsername)
            )
        )
            .Should()
            .BeFalse();

        var link = await SendAsync(
            AdminA(),
            HttpMethod.Post,
            Memberships,
            new { username = _f.UserB.Username, role = "User" }
        );
        ((int)link.Status).Should().BeInRange(400, 499, link.Body);
        link.Body.Should().Contain("requiere un perfil de acceso");
        (await MembershipAsync(_f.CompanyAId, _f.UserB.Id)).Should().BeNull();
    }

    // ── Invariante de administradores y usuarios operativos ────────────────────────────────

    [Fact]
    public async Task Empresa_administrada_solo_por_el_global_recibe_su_primer_admin_y_luego_lo_protege()
    {
        var promote = await SendAsync(
            GlobalIn(_f.CompanyDId),
            HttpMethod.Post,
            Memberships,
            new { username = _f.FirstAdminD.Username, role = "Admin" }
        );
        promote.Status.Should().Be(HttpStatusCode.OK, promote.Body);
        (await MembershipAsync(_f.CompanyDId, _f.FirstAdminD.Id))!.Role.Should().Be("Admin");

        var revokeLastOwn = await SendAsync(
            GlobalIn(_f.CompanyDId),
            HttpMethod.Post,
            $"{Memberships}/revoke",
            new { username = _f.FirstAdminD.Username }
        );
        ((int)revokeLastOwn.Status).Should().BeInRange(400, 499, revokeLastOwn.Body);
        revokeLastOwn.Body.Should().Contain("al menos un administrador activo");
        (await MembershipAsync(_f.CompanyDId, _f.FirstAdminD.Id))!.IsActive.Should().BeTrue();

        // El global sigue con su membership Admin en D.
        (await MembershipAsync(_f.CompanyDId, _f.Global.Id))!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Usuario_operativo_sin_permisos_no_accede_a_IAM()
    {
        foreach (
            var url in new[]
            {
                Memberships,
                $"{Memberships}/lookup?username={_f.AdminA.Username}",
                Sessions,
            }
        )
        {
            var response = await SendAsync(OperA(), HttpMethod.Get, url);
            response.Status.Should().Be(HttpStatusCode.Forbidden, $"{url}: {response.Body}");
        }
    }

    [Fact]
    public async Task Admin_de_otro_tenant_no_ve_identidades_de_T1()
    {
        var list = await SendAsync(AdminC(), HttpMethod.Get, $"{Memberships}?onlyActive=false");

        list.Status.Should().Be(HttpStatusCode.OK, list.Body);
        IdsOf(list.Body, "identityUserId")
            .Should()
            .BeEquivalentTo([_f.AdminC.Id, _f.UserC.Id]);
    }
}
