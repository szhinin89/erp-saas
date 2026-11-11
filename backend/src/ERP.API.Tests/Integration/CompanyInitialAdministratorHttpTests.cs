using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// IAM-V1-F3 — administrador inicial al crear una empresa desde Admin Core y regeneración de su
/// contraseña temporal. HTTP real + PostgreSQL real: credenciales mostradas una sola vez (nunca en
/// consultas posteriores), primer ingreso con cambio obligatorio, duplicados con 409 genérico,
/// acceso restringido al administrador global y regeneración que invalida la contraseña anterior.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CompanyInitialAdministratorHttpTests : IClassFixture<CompanyIdentityUpdateFixture>
{
    private readonly CompanyIdentityUpdateFixture _f;

    public CompanyInitialAdministratorHttpTests(CompanyIdentityUpdateFixture f) => _f = f;

    private sealed record Created(Guid CompanyId, string Username, string Password, string Raw);

    private object Body(string username, string email) =>
        new
        {
            tenantId = _f.TenantId,
            taxId = CompanyIdentityUpdateFixture.ValidRuc(),
            legalName = "Empresa con administrador",
            tradeName = "ConAdmin",
            initialAdministrator = new
            {
                username,
                firstName = "Ana",
                lastName = "Administradora",
                email,
            },
        };

    private async Task<(HttpResponseMessage Http, string Text)> PostCompanyAsync(object body)
    {
        using var client = _f.Client(global: true);
        var http = await client.PostAsJsonAsync("/api/v1/companies", body);
        return (http, await http.Content.ReadAsStringAsync());
    }

    private async Task<Created> CreateAsync(string suffix)
    {
        var (http, text) = await PostCompanyAsync(Body($"adm.{suffix}", $"adm.{suffix}@example.test"));
        http.StatusCode.Should().Be(HttpStatusCode.Created, text);
        http.Headers.CacheControl!.NoStore.Should().BeTrue("la respuesta lleva credenciales");

        var data = JsonDocument.Parse(text).RootElement.GetProperty("data");
        var admin = data.GetProperty("initialAdministrator");
        return new Created(
            data.GetProperty("id").GetGuid(),
            admin.GetProperty("username").GetString()!,
            admin.GetProperty("temporaryPassword").GetString()!,
            text
        );
    }

    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(HttpStatusCode Status, bool RequiresReset, string? Token)> LoginAsync(
        string username,
        string password
    )
    {
        using var client = _f.App.CreateClient();
        var http = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
        var text = await http.Content.ReadAsStringAsync();
        if (http.StatusCode != HttpStatusCode.OK)
            return (http.StatusCode, false, null);
        var data = JsonDocument.Parse(text).RootElement.GetProperty("data");
        return (
            http.StatusCode,
            data.TryGetProperty("requiresPasswordReset", out var r) && r.GetBoolean(),
            data.TryGetProperty("token", out var t) ? t.GetString() : null
        );
    }

    [Fact]
    public async Task Creation_returns_the_temporary_credentials_once_and_first_login_forces_a_password_change()
    {
        var created = await CreateAsync(NewSuffix());

        created.Password.Should().HaveLength(16);

        // Primer ingreso con la contraseña temporal: sin sesión, solo token de cambio obligatorio.
        var login = await LoginAsync(created.Username, created.Password);
        login.Status.Should().Be(HttpStatusCode.OK);
        login.RequiresReset.Should().BeTrue();
        login.Token.Should().BeNullOrEmpty("no se emite JWT hasta cambiar la contraseña");

        // La contraseña no se persiste: solo existe su hash, y verifica.
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var user = await db.IdentityUsers.IgnoreQueryFilters().SingleAsync(u => u.UsernameNormalized == created.Username);
        user.PasswordHash.Should().NotContain(created.Password);
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>()
            .VerifyPassword(created.Password, user.PasswordHash).Should().BeTrue();
        user.RequirePasswordReset.Should().BeTrue();

        // Ninguna consulta posterior vuelve a mostrar la contraseña.
        using var client = _f.Client(global: true);
        var companies = await (await client.GetAsync("/api/v1/admin-core/companies")).Content.ReadAsStringAsync();
        var admins = await (
            await client.GetAsync($"/api/v1/admin-core/companies/{created.CompanyId}/administrators")
        ).Content.ReadAsStringAsync();
        companies.Should().NotContain(created.Password);
        admins.Should().Contain(created.Username).And.NotContain(created.Password).And.NotContain("passwordHash");
    }

    [Fact]
    public async Task First_login_flow_forces_password_change_then_grants_access_only_to_the_assigned_company_without_global_privileges()
    {
        var suffix = NewSuffix();
        var created = await CreateAsync(suffix);
        var other = await CreateAsync(NewSuffix()); // otra empresa del mismo tenant: no debe ser accesible
        const string newPassword = "NuevaClave2026";

        // 1) Inicio de sesión con la contraseña temporal: sin sesión, solo token de cambio.
        using var anonymous = _f.App.CreateClient();
        var firstLogin = await anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new { username = created.Username, password = created.Password });
        firstLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginData = JsonDocument.Parse(await firstLogin.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        loginData.GetProperty("requiresPasswordReset").GetBoolean().Should().BeTrue();
        loginData.GetProperty("token").GetString().Should().BeNullOrEmpty();
        var resetToken = loginData.GetProperty("passwordResetToken").GetString();
        resetToken.Should().NotBeNullOrEmpty();

        // 2) Una contraseña débil no completa el cambio.
        var weak = await anonymous.PostAsJsonAsync("/api/v1/auth/complete-password-reset",
            new { passwordResetToken = resetToken, newPassword = "corta" });
        weak.StatusCode.Should().NotBe(HttpStatusCode.OK);

        // 3) Cambio correcto: devuelve una sesión completa.
        var complete = await anonymous.PostAsJsonAsync("/api/v1/auth/complete-password-reset",
            new { passwordResetToken = resetToken, newPassword });
        var completeText = await complete.Content.ReadAsStringAsync();
        complete.StatusCode.Should().Be(HttpStatusCode.OK, completeText);
        var session = JsonDocument.Parse(completeText).RootElement.GetProperty("data");
        session.GetProperty("requiresPasswordReset").GetBoolean().Should().BeFalse();
        session.GetProperty("token").GetString().Should().NotBeNullOrEmpty();

        // 4) La temporal queda invalidada y la nueva funciona, ya sin pedir cambio.
        (await LoginAsync(created.Username, created.Password)).Status.Should().NotBe(HttpStatusCode.OK);
        var relogin = await LoginAsync(created.Username, newPassword);
        relogin.Status.Should().Be(HttpStatusCode.OK);
        relogin.RequiresReset.Should().BeFalse();
        relogin.Token.Should().NotBeNullOrEmpty();

        // 5) Identidad del nuevo administrador: no es global y pertenece solo a su empresa.
        Guid adminId;
        Guid mainBranchOfCompany;
        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            adminId = await db.IdentityUsers.IgnoreQueryFilters()
                .Where(u => u.UsernameNormalized == created.Username).Select(u => u.Id).SingleAsync();
            (await db.GlobalUserRoles.IgnoreQueryFilters().AnyAsync(r => r.UserId == adminId)).Should().BeFalse();
            (await db.CompanyUserMemberships.IgnoreQueryFilters().Where(m => m.IdentityUserId == adminId)
                .Select(m => m.CompanyId).ToListAsync()).Should().Equal(created.CompanyId);
            mainBranchOfCompany = await db.Branches.IgnoreQueryFilters()
                .Where(b => b.CompanyId == created.CompanyId && b.IsMainBranch).Select(b => b.Id).SingleAsync();
        }
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(relogin.Token!);
        jwt.Claims.Single(c => c.Type == "tenant_id").Value.Should().Be(_f.TenantId.ToString(),
            "el token es de tenant, no de plataforma (tenant vacío)");

        // 6) Acceso a SU empresa y sucursal; ninguno a la otra empresa ni a la consola global.
        using var admin = _f.ClientWithRealToken(relogin.Token!, _f.TenantId, created.CompanyId, adminId, "Admin");

        var context = await admin.GetAsync("/api/v1/session/context");
        var contextText = await context.Content.ReadAsStringAsync();
        context.StatusCode.Should().Be(HttpStatusCode.OK, contextText);
        var ctxData = JsonDocument.Parse(contextText).RootElement.GetProperty("data");
        ctxData.GetProperty("identity").GetProperty("username").GetString().Should().Be(created.Username);
        ctxData.GetProperty("authorization").GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString()).Should().Contain("*", "es Admin de su empresa por membresía");

        var branches = await admin.GetAsync("/api/v1/session/available-branches");
        var branchesText = await branches.Content.ReadAsStringAsync();
        branches.StatusCode.Should().Be(HttpStatusCode.OK, branchesText);
        var list = JsonDocument.Parse(branchesText).RootElement
            .GetProperty("data").GetProperty("branches").EnumerateArray().ToList();
        list.Should().ContainSingle();
        list[0].GetProperty("isMainBranch").GetBoolean().Should().BeTrue();
        list[0].GetProperty("id").GetGuid().Should().Be(mainBranchOfCompany);

        (await admin.GetAsync($"/api/v1/companies/{other.CompanyId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "no es miembro de esa empresa");
        (await admin.GetAsync("/api/v1/admin-core/companies")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync("/api/v1/companies", Body($"x.{suffix}", $"x.{suffix}@example.test")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync(
                $"/api/v1/admin-core/companies/{created.CompanyId}/administrators/{adminId}/temporary-password", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Simultaneous_company_creations_in_the_same_tenant_both_return_201_without_500()
    {
        var a = NewSuffix();
        var b = NewSuffix();

        var results = await Task.WhenAll(
            PostCompanyAsync(Body($"par.a.{a}", $"par.a.{a}@example.test")),
            PostCompanyAsync(Body($"par.b.{b}", $"par.b.{b}@example.test")));

        results.Select(r => r.Http.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.Created,
            string.Join(" | ", results.Select(r => r.Text)));
        var ids = results.Select(r => JsonDocument.Parse(r.Text).RootElement.GetProperty("data").GetProperty("id").GetGuid()).ToList();
        ids.Distinct().Should().HaveCount(2);

        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        foreach (var id in ids)
        {
            (await db.Branches.IgnoreQueryFilters().AnyAsync(x => x.CompanyId == id && x.IsMainBranch)).Should().BeTrue();
            (await db.Warehouses.IgnoreQueryFilters().AnyAsync(x => x.CompanyId == id)).Should().BeTrue();
            (await db.AccessProfiles.IgnoreQueryFilters().CountAsync(x => x.CompanyId == id)).Should().Be(2);
            (await db.CompanyUserMemberships.IgnoreQueryFilters().CountAsync(x => x.CompanyId == id)).Should().Be(2);
        }
        var banks = await db.Banks.IgnoreQueryFilters().Where(x => x.TenantId == _f.TenantId).Select(x => x.Code).ToListAsync();
        banks.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Duplicate_username_or_email_returns_a_generic_409_and_creates_no_company()
    {
        var suffix = NewSuffix();
        await CreateAsync(suffix);
        int CountCompanies()
        {
            using var s = _f.CreateScope();
            return s.ServiceProvider.GetRequiredService<ErpDbContext>().Companies.IgnoreQueryFilters().Count();
        }
        var before = CountCompanies();

        var byUsername = await PostCompanyAsync(Body($"ADM.{suffix}", "otro@example.test"));
        var byEmail = await PostCompanyAsync(Body("otro.usuario", $"ADM.{suffix}@Example.test"));

        byUsername.Http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        byEmail.Http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        // Misma respuesta en ambos casos: no revela cuál de los dos existe.
        string UserMessage(string json) =>
            JsonDocument.Parse(json).RootElement.GetProperty("message").GetProperty("user").GetString()!;
        UserMessage(byUsername.Text).Should().Be(UserMessage(byEmail.Text));
        byUsername.Text.Should().NotContain("adm.").And.NotContain("@example.test");
        CountCompanies().Should().Be(before);
    }

    [Fact]
    public async Task Company_creation_without_administrator_keeps_the_integration_contract()
    {
        using var client = _f.Client(global: true);
        var http = await client.PostAsJsonAsync("/api/v1/companies", new
        {
            tenantId = _f.TenantId,
            taxId = CompanyIdentityUpdateFixture.ValidRuc(),
            legalName = "Sin administrador",
            tradeName = (string?)null,
        });

        http.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = JsonDocument.Parse(await http.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        data.GetProperty("legalName").GetString().Should().Be("Sin administrador");
        data.TryGetProperty("initialAdministrator", out var admin).Should().BeTrue();
        admin.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Regeneration_invalidates_the_previous_password_and_shows_the_new_one_once()
    {
        var created = await CreateAsync(NewSuffix());
        using var client = _f.Client(global: true);

        var listJson = await client.GetStringAsync($"/api/v1/admin-core/companies/{created.CompanyId}/administrators");
        var adminId = JsonDocument.Parse(listJson).RootElement.GetProperty("data")
            .EnumerateArray().Single().GetProperty("identityUserId").GetGuid();

        var http = await client.PostAsync(
            $"/api/v1/admin-core/companies/{created.CompanyId}/administrators/{adminId}/temporary-password", null);
        var text = await http.Content.ReadAsStringAsync();
        http.StatusCode.Should().Be(HttpStatusCode.OK, text);
        http.Headers.CacheControl!.NoStore.Should().BeTrue();
        var next = JsonDocument.Parse(text).RootElement.GetProperty("data").GetProperty("temporaryPassword").GetString()!;
        next.Should().NotBe(created.Password);

        (await LoginAsync(created.Username, created.Password)).Status.Should().NotBe(HttpStatusCode.OK, "la anterior quedó invalidada");
        var login = await LoginAsync(created.Username, next);
        login.Status.Should().Be(HttpStatusCode.OK);
        login.RequiresReset.Should().BeTrue("debe cambiarla en el próximo ingreso");

        // La nueva contraseña no vuelve a consultarse.
        (await client.GetStringAsync($"/api/v1/admin-core/companies/{created.CompanyId}/administrators"))
            .Should().NotContain(next);
    }

    [Fact]
    public async Task Only_the_global_administrator_can_list_or_regenerate()
    {
        var created = await CreateAsync(NewSuffix());
        using var global = _f.Client(global: true);
        var adminId = JsonDocument.Parse(
                await global.GetStringAsync($"/api/v1/admin-core/companies/{created.CompanyId}/administrators"))
            .RootElement.GetProperty("data").EnumerateArray().Single().GetProperty("identityUserId").GetGuid();

        using var tenantUser = _f.Client(global: false);
        (await tenantUser.GetAsync($"/api/v1/admin-core/companies/{created.CompanyId}/administrators"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tenantUser.PostAsync(
                $"/api/v1/admin-core/companies/{created.CompanyId}/administrators/{adminId}/temporary-password", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Un usuario inexistente o de otra empresa responde igual: 404 uniforme.
        var otherCompany = await CreateAsync(NewSuffix());
        (await global.PostAsync(
                $"/api/v1/admin-core/companies/{created.CompanyId}/administrators/{Guid.NewGuid()}/temporary-password", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await global.PostAsync(
                $"/api/v1/admin-core/companies/{otherCompany.CompanyId}/administrators/{adminId}/temporary-password", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Y la contraseña original sigue vigente tras los intentos rechazados.
        (await LoginAsync(created.Username, created.Password)).RequiresReset.Should().BeTrue();
    }
}
