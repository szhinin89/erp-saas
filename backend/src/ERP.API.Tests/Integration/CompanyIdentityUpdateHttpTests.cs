using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Application.Access.Authorization;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-COMPANY-IDENTITY-SSOT-01 — la identidad de empresa (RUC, razón social, nombre comercial,
/// activo) se edita desde dos contextos de autorización legítimos:
///   - operativo: <c>PUT /api/v1/companies/{id}</c> — usuario del tenant con permiso
///     settings.companies.update y membership en la empresa (scope server-side del tenant);
///   - global: <c>PUT /api/v1/admin-core/companies/{id}</c> — token global (tenant vacío + Admin,
///     policy PlatformAdmin) sobre la empresa explícitamente indicada de cualquier tenant.
/// Ambos llegan a la misma regla (validación, unicidad del RUC, normalización, auditoría): mismo
/// status y mismo código ante el mismo dato. HTTP real + PostgreSQL real.
/// </summary>
public sealed class CompanyIdentityUpdateFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _factory = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public WebApplicationFactory<Program> App { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid ForeignTenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GlobalAdminId { get; } = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var tenant = Tenant.Create("ZH-Identity", $"zh-id-{Guid.NewGuid():N}", _adminId);
            var foreign = Tenant.Create("ZH-Identity-Otro", $"zh-io-{Guid.NewGuid():N}", _adminId);
            db.Tenants.AddRange(tenant, foreign);
            await db.SaveChangesAsync();
            TenantId = tenant.Id;
            ForeignTenantId = foreign.Id;

            var user = IdentityUser.Create(
                $"idn-{Guid.NewGuid():N}"[..12],
                "Admin",
                "Empresa",
                $"idn-{Guid.NewGuid():N}@test.com",
                "TEST_PASSWORD_HASH",
                _adminId
            );
            db.IdentityUsers.Add(user);
            await db.SaveChangesAsync();
            UserId = user.Id;
        }

        App = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>()
            )
        );
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
        await _factory.DisposeAsync();
    }

    public IServiceScope CreateScope() => _factory.Services.CreateScope();

    /// <summary>Empresa nueva del tenant indicado; con membership del usuario operativo si es del tenant propio.</summary>
    public async Task<Guid> CreateCompanyAsync(Guid tenantId, string? ruc = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var company = Company.CreateManaged(
            tenantId,
            ruc ?? ValidRuc(),
            "Empresa Original",
            tradeName: "Original",
            createdBy: _adminId
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        if (tenantId == TenantId)
        {
            db.CompanyUserMemberships.Add(
                CompanyUserMembership.Create(company.Id, UserId, "Admin", null, _adminId)
            );
            await db.SaveChangesAsync();
        }
        return company.Id;
    }

    public async Task<Company> ReadAsync(Guid companyId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db
            .Companies.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(c => c.Id == companyId);
    }

    /// <summary>Cliente de un contexto: ICurrentTenant/ICurrentUser del host de test siguen al token.</summary>
    public HttpClient Client(bool global)
    {
        _factory.MutableTenant.TenantId = global ? Guid.Empty : TenantId;
        _factory.MutableCompany.CompanyId = Guid.Empty;
        _factory.MutableUser.UserId = global ? GlobalAdminId : UserId;
        _factory.MutableUser.Role = "Admin";
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            global
                ? TestJwtFactory.CreateSessionJwt(Guid.Empty, GlobalAdminId, role: "Admin")
                : TestJwtFactory.CreateSessionJwt(TenantId, UserId, role: "Admin")
        );
        return client;
    }

    private static int _sequence = Random.Shared.Next(1_000, 9_000);

    /// <summary>RUC válido de persona natural (provincia 17, módulo 10) y único por llamada.</summary>
    public static string ValidRuc()
    {
        var body = $"170{Interlocked.Increment(ref _sequence):D6}"; // 9 dígitos, tercer dígito 0
        int[] coef = [2, 1, 2, 1, 2, 1, 2, 1, 2];
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var p = (body[i] - '0') * coef[i];
            sum += p >= 10 ? p - 9 : p;
        }
        return $"{body}{(10 - sum % 10) % 10}001";
    }
}

[Trait("Category", "PostgreSql")]
public sealed class CompanyIdentityUpdateHttpTests : IClassFixture<CompanyIdentityUpdateFixture>
{
    private readonly CompanyIdentityUpdateFixture _f;

    public CompanyIdentityUpdateHttpTests(CompanyIdentityUpdateFixture f) => _f = f;

    private sealed record Response(HttpStatusCode Status, string? Code);

    private static string Url(bool global, Guid id) =>
        global ? $"/api/v1/admin-core/companies/{id}" : $"/api/v1/companies/{id}";

