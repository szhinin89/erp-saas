using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F6 — los permisos de anticipos se asignan por la administración oficial
/// de perfiles (catálogo, perfil, permisos y membresía por HTTP, PostgreSQL real) y gobiernan
/// permisos efectivos, menú y API de un usuario no administrador.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CustomerAdvanceProfileAccessHttpTests : IAsyncLifetime
{
    private const string Advances = "/api/v1/finance/customer-advances";
    private const string Profiles = "/api/v1/admin/iam/profiles";
    private const string Route = "/finance/customer-advances";

    private static readonly string[] Actions =
    [
        FinancePermissions.CustomerAdvancesCreate,
        FinancePermissions.CustomerAdvancesApply,
        FinancePermissions.CustomerAdvancesReverse,
        FinancePermissions.CustomerAdvancesRefund,
    ];

    private readonly PostgreSqlTestWebAppFactory _factory = new(useHttpCompanyContext: true);
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _adminId;
    private Guid _userId;
    private string _username = null!;
    private Guid _existingProfileId;
    private Guid _unauthorizedBranchId;
    private Guid _otherCompanyBranchId;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
        await SeedAsync();
        _client = _factory.CreateClient();
        _factory.MutableTenant.TenantId = _tenantId;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task El_catalogo_oficial_expone_la_pantalla_con_sus_cuatro_acciones()
    {
        var catalog = await AdminAsync(HttpMethod.Get, "/api/v1/admin/permissions/catalog");

        var item = catalog
            .GetProperty("groups")
            .EnumerateArray()
            .SelectMany(g => g.GetProperty("categories").EnumerateArray())
            .SelectMany(c => c.GetProperty("items").EnumerateArray())
            .Single(i => i.GetProperty("route").GetString() == Route);
        item.GetProperty("permission").GetString().Should().Be(FinancePermissions.View);
        var actions = item.GetProperty("actions").EnumerateArray().ToList();
        actions
            .Select(a => a.GetProperty("code").GetString())
            .Should()
            .BeEquivalentTo([FinancePermissions.View, .. Actions]);
        actions
            .Select(a => a.GetProperty("label").GetString())
            .Should()
            .NotContain(["apply", "refund", "create", "reverse"]);
    }

    [Fact]
    public async Task Un_perfil_existente_no_recibe_permisos_de_anticipos()
    {
        await AssignProfileAsync(_existingProfileId);

        var menu = await MenuRoutesAsync();

        menu.Should().Contain("/finance/receivables").And.NotContain(Route);
        (await UserAsync(HttpMethod.Get, Advances)).Should().Be(HttpStatusCode.Forbidden);
        foreach (var status in await OperationStatusesAsync())
            status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(FinancePermissions.CustomerAdvancesCreate)]
    [InlineData(FinancePermissions.CustomerAdvancesApply)]
    [InlineData(FinancePermissions.CustomerAdvancesReverse)]
    [InlineData(FinancePermissions.CustomerAdvancesRefund)]
    public async Task Cada_permiso_asignado_desde_el_perfil_habilita_solo_sus_operaciones(
        string? action
    )
    {
        string[] granted = action is null
            ? [FinancePermissions.View]
            : [FinancePermissions.View, action];
        var profile = await CreateProfileAsync(granted);

        // Persistencia leída por la misma administración.
        var stored = await AdminAsync(HttpMethod.Get, $"{Profiles}/{profile}/permissions");
        stored.GetRawText().Should().Contain(FinancePermissions.View);
        foreach (var key in Actions)
            stored.GetRawText().Contains(key, StringComparison.Ordinal).Should().Be(key == action);
        await AssignProfileAsync(profile);

        var effective = await UserDataAsync("/api/v1/admin/iam/me/permissions");
        effective
            .GetProperty("permissions")
            .EnumerateArray()
            .Select(p => p.GetString())
            .Should()
            .BeEquivalentTo(granted);
        (await MenuRoutesAsync()).Should().Contain(Route).And.NotContain("/finance/receivables");
        (await UserAsync(HttpMethod.Get, Advances)).Should().Be(HttpStatusCode.OK);

        // Orden: registrar, aplicar, revertir aplicación, anular, devolver, revertir devolución,
        // revertir ingreso. Sin permiso 403; con permiso la solicitud llega a la operación.
        var allowed = new[]
        {
            action == FinancePermissions.CustomerAdvancesCreate,
            action == FinancePermissions.CustomerAdvancesApply,
            action == FinancePermissions.CustomerAdvancesReverse,
            action == FinancePermissions.CustomerAdvancesReverse,
            action == FinancePermissions.CustomerAdvancesRefund,
            action == FinancePermissions.CustomerAdvancesRefund,
            action == FinancePermissions.CustomerAdvancesRefund,
        };
        var statuses = await OperationStatusesAsync();
        statuses.Select(s => s != HttpStatusCode.Forbidden).Should().Equal(allowed);
    }

    [Fact]
    public async Task Sin_finance_view_no_hay_menu_ni_consulta_aunque_tenga_acciones()
    {
        await AssignProfileAsync(await CreateProfileAsync(Actions));

        (await MenuRoutesAsync()).Should().NotContain(Route);
        (await UserAsync(HttpMethod.Get, Advances)).Should().Be(HttpStatusCode.Forbidden);
        (await UserAsync(HttpMethod.Get, $"{Advances}/{Guid.NewGuid()}"))
            .Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(MasterDataPermissions.BusinessPartnersView)]
    [InlineData(TreasuryPermissions.BankAccountsView)]
    [InlineData(CajaPermissions.View)]
    [InlineData(SalesPermissions.View)]
    public async Task Cada_selector_exige_solo_el_permiso_de_su_recurso(string? extra)
    {
        string[] granted = extra is null
            ? [FinancePermissions.View]
            : [FinancePermissions.View, extra];
        await AssignProfileAsync(await CreateProfileAsync(granted));
        (string Url, string? Permission)[] selectors =
        [
            ("/api/v1/master/business-partners", MasterDataPermissions.BusinessPartnersView),
            ("/api/v1/treasury/bank-accounts", TreasuryPermissions.BankAccountsView),
            ("/api/v1/cash-registers", CajaPermissions.View),
            ("/api/v1/sales-receivables", SalesPermissions.View),
            // Solo sesión: no exigen permiso de negocio.
            ("/api/v1/payment-methods", null),
            ("/api/v1/session/available-branches", null),
        ];

        // Consultar anticipos no exige ningún permiso operativo.
        (await UserAsync(HttpMethod.Get, Advances))
            .Should()
            .Be(HttpStatusCode.OK);
        foreach (var (url, permission) in selectors)
            (await UserAsync(HttpMethod.Get, url))
                .Should()
                .Be(
                    permission is null || permission == extra
                        ? HttpStatusCode.OK
                        : HttpStatusCode.Forbidden,
                    url
                );
    }

    [Fact]
    public async Task Las_sucursales_de_la_sesion_son_solo_las_autorizadas_de_la_empresa()
    {
        await AssignProfileAsync(await CreateProfileAsync([FinancePermissions.View]));

        var data = await UserDataAsync("/api/v1/session/available-branches");

        data.GetProperty("branches")
            .EnumerateArray()
            .Select(b => b.GetProperty("id").GetGuid())
            .Should()
            .Equal(_branchId)
            .And.NotContain([_unauthorizedBranchId, _otherCompanyBranchId]);
    }

    // ── Administración oficial ───────────────────────────────────────────

    private async Task<Guid> CreateProfileAsync(string[] permissions)
    {
        var created = await AdminAsync(
            HttpMethod.Post,
            Profiles,
            new { name = $"CA {Guid.NewGuid():N}"[..20], description = (string?)null },
            HttpStatusCode.Created
        );
        var id = created.GetProperty("id").GetGuid();
        var saved = await AdminAsync(
            HttpMethod.Put,
            $"{Profiles}/{id}/permissions",
            new
            {
                profileId = id,
                items = permissions.Select(p => new { permissionKey = p, isAllowed = true }),
            }
        );
        saved.GetProperty("rejected").GetArrayLength().Should().Be(0, saved.GetRawText());
        return id;
    }

    private Task AssignProfileAsync(Guid profileId) =>
        AdminAsync(
            HttpMethod.Post,
            "/api/v1/admin/iam/memberships",
            new
            {
                username = _username,
                role = "User",
                profileId,
                authorizedBranchIds = new[] { _branchId },
                defaultBranchId = _branchId,
            }
        );

    private async Task<List<string>> MenuRoutesAsync()
    {
        var menu = await UserDataAsync("/api/v1/me/menu");
        var routes = new List<string>();
        Collect(menu, routes);
        return routes;

        static void Collect(JsonElement node, List<string> found)
        {
            if (node.ValueKind == JsonValueKind.Array)
                foreach (var child in node.EnumerateArray())
                    Collect(child, found);
            else if (node.ValueKind == JsonValueKind.Object)
                foreach (var property in node.EnumerateObject())
                    if (property.Value.ValueKind == JsonValueKind.String)
                        found.Add(property.Value.GetString()!);
                    else
                        Collect(property.Value, found);
        }
    }

    private async Task<HttpStatusCode[]> OperationStatusesAsync()
    {
        var id = Guid.NewGuid();
        var evidence = new
        {
            reason = "r",
            reference = "r",
            evidenceDate = "2026-01-01",
            clientRequestId = Guid.NewGuid(),
        };
        (string Url, object Body)[] operations =
        [
            (Advances, new { }),
            ($"{Advances}/{id}/applications", new { }),
            ($"{Advances}/{id}/applications/{id}/reverse", new { }),
            ($"{Advances}/{id}/cancel", new { }),
            ($"{Advances}/{id}/refunds", new { }),
            ($"{Advances}/{id}/refunds/{id}/reverse", evidence),
            ($"{Advances}/{id}/reverse-income", evidence),
        ];
        var statuses = new List<HttpStatusCode>();
        foreach (var (url, body) in operations)
            statuses.Add(await UserAsync(HttpMethod.Post, url, body));
        return [.. statuses];
    }

    // ── HTTP ─────────────────────────────────────────────────────────────

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(
        Guid userId,
        string role,
        HttpMethod method,
        string url,
        object? body
    )
    {
        _factory.MutableUser.UserId = userId;
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(_tenantId, userId, role: role)
        );
        request.Headers.Add("X-Company-Id", _companyId.ToString());
        request.Headers.Add("X-Branch-Id", _branchId.ToString());
        if (body is not null)
            request.Content = JsonContent.Create(body);
        using var response = await _client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonElement> AdminAsync(
        HttpMethod method,
        string url,
        object? body = null,
        HttpStatusCode expected = HttpStatusCode.OK
    )
    {
        var (status, text) = await SendAsync(_adminId, "Admin", method, url, body);
        status.Should().Be(expected, text);
        return JsonDocument.Parse(text).RootElement.GetProperty("data");
    }

    private async Task<HttpStatusCode> UserAsync(
        HttpMethod method,
        string url,
        object? body = null
    ) => (await SendAsync(_userId, "User", method, url, body)).Status;

    private async Task<JsonElement> UserDataAsync(string url)
    {
        var (status, text) = await SendAsync(_userId, "User", HttpMethod.Get, url, null);
        status.Should().Be(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.GetProperty("data");
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seedBy = Guid.NewGuid();
        var tenant = Tenant.Create("CA-PERFILES", $"ca-p-{Guid.NewGuid():N}", seedBy);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _factory.MutableTenant.TenantId = _tenantId;
        var company = Company.CreateManaged(
            _tenantId,
            CompanyIdentityUpdateFixture.ValidRuc(),
            "Empresa Perfiles S.A.",
            createdBy: seedBy
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _companyId = company.Id;
        var otherCompany = Company.CreateManaged(
            _tenantId,
            CompanyIdentityUpdateFixture.ValidRuc(),
            "Otra Empresa S.A.",
            createdBy: seedBy
        );
        db.Companies.Add(otherCompany);
        await db.SaveChangesAsync();
        Branch Extra(Guid companyId, string code, bool main) =>
            Branch.Create(
                _tenantId,
                code,
                "Av. 2",
                code,
                description: null,
                reference: null,
                postalCode: null,
                phone: null,
                secondaryPhone: null,
                email: null,
                website: null,
                managerName: null,
                managerPosition: null,
                managerEmail: null,
                managerPhone: null,
                countryId: null,
                provinceId: null,
                cantonId: null,
                parishId: null,
                latitude: null,
                longitude: null,
                openingDate: null,
                internalNotes: null,
                isMainBranch: main,
                createdBy: seedBy,
                companyId: companyId
            );
        var unauthorized = Extra(_companyId, "B02", main: false);
        var foreign = Extra(otherCompany.Id, "X01", main: true);
        db.Branches.AddRange(unauthorized, foreign);
        (_unauthorizedBranchId, _otherCompanyBranchId) = (unauthorized.Id, foreign.Id);
        var branch = Branch.Create(
            tenantId: _tenantId,
            name: "Matriz",
            address: "Av. 1",
            code: "B01",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: seedBy,
            companyId: _companyId
        );
        IdentityUser NewUser(string prefix) =>
            IdentityUser.Create(
                $"{prefix}-{Guid.NewGuid():N}"[..30],
                "Usuario",
                "Prueba",
                $"{prefix}-{Guid.NewGuid():N}@example.com",
                "hash",
                seedBy
            );
        var admin = NewUser("ca-admin");
        var user = NewUser("ca-user");
        var existing = AccessProfile.Create(_tenantId, _companyId, "Existente", null, seedBy);
        db.Branches.Add(branch);
        db.IdentityUsers.AddRange(admin, user);
        db.AccessProfiles.Add(existing);
        await db.SaveChangesAsync();
        db.AccessProfilePermissions.Add(
            AccessProfilePermission.Create(
                _tenantId,
                existing.Id,
                SalesPermissions.View,
                true,
                seedBy
            )
        );
        db.CompanyUserMemberships.AddRange(
            CompanyUserMembership.Create(_companyId, admin.Id, "Admin", null, seedBy),
            CompanyUserMembership.Create(_companyId, user.Id, "User", null, seedBy)
        );
        await db.SaveChangesAsync();
        (_branchId, _adminId, _userId, _username, _existingProfileId) = (
            branch.Id,
            admin.Id,
            user.Id,
            user.Username,
            existing.Id
        );
    }
}
