using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
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
/// SEC-05 — perfiles de acceso independientes por <c>TenantId + CompanyId</c>. Tenant con
/// empresas A y B; ambas tienen un perfil "Cajero" (mismo nombre, identidad y permisos propios).
/// Usuarios: Admin de A que es User sin permisos en B; delegado no Admin de A con
/// <c>access.company_user_memberships.view</c>; usuario multiempresa con un perfil distinto en A y
/// en B; usuario con perfil que se desactiva; destinos de asignación; administrador global.
/// </summary>
public sealed class CompanyAccessProfilesIsolationHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);

    internal PostgreSqlTestWebAppFactory Factory => _baseFactory;
    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }

    public Guid CashierProfileAId { get; private set; }
    public Guid CashierProfileBId { get; private set; }
    public Guid DelegateProfileAId { get; private set; }
    public Guid RicherProfileAId { get; private set; }
    public Guid SubsetProfileAId { get; private set; }
    public Guid MultiProfileAId { get; private set; }
    public Guid MultiProfileBId { get; private set; }
    public Guid DeactivatedProfileAId { get; private set; }

    public Guid AdminAId { get; private set; }
    public Guid DelegateAId { get; private set; }
    public string DelegateAUsername { get; private set; } = null!;
    public Guid MultiUserId { get; private set; }
    public Guid DeactivatedProfileUserId { get; private set; }
    public string TargetForeignProfileUsername { get; private set; } = null!;
    public string TargetRicherProfileUsername { get; private set; } = null!;
    public string TargetSubsetProfileUsername { get; private set; } = null!;
    public string TargetOwnProfileUsername { get; private set; } = null!;
    public Guid GlobalAdminId { get; private set; }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        Client = _baseFactory.CreateClient();
        _baseFactory.MutableTenant.TenantId = TenantId;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var seedBy = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-SEC05-Test", $"zh-sec05-{Guid.NewGuid():N}", seedBy);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var companyA = NewCompany("Empresa A SEC05", seedBy);
        var companyB = NewCompany("Empresa B SEC05", seedBy);
        db.Companies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();
        CompanyAId = companyA.Id;
        CompanyBId = companyB.Id;

        var cashierA = AccessProfile.Create(TenantId, CompanyAId, "Cajero", null, seedBy);
        var cashierB = AccessProfile.Create(TenantId, CompanyBId, "Cajero", null, seedBy);
        var delegateA = AccessProfile.Create(TenantId, CompanyAId, "Delegado", null, seedBy);
        var richerA = AccessProfile.Create(TenantId, CompanyAId, "Supervisor", null, seedBy);
        var subsetA = AccessProfile.Create(TenantId, CompanyAId, "Auxiliar", null, seedBy);
        var multiA = AccessProfile.Create(TenantId, CompanyAId, "Multi A", null, seedBy);
        var multiB = AccessProfile.Create(TenantId, CompanyBId, "Multi B", null, seedBy);
        var deactivatedA = AccessProfile.Create(TenantId, CompanyAId, "A desactivar", null, seedBy);
        db.AccessProfiles.AddRange(
            cashierA,
            cashierB,
            delegateA,
            richerA,
            subsetA,
            multiA,
            multiB,
            deactivatedA
        );
        await db.SaveChangesAsync();
        CashierProfileAId = cashierA.Id;
        CashierProfileBId = cashierB.Id;
        DelegateProfileAId = delegateA.Id;
        RicherProfileAId = richerA.Id;
        SubsetProfileAId = subsetA.Id;
        MultiProfileAId = multiA.Id;
        MultiProfileBId = multiB.Id;
        DeactivatedProfileAId = deactivatedA.Id;

        AccessProfilePermission Grant(AccessProfile profile, string key) =>
            AccessProfilePermission.Create(TenantId, profile.Id, key, true, seedBy);

        db.AccessProfilePermissions.AddRange(
            Grant(cashierA, AccessPermissions.SessionsView),
            Grant(cashierB, AccessPermissions.SessionsClose),
            Grant(delegateA, AccessPermissions.MembershipsView),
            Grant(richerA, AccessPermissions.MembershipsView),
            Grant(richerA, AccessPermissions.SessionsView),
            Grant(subsetA, AccessPermissions.MembershipsView),
            Grant(multiA, AccessPermissions.SessionsView),
            Grant(multiB, AccessPermissions.ProfilesView),
            Grant(deactivatedA, AccessPermissions.SessionsView)
        );

        var adminA = NewUser("sec05-admin-a", seedBy);
        var delegateUser = NewUser("sec05-delegate", seedBy);
        var multi = NewUser("sec05-multi", seedBy);
        var deactivatedUser = NewUser("sec05-deact", seedBy);
        var targetForeign = NewUser("sec05-t-foreign", seedBy);
        var targetRicher = NewUser("sec05-t-richer", seedBy);
        var targetSubset = NewUser("sec05-t-subset", seedBy);
        var targetOwn = NewUser("sec05-t-own", seedBy);
        var global = NewUser("sec05-global", seedBy);
        db.IdentityUsers.AddRange(
            adminA,
            delegateUser,
            multi,
            deactivatedUser,
            targetForeign,
            targetRicher,
            targetSubset,
            targetOwn,
            global
        );
        await db.SaveChangesAsync();
        AdminAId = adminA.Id;
        DelegateAId = delegateUser.Id;
        DelegateAUsername = delegateUser.Username;
        MultiUserId = multi.Id;
        DeactivatedProfileUserId = deactivatedUser.Id;
        TargetForeignProfileUsername = targetForeign.Username;
        TargetRicherProfileUsername = targetRicher.Username;
        TargetSubsetProfileUsername = targetSubset.Username;
        TargetOwnProfileUsername = targetOwn.Username;
        GlobalAdminId = global.Id;

        db.CompanyUserMemberships.AddRange(
            CompanyUserMembership.Create(CompanyAId, adminA.Id, "Admin", null, seedBy),
            CompanyUserMembership.Create(CompanyBId, adminA.Id, "User", cashierB.Id, seedBy),
            CompanyUserMembership.Create(CompanyAId, delegateUser.Id, "User", delegateA.Id, seedBy),
            CompanyUserMembership.Create(CompanyAId, multi.Id, "User", multiA.Id, seedBy),
            CompanyUserMembership.Create(CompanyBId, multi.Id, "User", multiB.Id, seedBy),
            CompanyUserMembership.Create(
                CompanyAId,
                deactivatedUser.Id,
                "User",
                deactivatedA.Id,
                seedBy
            )
        );
        db.GlobalUserRoles.Add(GlobalUserRole.Create(global.Id, "Admin", seedBy));
        await db.SaveChangesAsync();
    }

    private Company NewCompany(string legalName, Guid createdBy) =>
        Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: legalName,
            createdBy: createdBy
        );

    private static IdentityUser NewUser(string prefix, Guid createdBy) =>
        IdentityUser.Create(
            $"{prefix}-{Guid.NewGuid():N}"[..30],
            "Usuario",
            "Prueba",
            $"{prefix}-{Guid.NewGuid():N}@example.com",
            "hash",
            createdBy
        );
}