    /// <summary>PUT al endpoint del contexto <paramref name="endpointGlobal"/> con el token del contexto <paramref name="tokenGlobal"/>.</summary>
    private async Task<Response> SendAsync(
        bool endpointGlobal,
        bool tokenGlobal,
        Guid id,
        string legalName,
        string? tradeName,
        bool isActive,
        string? taxId
    )
    {
        using var client = _f.Client(tokenGlobal);
        var http = await client.PutAsJsonAsync(
            Url(endpointGlobal, id),
            new
            {
                id,
                legalName,
                tradeName,
                isActive,
                taxId,
            }
        );
        var text = await http.Content.ReadAsStringAsync();
        string? code = null;
        if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith('{'))
            code = JsonDocument.Parse(text).RootElement.TryGetProperty("code", out var c)
                ? c.GetString()
                : null;
        return new Response(http.StatusCode, code);
    }

    /// <summary>Cada contexto por su propio endpoint.</summary>
    private Task<Response> Put(
        bool global,
        Guid id,
        string legalName,
        string? tradeName = null,
        bool isActive = true,
        string? taxId = null
    ) => SendAsync(global, global, id, legalName, tradeName, isActive, taxId);

    private Task<Response> PutWithTokenAsync(bool endpointGlobal, bool tokenGlobal, Guid id) =>
        SendAsync(endpointGlobal, tokenGlobal, id, "Intento", null, true, null);

    private Task<Guid> OwnCompanyAsync(string? ruc = null) =>
        _f.CreateCompanyAsync(_f.TenantId, ruc);

    // ── Misma regla desde ambos contextos ───────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actualizacion_valida_normaliza_y_audita_igual(bool global)
    {
        var id = await OwnCompanyAsync();
        var ruc = CompanyIdentityUpdateFixture.ValidRuc();

        var response = await Put(
            global,
            id,
            "  Nueva Razón  ",
            "  Comercial  ",
            isActive: false,
            taxId: $" {ruc} "
        );

        response.Status.Should().Be(HttpStatusCode.OK, response.Code);
        var company = await _f.ReadAsync(id);
        company.LegalName.Should().Be("Nueva Razón");
        company.TradeName.Should().Be("Comercial");
        company.IsActive.Should().BeFalse();
        company.TaxIdentificationNumber.Should().Be(ruc);
        company.IsTemporaryTaxIdentification.Should().BeFalse();
        company.TaxIdentificationStatus.Should().Be(TaxIdentificationStatus.Verified);
        company.UpdatedBy.Should().Be(global ? _f.GlobalAdminId : _f.UserId);
    }

    [Theory]
    [InlineData(false, "1234567890123")]
    [InlineData(true, "1234567890123")]
    [InlineData(false, "17ABC")]
    [InlineData(true, "17ABC")]
    public async Task RUC_invalido_misma_semantica_422_VALIDATION_ERROR_sin_cambios(
        bool global,
        string ruc
    )
    {
        var id = await OwnCompanyAsync();
        var before = await _f.ReadAsync(id);

        var response = await Put(global, id, "Otra", taxId: ruc);

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Code.Should().Be("VALIDATION_ERROR");
        (await _f.ReadAsync(id)).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Razon_social_vacia_misma_semantica_422_VALIDATION_ERROR(bool global)
    {
        var id = await OwnCompanyAsync();

        var response = await Put(global, id, "   ");

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Code.Should().Be("VALIDATION_ERROR");
        (await _f.ReadAsync(id)).LegalName.Should().Be("Empresa Original");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RUC_duplicado_de_otro_tenant_mismo_codigo_409(bool global)
    {
        var takenRuc = CompanyIdentityUpdateFixture.ValidRuc();
        await _f.CreateCompanyAsync(_f.ForeignTenantId, takenRuc);
        var id = await OwnCompanyAsync();

        var response = await Put(global, id, "Otra", taxId: takenRuc);

        response.Status.Should().Be(HttpStatusCode.Conflict);
        response.Code.Should().Be("COMPANY_RUC_ALREADY_EXISTS");
        (await _f.ReadAsync(id)).TaxIdentificationNumber.Should().NotBe(takenRuc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empresa_inexistente_misma_semantica_404_NOT_FOUND(bool global)
    {
        var response = await Put(global, Guid.NewGuid(), "Otra");

        response.Status.Should().Be(HttpStatusCode.NotFound);
        response.Code.Should().Be("NOT_FOUND");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RUC_vacio_conserva_la_identificacion_fiscal(bool global)
    {
        var ruc = CompanyIdentityUpdateFixture.ValidRuc();
        var id = await OwnCompanyAsync(ruc);

        (await Put(global, id, "Solo nombres", taxId: null)).Status.Should().Be(HttpStatusCode.OK);

        var company = await _f.ReadAsync(id);
        company.TaxIdentificationNumber.Should().Be(ruc);
        company.LegalName.Should().Be("Solo nombres");
    }

    // ── Contexto / alcance ──────────────────────────────────────────────

    [Fact]
    public async Task Operativo_sobre_empresa_de_otro_tenant_es_404_igual_que_inexistente()
    {
        var foreign = await _f.CreateCompanyAsync(_f.ForeignTenantId);
        var before = await _f.ReadAsync(foreign);

        var response = await Put(global: false, foreign, "Intrusa");

        response.Status.Should().Be(HttpStatusCode.NotFound);
        response.Code.Should().Be("NOT_FOUND");
        (await _f.ReadAsync(foreign)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Global_opera_la_empresa_explicita_de_cualquier_tenant()
    {
        var foreign = await _f.CreateCompanyAsync(_f.ForeignTenantId);

        (await Put(global: true, foreign, "Editada por plataforma"))
            .Status.Should()
            .Be(HttpStatusCode.OK);

        (await _f.ReadAsync(foreign)).LegalName.Should().Be("Editada por plataforma");
    }

    [Fact]
    public async Task Token_de_tenant_no_puede_usar_el_endpoint_global()
    {
        var id = await OwnCompanyAsync();

        var response = await PutWithTokenAsync(endpointGlobal: true, tokenGlobal: false, id);

        response.Status.Should().Be(HttpStatusCode.Forbidden);
        (await _f.ReadAsync(id)).LegalName.Should().Be("Empresa Original");
    }

    [Fact]
    public async Task Token_global_no_opera_por_el_endpoint_de_tenant()
    {
        var id = await OwnCompanyAsync();

        var response = await PutWithTokenAsync(endpointGlobal: false, tokenGlobal: true, id);

        ((int)response.Status).Should().BeGreaterThanOrEqualTo(400).And.BeLessThan(500);
        (await _f.ReadAsync(id)).LegalName.Should().Be("Empresa Original");
    }
}
