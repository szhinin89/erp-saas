using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Application.Access.Authorization;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-API-ERROR-CONTRACT-HARDENING-01 — contrato único Error → HTTP por HTTP real (pipeline,
/// behaviors, guards, handlers y PostgreSQL reales) en los endpoints afectados:
///   - recepción de compras: SRI_COMMUNICATION_ERROR → 502 (antes 400) solo por el SSOT, sin tocar
///     handler ni flujo; documento inexistente y de otra empresa → mismo 404 (sin existence leakage);
///   - retenciones XML/PDF: NOT_FOUND → 404 (antes 400 por ApiBadRequest manual);
///   - rate limiter: 429 con envelope RATE_LIMITED (antes 429 sin cuerpo);
///   - sucursal inexistente y sucursal de otra empresa → respuesta idéntica.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ApiErrorContractHttpTests : IAsyncLifetime
{
    private const string AccessKey = "0107202601179135268800120150270001617400016174011";

    private readonly PostgreSqlTestWebAppFactory _factory = new(useHttpCompanyContext: true);
    private readonly Guid _adminId = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _foreignBranchId;
    private Guid _documentId;
    private Guid _foreignDocumentId;
    private Guid _userId;
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;

    private sealed class SriUnavailableXmlProvider : ISriReceptionXmlProvider
    {
        public Task<SriReceptionXmlQueryResult> GetAuthorizedXmlAsync(
            Guid tenantId,
            Guid companyId,
            string accessKey,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                new SriReceptionXmlQueryResult(false, null, null, null, "SRI no disponible")
            );
    }

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
            var tenant = Tenant.Create("ZH-ErrorContract", $"zh-ec-{Guid.NewGuid():N}", _adminId);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            _tenantId = tenant.Id;

            var company = Company.CreateManaged(_tenantId, $"179{Guid.NewGuid():N}"[..13], "Empresa A", createdBy: _adminId);
            var foreignCompany = Company.CreateManaged(_tenantId, $"179{Guid.NewGuid():N}"[..13], "Empresa B", createdBy: _adminId);
            db.Companies.AddRange(company, foreignCompany);
            await db.SaveChangesAsync();
            _companyId = company.Id;

            var branch = NewBranch(company.Id, "Matriz A", "SUC-A");
            var foreignBranch = NewBranch(foreignCompany.Id, "Matriz B", "SUC-B");
            db.Branches.AddRange(branch, foreignBranch);
            await db.SaveChangesAsync();
            _branchId = branch.Id;
            _foreignBranchId = foreignBranch.Id;

            var user = IdentityUser.Create($"ec-{Guid.NewGuid():N}", "Usuario", "Prueba", $"ec-{Guid.NewGuid():N}@test.com", "hash", _adminId);
            db.IdentityUsers.Add(user);
            await db.SaveChangesAsync();
            _userId = user.Id;

            var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, _adminId);
            db.CompanyUserMemberships.Add(membership);
            await db.SaveChangesAsync();
            db.CompanyUserBranches.Add(CompanyUserBranch.Create(_tenantId, company.Id, membership.Id, branch.Id, _adminId));

            var document = NewReceptionDocument(company.Id, branch.Id, AccessKey);
            var foreignDocument = NewReceptionDocument(foreignCompany.Id, foreignBranch.Id, AccessKey[..^1] + "2");
            db.PurchaseReceptionDocuments.AddRange(document, foreignDocument);
            await db.SaveChangesAsync();
            _documentId = document.Id;
            _foreignDocumentId = foreignDocument.Id;
        }

        _app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>();
            services.AddScoped<ISriReceptionXmlProvider, SriUnavailableXmlProvider>();
        }));
        _client = _app.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(_tenantId, _userId)
        );
        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableUser.UserId = _userId;
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _factory.DisposeAsync();
    }

    // ── Recepción de compras (Purchases CLOSED: solo cambia el mapping transversal) ─────────

    [Fact]
    public async Task Download_xml_con_SRI_no_disponible_responde_502_SRI_COMMUNICATION_ERROR_y_no_toca_el_documento()
    {
        var (status, code, errors) = await SendAsync(HttpMethod.Post, $"/api/v1/purchases/reception/{_documentId}/download-xml", _branchId);

        status.Should().Be(HttpStatusCode.BadGateway);
        code.Should().Be("SRI_COMMUNICATION_ERROR");
        errors.Should().ContainSingle().Which.Should().Contain("SRI");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var document = await db.PurchaseReceptionDocuments.IgnoreQueryFilters().SingleAsync(d => d.Id == _documentId);
        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Imported);
    }

    [Fact]
    public async Task Download_xml_de_documento_inexistente_y_de_otra_empresa_son_el_mismo_404()
    {
        var nonexistent = await SendAsync(HttpMethod.Post, $"/api/v1/purchases/reception/{Guid.NewGuid()}/download-xml", _branchId);
        var foreign = await SendAsync(HttpMethod.Post, $"/api/v1/purchases/reception/{_foreignDocumentId}/download-xml", _branchId);

        nonexistent.Status.Should().Be(HttpStatusCode.NotFound);
        nonexistent.Code.Should().Be("NOT_FOUND");
        foreign.Should().BeEquivalentTo(nonexistent);
    }

    // ── Retenciones XML/PDF (antes ApiBadRequest manual: todo fallo salía 400) ──────────────

    [Theory]
    [InlineData("electronic/xml")]
    [InlineData("ride/pdf")]
    public async Task Retencion_inexistente_responde_404_NOT_FOUND(string suffix)
    {
        var (status, code, errors) = await SendAsync(HttpMethod.Get, $"/api/v1/retentions/{Guid.NewGuid()}/{suffix}", branchId: null);

        status.Should().Be(HttpStatusCode.NotFound);
        code.Should().Be("NOT_FOUND");
        errors.Should().ContainSingle();
    }

    // ── Sucursal: inexistente y ajena son indistinguibles ──────────────────────────────────

    [Fact]
    public async Task Sucursal_inexistente_y_de_otra_empresa_producen_la_misma_respuesta()
    {
        var nonexistent = await SendAsync(HttpMethod.Get, "/api/v1/cash-registers", Guid.NewGuid());
        var foreign = await SendAsync(HttpMethod.Get, "/api/v1/cash-registers", _foreignBranchId);

        nonexistent.Status.Should().Be(HttpStatusCode.Forbidden);
        nonexistent.Code.Should().Be("BRANCH_SCOPE_FORBIDDEN");
        foreign.Should().BeEquivalentTo(nonexistent);
    }

    // ── Rate limiter ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rate_limiter_rechaza_con_429_y_envelope_RATE_LIMITED()
    {
        using var anonymous = _app.CreateClient();
        (HttpStatusCode Status, string? Code, string[] Errors) last = default;

        // auth-refresh-ip: 60/min por IP por defecto; sin refresh token cada llamada es 401 hasta agotar la ventana.
        for (var i = 0; i < 200 && last.Status != HttpStatusCode.TooManyRequests; i++)
        {
            using var response = await anonymous.PostAsync("/api/v1/auth/refresh", JsonContent("{}"));
            last = await ReadAsync(response);
            if (last.Status != HttpStatusCode.TooManyRequests)
                last.Code.Should().Be("UNAUTHORIZED");
        }

        last.Status.Should().Be(HttpStatusCode.TooManyRequests);
        last.Code.Should().Be("RATE_LIMITED");
    }

    private async Task<(HttpStatusCode Status, string? Code, string[] Errors)> SendAsync(HttpMethod method, string url, Guid? branchId)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Company-Id", _companyId.ToString());
        if (branchId is Guid b)
            request.Headers.Add("X-Branch-Id", b.ToString());
        if (method == HttpMethod.Post)
            request.Content = JsonContent("{}");

        using var response = await _client.SendAsync(request);
        return await ReadAsync(response);
    }

    private static StringContent JsonContent(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");

    private static async Task<(HttpStatusCode Status, string? Code, string[] Errors)> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{'))
            return (response.StatusCode, null, []);
        var json = JsonDocument.Parse(text).RootElement;
        var errors = json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().Select(x => x.GetString()!).ToArray()
            : [];
        return (response.StatusCode, json.TryGetProperty("code", out var c) ? c.GetString() : null, errors);
    }

    private PurchaseReceptionDocument NewReceptionDocument(Guid companyId, Guid branchId, string accessKey) =>
        PurchaseReceptionDocument.Create(
            _tenantId,
            companyId,
            branchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1791352688001",
            "QUALA ECUADOR S A",
            supplierId: null,
            accessKey,
            "015-027-000161740",
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 21, 6, 55, DateTimeKind.Utc),
            15.96m,
            2.4m,
            18.35m,
            _adminId
        );

    private Branch NewBranch(Guid companyId, string name, string code) =>
        Branch.Create(
            _tenantId,
            name,
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
            _adminId,
            companyId: companyId
        );
}