[Trait("Category", "PostgreSql")]
public sealed class CompanyAccessProfilesIsolationHttpTests
    : IClassFixture<CompanyAccessProfilesIsolationHttpFixture>
{
    private const string ProfilesEndpoint = "/api/v1/admin/iam/profiles";
    private const string MembershipsEndpoint = "/api/v1/admin/iam/memberships";
    private const string MyPermissionsEndpoint = "/api/v1/admin/iam/me/permissions";
    private readonly CompanyAccessProfilesIsolationHttpFixture _f;

    public CompanyAccessProfilesIsolationHttpTests(
        CompanyAccessProfilesIsolationHttpFixture fixture
    ) => _f = fixture;

    private string Jwt(Guid userId, string role = "User") =>
        TestJwtFactory.CreateSessionJwt(_f.TenantId, userId, role: role);

    private string OperatorJwt() =>
        TestJwtFactory.CreateSessionJwt(
            _f.TenantId,
            _f.GlobalAdminId,
            role: "Admin",
            extraClaims:
            [
                new Claim("operator_mode", "true"),
                new Claim("global_admin_user_id", _f.GlobalAdminId.ToString()),
            ]
        );

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(
        HttpMethod method,
        string url,
        string jwt,
        Guid currentUserId,
        Guid companyId,
        object? body = null
    )
    {
        var previousUser = _f.Factory.MutableUser.UserId;
        _f.Factory.MutableUser.UserId = currentUserId;
        try
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            request.Headers.Add("X-Company-Id", companyId.ToString());
            if (body is not null)
                request.Content = JsonContent.Create(body);
            using var response = await _f.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            _f.Factory.MutableUser.UserId = previousUser;
        }
    }

    private static IReadOnlyList<(Guid Id, string Name)> ProfilesOf(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc
            .RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(e => (e.GetProperty("id").GetGuid(), e.GetProperty("name").GetString()!))
            .ToList();
    }

    private static IReadOnlyList<string> PermissionsOf(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc
            .RootElement.GetProperty("data")
            .GetProperty("permissions")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();
    }

    private async Task<IReadOnlyList<(string Key, bool Allowed)>> StoredPermissionsAsync(
        Guid profileId
    )
    {
        using var scope = _f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return (
            await db
                .AccessProfilePermissions.Where(p => p.ProfileId == profileId)
                .Select(p => new { p.PermissionKey, p.IsAllowed })
                .ToListAsync()
        )
            .Select(p => (p.PermissionKey, p.IsAllowed))
            .ToList();
    }

    private async Task<CompanyUserMembership?> MembershipAsync(Guid companyId, string username)
    {
        using var scope = _f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var user = await db.IdentityUsers.SingleAsync(u => u.Username == username);
        return await db.CompanyUserMemberships.SingleOrDefaultAsync(m =>
            m.CompanyId == companyId && m.IdentityUserId == user.Id
        );
    }

    // ── 1 / 3: Admin A administra perfiles de A; nombres repetibles entre empresas ─────────

    [Fact]
    public async Task Admin_A_lista_solo_los_perfiles_de_A()
    {
        var list = await SendAsync(
            HttpMethod.Get,
            $"{ProfilesEndpoint}?onlyActive=false",
            Jwt(_f.AdminAId, "Admin"),
            _f.AdminAId,
            _f.CompanyAId
        );

        list.Status.Should().Be(HttpStatusCode.OK, list.Body);
        var ids = ProfilesOf(list.Body).Select(p => p.Id).ToList();
        ids.Should().Contain(_f.CashierProfileAId);
        ids.Should().NotContain(_f.CashierProfileBId);
        ids.Should().NotContain(_f.MultiProfileBId);
    }

    [Fact]
    public async Task Admin_A_crea_un_perfil_con_el_mismo_nombre_que_uno_de_B_sin_compartir_permisos()
    {
        var name = $"Compartido {Guid.NewGuid():N}"[..24];
        var operatorInB = await SendAsync(
            HttpMethod.Post,
            ProfilesEndpoint,
            OperatorJwt(),
            _f.GlobalAdminId,
            _f.CompanyBId,
            new { name, description = (string?)null }
        );
        operatorInB.Status.Should().Be(HttpStatusCode.Created, operatorInB.Body);

        var adminInA = await SendAsync(
            HttpMethod.Post,
            ProfilesEndpoint,
            Jwt(_f.AdminAId, "Admin"),
            _f.AdminAId,
            _f.CompanyAId,
            new { name, description = (string?)null }
        );
        adminInA.Status.Should().Be(HttpStatusCode.Created, adminInA.Body);

        Guid IdOf(string body)
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        }

        var profileBId = IdOf(operatorInB.Body);
        var profileAId = IdOf(adminInA.Body);
        profileAId.Should().NotBe(profileBId);

        var grant = await SendAsync(
            HttpMethod.Put,
            $"{ProfilesEndpoint}/{profileAId}/permissions",
            Jwt(_f.AdminAId, "Admin"),
            _f.AdminAId,
            _f.CompanyAId,
            new
            {
                profileId = profileAId,
                items = new[]
                {
                    new { permissionKey = AccessPermissions.SessionsView, isAllowed = true },
                },
            }
        );
        grant.Status.Should().Be(HttpStatusCode.OK, grant.Body);

        (await StoredPermissionsAsync(profileAId))
            .Should()
            .BeEquivalentTo([(AccessPermissions.SessionsView, true)]);
        (await StoredPermissionsAsync(profileBId)).Should().BeEmpty();
    }

    // ── 2: Admin A no consulta ni modifica perfiles de B ────────────────────────────────

    [Fact]
    public async Task Admin_A_no_consulta_ni_modifica_perfiles_de_B_desde_A()
    {
        var jwt = Jwt(_f.AdminAId, "Admin");

        var read = await SendAsync(
            HttpMethod.Get,
            $"{ProfilesEndpoint}/{_f.CashierProfileBId}/permissions",
            jwt,
            _f.AdminAId,
            _f.CompanyAId
        );
        read.Status.Should().NotBe(HttpStatusCode.OK, read.Body);
        read.Body.Should().NotContain(AccessPermissions.SessionsClose);

        var grant = await SendAsync(
            HttpMethod.Put,
            $"{ProfilesEndpoint}/{_f.CashierProfileBId}/permissions",
            jwt,
            _f.AdminAId,
            _f.CompanyAId,
            new
            {
                profileId = _f.CashierProfileBId,
                items = new[]
                {
                    new { permissionKey = AccessPermissions.ProfilesView, isAllowed = true },
                    new { permissionKey = AccessPermissions.SessionsClose, isAllowed = false },
                },
            }
        );
        grant.Status.Should().NotBe(HttpStatusCode.OK, grant.Body);

        var deactivate = await SendAsync(
            HttpMethod.Put,
            $"{ProfilesEndpoint}/{_f.CashierProfileBId}",
            jwt,
            _f.AdminAId,
            _f.CompanyAId,
            new
            {
                profileId = _f.CashierProfileBId,
                name = "Cajero",
                description = (string?)null,
                isActive = false,
            }
        );
        deactivate.Status.Should().NotBe(HttpStatusCode.OK, deactivate.Body);

        (await StoredPermissionsAsync(_f.CashierProfileBId))
            .Should()
            .BeEquivalentTo([(AccessPermissions.SessionsClose, true)]);
    }

    [Fact]
    public async Task Admin_A_que_es_User_en_B_no_administra_perfiles_desde_B()
    {
        var list = await SendAsync(
            HttpMethod.Get,
            ProfilesEndpoint,
            Jwt(_f.AdminAId, "Admin"),
            _f.AdminAId,
            _f.CompanyBId
        );

        list.Status.Should().Be(HttpStatusCode.Forbidden, list.Body);
    }

    // ── 4: membership de A no recibe perfiles de B ─────────────────────────────────────

    [Fact]
    public async Task Membership_de_A_no_puede_recibir_un_perfil_de_B()
    {
        var upsert = await SendAsync(
            HttpMethod.Post,
            MembershipsEndpoint,
            Jwt(_f.AdminAId, "Admin"),
            _f.AdminAId,
            _f.CompanyAId,
            new
            {
                username = _f.TargetForeignProfileUsername,
                role = "User",
                profileId = _f.CashierProfileBId,
            }
        );

        upsert.Status.Should().Be(HttpStatusCode.NotFound, upsert.Body);
        (await MembershipAsync(_f.CompanyAId, _f.TargetForeignProfileUsername))
            .Should()
            .BeNull();
    }

    // ── 5 / 6: no Admin no se autoescala ────────────────────────────────────────────────

    [Fact]
    public async Task No_Admin_no_puede_asignarse_el_rol_Admin()
    {
        var upsert = await SendAsync(
            HttpMethod.Post,
            MembershipsEndpoint,
            Jwt(_f.DelegateAId),
            _f.DelegateAId,
            _f.CompanyAId,
            new { username = _f.DelegateAUsername, role = "Admin" }
        );

        upsert.Status.Should().Be(HttpStatusCode.Forbidden, upsert.Body);
        var membership = await MembershipAsync(_f.CompanyAId, _f.DelegateAUsername);
        membership!.Role.Should().Be("User");
        membership.ProfileId.Should().Be(_f.DelegateProfileAId);
    }

    [Fact]
    public async Task No_Admin_no_puede_asignar_un_perfil_superior_al_propio()
    {
        var upsert = await SendAsync(
            HttpMethod.Post,
            MembershipsEndpoint,
            Jwt(_f.DelegateAId),
            _f.DelegateAId,
            _f.CompanyAId,
            new
            {
                username = _f.TargetRicherProfileUsername,
                role = "User",
                profileId = _f.RicherProfileAId,
            }
        );

        upsert.Status.Should().Be(HttpStatusCode.Forbidden, upsert.Body);
        upsert.Body.Should().Contain(AccessPermissions.SessionsView);
        (await MembershipAsync(_f.CompanyAId, _f.TargetRicherProfileUsername))
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task No_Admin_puede_asignar_un_perfil_dentro_de_su_alcance()
    {
        var upsert = await SendAsync(
            HttpMethod.Post,
            MembershipsEndpoint,
            Jwt(_f.DelegateAId),
            _f.DelegateAId,
            _f.CompanyAId,
            new
            {
                username = _f.TargetSubsetProfileUsername,
                role = "User",
                profileId = _f.SubsetProfileAId,
            }
        );

        upsert.Status.Should().Be(HttpStatusCode.OK, upsert.Body);
        (await MembershipAsync(_f.CompanyAId, _f.TargetSubsetProfileUsername))!
            .ProfileId.Should()
            .Be(_f.SubsetProfileAId);
    }

    // ── 7: Admin legítimo asigna perfil de su empresa ──────────────────────────────────

    [Fact]
    public async Task Admin_A_asigna_un_perfil_de_A()
    {
        var upsert = await SendAsync(
            HttpMethod.Post,
            MembershipsEndpoint,
            Jwt(_f.AdminAId, "Admin"),
            _f.AdminAId,
            _f.CompanyAId,
            new
            {
                username = _f.TargetOwnProfileUsername,
                role = "User",
                profileId = _f.CashierProfileAId,
            }
        );

        upsert.Status.Should().Be(HttpStatusCode.OK, upsert.Body);
        var membership = await MembershipAsync(_f.CompanyAId, _f.TargetOwnProfileUsername);
        membership!.ProfileId.Should().Be(_f.CashierProfileAId);
        membership.Role.Should().Be("User");
    }

    // ── 8: perfil desactivado no concede permisos ──────────────────────────────────────

    [Fact]
    public async Task Perfil_desactivado_deja_de_conceder_permisos_efectivos()
    {
        var userJwt = Jwt(_f.DeactivatedProfileUserId);
        var before = await SendAsync(
            HttpMethod.Get,
            MyPermissionsEndpoint,
            userJwt,
            _f.DeactivatedProfileUserId,
            _f.CompanyAId
        );
        PermissionsOf(before.Body).Should().BeEquivalentTo([AccessPermissions.SessionsView]);

        var deactivate = await SendAsync(
            HttpMethod.Put,
            $"{ProfilesEndpoint}/{_f.DeactivatedProfileAId}",
            Jwt(_f.AdminAId, "Admin"),
            _f.AdminAId,
            _f.CompanyAId,
            new
            {
                profileId = _f.DeactivatedProfileAId,
                name = "A desactivar",
                description = (string?)null,
                isActive = false,
            }
        );
        deactivate.Status.Should().Be(HttpStatusCode.OK, deactivate.Body);

        var after = await SendAsync(
            HttpMethod.Get,
            MyPermissionsEndpoint,
            userJwt,
            _f.DeactivatedProfileUserId,
            _f.CompanyAId
        );
        after.Status.Should().Be(HttpStatusCode.OK, after.Body);
        PermissionsOf(after.Body).Should().BeEmpty();
    }

    // ── 9: Administrador Global en modo operador ───────────────────────────────────────

    [Fact]
    public async Task Administrador_global_en_modo_operador_administra_perfiles_de_cualquier_empresa()
    {
        foreach (
            var (companyId, ownProfile, foreignProfile) in new[]
            {
                (_f.CompanyAId, _f.CashierProfileAId, _f.CashierProfileBId),
                (_f.CompanyBId, _f.CashierProfileBId, _f.CashierProfileAId),
            }
        )
        {
            var list = await SendAsync(
                HttpMethod.Get,
                $"{ProfilesEndpoint}?onlyActive=false",
                OperatorJwt(),
                _f.GlobalAdminId,
                companyId
            );
            list.Status.Should().Be(HttpStatusCode.OK, list.Body);
            var ids = ProfilesOf(list.Body).Select(p => p.Id).ToList();
            ids.Should().Contain(ownProfile);
            ids.Should().NotContain(foreignProfile);

            var read = await SendAsync(
                HttpMethod.Get,
                $"{ProfilesEndpoint}/{ownProfile}/permissions",
                OperatorJwt(),
                _f.GlobalAdminId,
                companyId
            );
            read.Status.Should().Be(HttpStatusCode.OK, read.Body);
        }
    }

    // ── 10: cambio de empresa → permisos y navegación de la empresa seleccionada ────────

    [Fact]
    public async Task Cambio_de_empresa_resuelve_los_permisos_del_perfil_de_esa_empresa()
    {
        var jwt = Jwt(_f.MultiUserId);

        var inA = await SendAsync(
            HttpMethod.Get,
            MyPermissionsEndpoint,
            jwt,
            _f.MultiUserId,
            _f.CompanyAId
        );
        inA.Status.Should().Be(HttpStatusCode.OK, inA.Body);
        PermissionsOf(inA.Body).Should().BeEquivalentTo([AccessPermissions.SessionsView]);

        var inB = await SendAsync(
            HttpMethod.Get,
            MyPermissionsEndpoint,
            jwt,
            _f.MultiUserId,
            _f.CompanyBId
        );
        inB.Status.Should().Be(HttpStatusCode.OK, inB.Body);
        PermissionsOf(inB.Body).Should().BeEquivalentTo([AccessPermissions.ProfilesView]);

        // El perfil de B concede access.profiles.view solo en B: en B lista perfiles de B, en A no.
        var listInB = await SendAsync(
            HttpMethod.Get,
            ProfilesEndpoint,
            jwt,
            _f.MultiUserId,
            _f.CompanyBId
        );
        listInB.Status.Should().Be(HttpStatusCode.OK, listInB.Body);
        ProfilesOf(listInB.Body).Select(p => p.Id).Should().Contain(_f.MultiProfileBId);

        var listInA = await SendAsync(
            HttpMethod.Get,
            ProfilesEndpoint,
            jwt,
            _f.MultiUserId,
            _f.CompanyAId
        );
        listInA.Status.Should().Be(HttpStatusCode.Forbidden, listInA.Body);
    }
}
