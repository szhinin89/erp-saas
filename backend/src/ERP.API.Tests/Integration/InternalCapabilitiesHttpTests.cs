using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;

namespace ERP.API.Tests.Integration;

/// <summary>Brechas V1 internas: HTTP, JWT, permisos/menú y persistencia PostgreSQL reales.
/// Reutiliza el fixture SEC-04 desechable. No accede a instalaciones ni credenciales existentes.</summary>
[Trait("Category", "PostgreSql")]
public sealed partial class InternalCapabilitiesHttpTests : IClassFixture<CrossCompanyAdminPrivilegeHttpFixture>, IDisposable
{
    private readonly CrossCompanyAdminPrivilegeHttpFixture _f;
    private readonly WebApplicationFactory<Program> _app;
    private const string Password = "InternalV1-Test!2026";

    public InternalCapabilitiesHttpTests(CrossCompanyAdminPrivilegeHttpFixture fixture)
    {
        _f = fixture;
        _f.Factory.MutableCompany.CompanyId = _f.CompanyAId;
        _app = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:BasePath"] = Path.Combine(AppContext.BaseDirectory, ".local", "internal-v1-media"),
            }));
            builder.ConfigureTestServices(services =>
            {
                // El fixture original fija usuario/tenant para SEC-04; aquí deben salir del JWT HTTP.
                services.RemoveAll<ICurrentUser>();
                services.RemoveAll<ICurrentTenant>();
                services.AddScoped<ICurrentUser, CurrentUserService>();
                services.AddScoped<ICurrentTenant, CurrentTenantService>();
            });
        });
    }

    public void Dispose() => _app.Dispose();

    private HttpClient Client(string? jwt = null)
    {
        var client = _app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Company-Id", _f.CompanyAId.ToString());
        if (jwt is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    private HttpClient Admin() => Client(TestJwtFactory.CreateSessionJwt(_f.TenantId, _f.MultiCompanyUserId, role: "Admin"));

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<Guid> Profile(HttpClient admin, string permission)
    {
        var profile = await Data(await admin.PostAsJsonAsync("/api/v1/admin/iam/profiles", new { name = $"V1-{Guid.NewGuid():N}", description = "Prueba interna" }));
        var id = profile.GetProperty("id").GetGuid();
        var saved = await Data(await admin.PutAsJsonAsync($"/api/v1/admin/iam/profiles/{id}/permissions", new { profileId = id, items = new[] { new { permissionKey = permission, isAllowed = true } } }));
        saved.GetProperty("rejected").GetArrayLength().Should().Be(0);
        return id;
    }

    private static async Task<(string Username, Guid Id)> CreateUser(HttpClient admin, Guid profileId)
    {
        var username = $"v1-{Guid.NewGuid():N}"[..20];
        var data = await Data(await admin.PostAsJsonAsync("/api/v1/admin/iam/users", new
        {
            username, firstName = "Usuario", lastName = "Interno", email = $"{username}@example.invalid",
            password = Password, role = "User", profileId,
        }));
        return (username, data.GetProperty("identityUserId").GetGuid());
    }

    private static string Cookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("erp_refresh_token=", StringComparison.Ordinal)).Split(';')[0];

    private async Task<(string Jwt, string Cookie)> Login(string username)
    {
        using var client = Client();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = Password });
        var data = await Data(response);
        // Alta administrativa fuerza el cambio inicial: se completa por su endpoint real.
        if (data.TryGetProperty("passwordResetToken", out var reset) && reset.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(reset.GetString()))
        {
            using var completed = await client.PostAsJsonAsync("/api/v1/auth/complete-password-reset", new { passwordResetToken = reset.GetString(), newPassword = Password + "New" });
            data = await Data(completed);
            return (data.GetProperty("token").GetString()!, Cookie(completed));
        }
        return (data.GetProperty("token").GetString()!, Cookie(response));
    }

    private static async Task<HttpResponseMessage> Refresh(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Login_JWT_rotacion_y_reuso_de_familia_persisten_por_HTTP()
    {
        using var admin = Admin();
        var profile = await Profile(admin, "access.profiles.view");
        var user = await CreateUser(admin, profile);
        var login = await Login(user.Username);
        using var client = Client(login.Jwt);
        (await client.GetAsync("/api/v1/admin/iam/profiles")).StatusCode.Should().Be(HttpStatusCode.OK);
        using var otherClient = Client();
        using var otherLogin = await otherClient.PostAsJsonAsync("/api/v1/auth/login", new { username = user.Username, password = Password + "New" });
        await Data(otherLogin);
        var otherCookie = Cookie(otherLogin);
        using var rotated = await Refresh(client, login.Cookie);
        var next = await Data(rotated);
        var nextCookie = Cookie(rotated);
        nextCookie.Equals(login.Cookie, StringComparison.Ordinal).Should().BeFalse();
        using var nextClient = Client(next.GetProperty("token").GetString());
        (await nextClient.GetAsync("/api/v1/admin/iam/profiles")).StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var hash = RefreshTokenService.Hash(Uri.UnescapeDataString(login.Cookie.Split('=', 2)[1]));
        var original = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(t => t.TokenHash == hash);
        original.IsRevoked.Should().BeTrue();
        var successor = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(t => t.ParentTokenId == original.Id);
        successor.FamilyId.Should().Be(original.FamilyId);
        successor.RotationDepth.Should().Be(original.RotationDepth + 1);
        successor.AbsoluteExpiresAt.Should().Be(original.AbsoluteExpiresAt);
        successor.IsRevoked.Should().BeFalse();
        using var benign = await Refresh(client, login.Cookie);
        benign.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await db.Set<RefreshToken>().AsNoTracking().SingleAsync(t => t.Id == successor.Id)).IsRevoked.Should().BeFalse();
        // En la BD desechable se envejece solo el timestamp del token de prueba para evitar sleeps.
        await db.Set<RefreshToken>().Where(t => t.Id == original.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, DateTime.UtcNow.AddHours(-1)));
        using var replay = await Refresh(client, login.Cookie);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await db.Set<RefreshToken>().AsNoTracking().Where(t => t.FamilyId == original.FamilyId).AllAsync(t => t.IsRevoked)).Should().BeTrue();
        using var revokedSuccessor = await Refresh(client, nextCookie);
        revokedSuccessor.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var independent = await Refresh(otherClient, otherCookie);
        independent.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Alta_membership_sucursales_perfiles_menu_y_autorizacion_por_HTTP()
    {
        using var admin = Admin();
        var branch = await AddBranch();
        foreach (var permission in new[] { "access.profiles.view", "access.sessions.view" })
        {
            var profile = await Profile(admin, permission);
            var user = await CreateUser(admin, profile);
            await Data(await admin.PostAsJsonAsync("/api/v1/admin/iam/memberships", new
            {
                username = user.Username, role = "User", profileId = profile,
                authorizedBranchIds = new[] { branch }, defaultBranchId = branch, loginMode = "DirectToDefault",
            }));
            var session = await Login(user.Username);
            using var client = Client(session.Jwt);
            var context = await Data(await client.GetAsync("/api/v1/session/context"));
            context.ToString().Should().Contain(branch.ToString());
            var menu = await Data(await client.GetAsync("/api/v1/me/menu"));
            var routes = Routes(menu).ToArray();
            var expected = permission == "access.profiles.view" ? "/admin/roles" : "/admin/access/sessions";
            var denied = permission == "access.profiles.view" ? "/admin/access/sessions" : "/admin/roles";
            routes.Should().Contain(expected).And.NotContain(denied);
            (await client.GetAsync("/api/v1/admin/iam/profiles")).StatusCode.Should().Be(permission == "access.profiles.view" ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
            (await client.GetAsync("/api/v1/admin/access/sessions")).StatusCode.Should().Be(permission == "access.sessions.view" ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
            using var scope = _f.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var membership = await db.CompanyUserMemberships.AsNoTracking().SingleAsync(m => m.IdentityUserId == user.Id && m.CompanyId == _f.CompanyAId);
            membership.ProfileId.Should().Be(profile);
            var branches = await Data(await admin.GetAsync($"/api/v1/admin/iam/memberships/{membership.Id}/branches"));
            branches.GetProperty("branches").EnumerateArray().Single(b => b.GetProperty("branchId").GetGuid() == branch)
                .GetProperty("authorized").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task Perfil_completo_branding_y_fiscal_se_guardan_y_reabren_por_HTTP()
    {
        using var admin = Admin();
        await Data(await admin.PutAsJsonAsync($"/api/v1/companies/{_f.CompanyAId}", new
        {
            id = _f.CompanyAId, legalName = "Empresa interna V1", tradeName = "Marca interna", isActive = true,
        }));
        var general = new
        {
            corporateEmail = "empresa@example.invalid", phone = "022345678", website = "https://example.invalid",
            currencyCode = "USD", timezone = "America/Guayaquil", legalRepName = "Representante Prueba",
            legalRepPosition = "Gerente", legalRepIdNumber = "1710034065", legalRepEmail = "rep@example.invalid", legalRepPhone = "0991234567",
        };
        await Data(await admin.PutAsJsonAsync("/api/v1/companies/profile", general));
        using var seedScope = _f.Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var taxRegime = await seedDb.Set<ERP.Domain.Modules.SriCatalogs.Entities.SriTaxRegime>().AsNoTracking()
            .Where(r => r.IsActive).Select(r => r.Code).FirstAsync();
        var fiscal = new
        {
            taxRegimeCode = taxRegime, isAccountingReq = false, specialTaxpayerNo = "1234", isForeignTrade = true,
            withholdsRenta = true, withholdsVat = true, retentionAgentResolutionNo = "00006498",
            retentionAgentEffectiveFrom = "2026-11-01", largeTaxpayerResolutionNo = "NAC-DGERCGC24-00000024",
            largeTaxpayerEffectiveFrom = "2026-11-01",
        };
        await Data(await admin.PutAsJsonAsync("/api/v1/companies/profile/fiscal", fiscal));
        await Data(await admin.PutAsJsonAsync("/api/v1/companies/profile/operation", new { languageCode = "en" }));
        await Data(await admin.PutAsJsonAsync("/api/v1/companies/profile/documents", new { extraLegend = "Leyenda interna de prueba" }));
        var branding = new { primaryColor = "#123456", secondaryColor = "#ABCDEF", slogan = "Marca de prueba", documentFooterText = "Pie de prueba" };
        await Data(await admin.PutAsJsonAsync("/api/v1/companies/profile/branding", branding));
        // Nueva sesión HTTP/DbContext: los valores no pueden provenir de estado de formulario/EF.
        using var reopened = Admin();
        var profile = await Data(await reopened.GetAsync("/api/v1/companies/profile"));
        foreach (var field in JsonSerializer.SerializeToElement(general).EnumerateObject())
            profile.GetProperty(field.Name).ToString().Should().Be(field.Value.ToString());
        profile.GetProperty("legalName").GetString().Should().Be("Empresa interna V1");
        profile.GetProperty("tradeName").GetString().Should().Be("Marca interna");
        profile.GetProperty("languageCode").GetString().Should().Be("en");
        profile.GetProperty("extraLegend").GetString().Should().Be("Leyenda interna de prueba");
        profile.GetProperty("retentionAgentResolutionNo").GetString().Should().Be("6498");
        foreach (var field in JsonSerializer.SerializeToElement(fiscal).EnumerateObject().Where(p => p.Name != "retentionAgentResolutionNo"))
            profile.GetProperty(field.Name).ToString().Should().Be(field.Value.ToString());
        var brand = await Data(await reopened.GetAsync("/api/v1/companies/profile/branding"));
        foreach (var field in JsonSerializer.SerializeToElement(branding).EnumerateObject())
            brand.GetProperty(field.Name).ToString().Should().BeEquivalentTo(field.Value.ToString());
    }

    [Fact]
    public async Task Logos_principal_y_alterno_persisten_y_se_descargan_tras_reabrir()
    {
        using var admin = Admin();
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aQZkAAAAASUVORK5CYII=");
        foreach (var role in new[] { "logo", "logo-alt" })
        {
            using var form = new MultipartFormDataContent();
            using var content = new ByteArrayContent(png);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(content, "file", "test.png");
            await Data(await admin.PostAsync($"/api/v1/companies/profile/{role}", form));
        }
        using var reopened = Admin();
        var profile = await Data(await reopened.GetAsync("/api/v1/companies/profile"));
        profile.GetProperty("logo").GetProperty("id").GetGuid().Should().NotBeEmpty();
        profile.GetProperty("alternateLogo").GetProperty("id").GetGuid().Should().NotBeEmpty();
        foreach (var role in new[] { "logo", "logo-alt" })
        {
            using var response = await reopened.GetAsync($"/api/v1/companies/profile/{role}/content");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(png);
        }
    }

    private static IEnumerable<string> Routes(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) foreach (var route in Routes(item)) yield return route;
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name is "route" or "routePath" or "path" && prop.Value.ValueKind == JsonValueKind.String) yield return prop.Value.GetString()!;
                else foreach (var route in Routes(prop.Value)) yield return route;
            }
    }

    private async Task<Guid> AddBranch()
    {
        using var scope = _f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var branch = Branch.Create(_f.TenantId, "Matriz interna", "Av. Prueba", $"V1-{Guid.NewGuid():N}"[..12],
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            isMainBranch: true, _f.MultiCompanyUserId, companyId: _f.CompanyAId);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.Id;
    }
}
