using System.Net;
using System.Net.Http.Headers;
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
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// SEC-04 — el bypass Admin de <c>perm:*</c> se decide por la membership de la empresa operativa
/// (X-Company-Id), nunca por el rol del JWT (emitido para la empresa de apertura de sesión).
/// Usuario multiempresa: Admin en A, User con perfil limitado en B, sin membership en C. Usuario
/// monoempresa: User en A con perfil que incluye <c>access.profiles.view</c>. Administrador global
/// en modo operador: sin membership en ninguna empresa del tenant.
/// Endpoint de prueba <c>GET admin/iam/profiles</c>: sin marcador de empresa, evalúa
/// <c>perm:access.profiles.view</c> (o memberships.view) en runtime — aísla el authorizer.
/// </summary>
public sealed class CrossCompanyAdminPrivilegeHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);

    internal PostgreSqlTestWebAppFactory Factory => _baseFactory;
    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid CompanyCId { get; private set; }
    public Guid MultiCompanyUserId { get; private set; }
    public Guid SingleCompanyUserId { get; private set; }
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
        _baseFactory.MutableUser.UserId = MultiCompanyUserId;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var seedBy = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-SEC04-Test", $"zh-sec04-{Guid.NewGuid():N}", seedBy);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var companyA = NewCompany("Empresa A SEC04", seedBy);
        var companyB = NewCompany("Empresa B SEC04", seedBy);
        var companyC = NewCompany("Empresa C SEC04", seedBy);
        db.Companies.AddRange(companyA, companyB, companyC);
        await db.SaveChangesAsync();
        CompanyAId = companyA.Id;
        CompanyBId = companyB.Id;
        CompanyCId = companyC.Id;

        var limitedProfile = AccessProfile.Create(TenantId, "Limitado SEC04", null, seedBy);
        var profilesViewer = AccessProfile.Create(TenantId, "Lector perfiles SEC04", null, seedBy);
        db.AccessProfiles.AddRange(limitedProfile, profilesViewer);
        await db.SaveChangesAsync();
        db.AccessProfilePermissions.AddRange(
            AccessProfilePermission.Create(
                TenantId,
                limitedProfile.Id,
                AccessPermissions.SessionsView,
                true,
                seedBy
            ),
            AccessProfilePermission.Create(
                TenantId,
                profilesViewer.Id,
                AccessPermissions.ProfilesView,
                true,
                seedBy
            )
        );

        var multi = NewUser("sec04-multi", seedBy);
        var single = NewUser("sec04-single", seedBy);
        var global = NewUser("sec04-global", seedBy);
        db.IdentityUsers.AddRange(multi, single, global);
        await db.SaveChangesAsync();
        MultiCompanyUserId = multi.Id;
        SingleCompanyUserId = single.Id;
        GlobalAdminId = global.Id;

        db.CompanyUserMemberships.AddRange(
            CompanyUserMembership.Create(companyA.Id, multi.Id, "Admin", null, seedBy),
            CompanyUserMembership.Create(companyB.Id, multi.Id, "User", limitedProfile.Id, seedBy),
            CompanyUserMembership.Create(companyA.Id, single.Id, "User", profilesViewer.Id, seedBy)
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
public sealed class CrossCompanyAdminPrivilegeHttpTests
    : IClassFixture<CrossCompanyAdminPrivilegeHttpFixture>
{
    private const string ProfilesEndpoint = "/api/v1/admin/iam/profiles";
    private const string MyPermissionsEndpoint = "/api/v1/admin/iam/me/permissions";
    private readonly CrossCompanyAdminPrivilegeHttpFixture _f;

    public CrossCompanyAdminPrivilegeHttpTests(CrossCompanyAdminPrivilegeHttpFixture fixture) =>
        _f = fixture;

    // JWT emitido al abrir sesión en A (rol Admin de esa membership), como SwitchCompany.
    private string MultiUserJwtFromA() =>
        TestJwtFactory.CreateSessionJwt(_f.TenantId, _f.MultiCompanyUserId, role: "Admin");

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(
        string url,
        string jwt,
        Guid currentUserId,
        Guid? companyId
    )
    {
        var previousUser = _f.Factory.MutableUser.UserId;
        _f.Factory.MutableUser.UserId = currentUserId;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            if (companyId is Guid id)
                request.Headers.Add("X-Company-Id", id.ToString());
            using var response = await _f.Client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            _f.Factory.MutableUser.UserId = previousUser;
        }
    }

    private const string CompanyScopeForbidden = "COMPANY_SCOPE_FORBIDDEN";

    // Header presente sin membership activa: mismo contrato que CompanyScopeBehavior.
    private static void AssertCompanyScopeForbidden((HttpStatusCode Status, string Body) response)
    {
        response.Status.Should().Be(HttpStatusCode.Forbidden, response.Body);
        response.Body.Should().Contain(CompanyScopeForbidden);
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

    [Fact]
    public async Task Admin_en_A_con_JWT_de_A_operando_B_recibe_solo_los_permisos_de_B()
    {
        var profiles = await SendAsync(
            ProfilesEndpoint,
            MultiUserJwtFromA(),
            _f.MultiCompanyUserId,
            _f.CompanyBId
        );
        // Empresa válida (membership activa en B) sin el permiso: rechazo de permiso, no de scope.
        profiles.Status.Should().Be(HttpStatusCode.Forbidden, profiles.Body);
        profiles.Body.Should().NotContain(CompanyScopeForbidden);

        var mine = await SendAsync(
            MyPermissionsEndpoint,
            MultiUserJwtFromA(),
            _f.MultiCompanyUserId,
            _f.CompanyBId
        );
        mine.Status.Should().Be(HttpStatusCode.OK, mine.Body);
        PermissionsOf(mine.Body).Should().BeEquivalentTo([AccessPermissions.SessionsView]);
    }

    [Fact]
    public async Task Admin_en_A_operando_A_conserva_el_bypass_Admin()
    {
        var profiles = await SendAsync(
            ProfilesEndpoint,
            MultiUserJwtFromA(),
            _f.MultiCompanyUserId,
            _f.CompanyAId
        );
        profiles.Status.Should().Be(HttpStatusCode.OK, profiles.Body);

        var mine = await SendAsync(
            MyPermissionsEndpoint,
            MultiUserJwtFromA(),
            _f.MultiCompanyUserId,
            _f.CompanyAId
        );
        PermissionsOf(mine.Body).Should().BeEquivalentTo(["*"]);
    }

    [Fact]
    public async Task Admin_en_A_sin_membership_en_C_es_rechazado()
    {
        var profiles = await SendAsync(
            ProfilesEndpoint,
            MultiUserJwtFromA(),
            _f.MultiCompanyUserId,
            _f.CompanyCId
        );
        AssertCompanyScopeForbidden(profiles);

        var mine = await SendAsync(
            MyPermissionsEndpoint,
            MultiUserJwtFromA(),
            _f.MultiCompanyUserId,
            _f.CompanyCId
        );
        PermissionsOf(mine.Body).Should().BeEmpty();
    }

    [Fact]
    public async Task Header_empresarial_invalido_no_cae_en_la_unica_membership_del_usuario()
    {
        var jwt = TestJwtFactory.CreateSessionJwt(
            _f.TenantId,
            _f.SingleCompanyUserId,
            role: "User"
        );

        // Antes de SEC-04: el header ajeno se ignoraba y se usaba el perfil de A (200).
        var withForeignHeader = await SendAsync(
            ProfilesEndpoint,
            jwt,
            _f.SingleCompanyUserId,
            _f.CompanyBId
        );
        AssertCompanyScopeForbidden(withForeignHeader);

        var withUnknownHeader = await SendAsync(
            ProfilesEndpoint,
            jwt,
            _f.SingleCompanyUserId,
            Guid.NewGuid()
        );
        AssertCompanyScopeForbidden(withUnknownHeader);
    }

    [Fact]
    public async Task Una_membership_sin_header_conserva_el_comportamiento_existente()
    {
        var jwt = TestJwtFactory.CreateSessionJwt(
            _f.TenantId,
            _f.SingleCompanyUserId,
            role: "User"
        );

        var withoutHeader = await SendAsync(ProfilesEndpoint, jwt, _f.SingleCompanyUserId, null);
        withoutHeader.Status.Should().Be(HttpStatusCode.OK, withoutHeader.Body);

        var withOwnHeader = await SendAsync(
            ProfilesEndpoint,
            jwt,
            _f.SingleCompanyUserId,
            _f.CompanyAId
        );
        withOwnHeader.Status.Should().Be(HttpStatusCode.OK, withOwnHeader.Body);
    }

    [Fact]
    public async Task Multiempresa_sin_header_no_resuelve_empresa_ni_bypass()
    {
        var profiles = await SendAsync(
            ProfilesEndpoint,
            MultiUserJwtFromA(),
            _f.MultiCompanyUserId,
            null
        );
        profiles.Status.Should().Be(HttpStatusCode.Forbidden, profiles.Body);
    }

    [Fact]
    public async Task Administrador_global_en_modo_operador_conserva_acceso_transversal()
    {
        var operatorJwt = TestJwtFactory.CreateSessionJwt(
            _f.TenantId,
            _f.GlobalAdminId,
            role: "Admin",
            extraClaims:
            [
                new Claim("operator_mode", "true"),
                new Claim("global_admin_user_id", _f.GlobalAdminId.ToString()),
            ]
        );

        foreach (var companyId in new[] { _f.CompanyAId, _f.CompanyBId, _f.CompanyCId })
        {
            var profiles = await SendAsync(
                ProfilesEndpoint,
                operatorJwt,
                _f.GlobalAdminId,
                companyId
            );
            profiles.Status.Should().Be(HttpStatusCode.OK, profiles.Body);

            var mine = await SendAsync(
                MyPermissionsEndpoint,
                operatorJwt,
                _f.GlobalAdminId,
                companyId
            );
            PermissionsOf(mine.Body).Should().BeEquivalentTo(["*"]);
        }
    }

    [Fact]
    public async Task JWT_Admin_sin_operator_mode_ni_membership_no_obtiene_bypass()
    {
        // El rol Admin del JWT sin claim de operador (ni GlobalUserRole validada en ese modo) no
        // es privilegio global: el administrador global solo opera vía OperateCompany.
        var plainAdminJwt = TestJwtFactory.CreateSessionJwt(
            _f.TenantId,
            _f.GlobalAdminId,
            role: "Admin"
        );

        var profiles = await SendAsync(
            ProfilesEndpoint,
            plainAdminJwt,
            _f.GlobalAdminId,
            _f.CompanyBId
        );
        AssertCompanyScopeForbidden(profiles);
    }
}
