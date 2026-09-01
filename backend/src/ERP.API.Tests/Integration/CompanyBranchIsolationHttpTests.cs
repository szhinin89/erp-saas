using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

namespace ERP.API.Tests.Integration;

/// <summary>
/// FASE 4C (ZH-AUTH-HTTP-COMPANY-BRANCH-ISOLATION-04C) — prueba de integración HTTP real, contra
/// PostgreSQL (Testcontainers) y el <see cref="Program"/> completo, de lo que FASE 4B ya probó a
/// nivel unitario con mocks: que los headers X-Company-Id y X-Branch-Id pasan por middleware,
/// controller, autorización, MediatR, CompanyScopeBehavior, BranchScopeBehavior y guards reales.
/// Sigue el mismo patrón que <see cref="CajaVentasFlowFixture"/> (PostgreSqlTestWebAppFactory +
/// TestJwtFactory) — no se introduce infraestructura de test nueva.
///
/// Endpoint elegido: GET /api/v1/cash-registers (CashRegisterController.GetByCurrentBranch), que
/// resuelve <see cref="ERP.Application.Modules.Caja.UseCases.GetCashRegistersByCurrentBranchQuery"/>
/// (IBranchScopedRequest) — es el endpoint IBranchScopedRequest de menor setup: GET sin body, sin
/// datos de dominio propios que sembrar (una sucursal sin cajas registradas devuelve 200 con lista
/// vacía), y su policy <c>perm:{CajaPermissions.View}</c> la satisface el bypass de rol Admin en
/// RuntimePermissionAuthorizer sin necesitar perfiles/permisos granulares.
///
/// Nota de diseño: el factory usa la opción opt-in useHttpCompanyContext para conservar
/// CurrentCompanyService real en esta suite; así X-Company-Id se resuelve desde HttpContext.
/// </summary>
public sealed class CompanyBranchIsolationHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid BranchAId { get; private set; }
    public Guid BranchAWithoutAccessId { get; private set; }
    public Guid BranchBId { get; private set; }
    public Guid UserId { get; private set; }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        Client = _baseFactory.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, UserId)
        );

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableUser.UserId = UserId;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create(
            "ZH-CompanyBranchIsolation-Test",
            $"zh-cbi-{Guid.NewGuid():N}",
            _adminId
        );
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var companyA = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa A S.A.",
            createdBy: _adminId
        );
        var companyB = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa B S.A.",
            createdBy: _adminId
        );
        db.Companies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();
        CompanyAId = companyA.Id;
        CompanyBId = companyB.Id;

        var branchA = NewBranch(TenantId, companyA.Id, "Matriz A", "SUC-A", _adminId);
        var branchAWithoutAccess = NewBranch(
            TenantId,
            companyA.Id,
            "Sucursal A Sin Acceso",
            "SUC-A2",
            _adminId,
            isMainBranch: false
        );
        var branchB = NewBranch(TenantId, companyB.Id, "Matriz B", "SUC-B", _adminId);
        db.Branches.AddRange(branchA, branchAWithoutAccess, branchB);
        await db.SaveChangesAsync();
        BranchAId = branchA.Id;
        BranchAWithoutAccessId = branchAWithoutAccess.Id;
        BranchBId = branchB.Id;

        var user = IdentityUser.Create(
            $"usuario-{Guid.NewGuid():N}",
            "Usuario",
            "Prueba",
            $"usuario-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        UserId = user.Id;

        // Membership solo en Empresa A — el usuario nunca tuvo acceso a Empresa B.
        var membership = CompanyUserMembership.Create(companyA.Id, user.Id, "Admin", null, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();

        // Autorizado únicamente para BranchA (CompanyUserBranch) — BranchB nunca se autoriza,
        // así que si el guard llegara a evaluarla (no debería) tampoco pasaría por ese motivo.
        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, companyA.Id, membership.Id, branchA.Id, _adminId)
        );
        await db.SaveChangesAsync();
    }

    private static Branch NewBranch(
        Guid tenantId,
        Guid companyId,
        string name,
        string code,
        Guid createdBy,
        bool isMainBranch = true
    ) =>
        Branch.Create(
            tenantId,
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
            isMainBranch: isMainBranch,
            createdBy,
            companyId: companyId
        );
}

[Trait("Category", "PostgreSql")]
public sealed class CompanyBranchIsolationHttpTests : IClassFixture<CompanyBranchIsolationHttpFixture>
{
    private const string Endpoint = "/api/v1/cash-registers";
    private readonly CompanyBranchIsolationHttpFixture _f;

    public CompanyBranchIsolationHttpTests(CompanyBranchIsolationHttpFixture fixture) => _f = fixture;

    [Fact]
    public async Task Request_con_X_Company_Id_y_X_Branch_Id_ambos_de_Empresa_A_es_aceptado_200()
    {
        using var response = await SendAsync(_f.CompanyAId, _f.BranchAId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Usuario_sin_acceso_a_Empresa_B_falla_403_y_no_cambia_contexto()
    {
        using var okBefore = await SendAsync(_f.CompanyAId, _f.BranchAId);
        okBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await SendAsync(_f.CompanyBId, _f.BranchBId);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should()
            .NotContain(
                _f.CompanyBId.ToString(),
                "el body de un 403 no debe filtrar el identificador de la empresa no autorizada"
            );

        using var okAfter = await SendAsync(_f.CompanyAId, _f.BranchAId);
        okAfter.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Branch_de_otra_company_con_Empresa_A_falla_403()
    {
        using var response = await SendAsync(_f.CompanyAId, _f.BranchBId);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should()
            .NotContain(
                _f.BranchBId.ToString(),
                "el body de un 403 no debe filtrar información de la sucursal cruzada"
            );
    }

    [Fact]
    public async Task Branch_de_la_misma_company_sin_CompanyUserBranch_falla_403()
    {
        using var response = await SendAsync(_f.CompanyAId, _f.BranchAWithoutAccessId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Header_company_invalido_falla_403_y_no_reutiliza_contexto_anterior()
    {
        using var okBefore = await SendAsync(_f.CompanyAId, _f.BranchAId);
        okBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await SendAsync("not-a-company-guid", _f.BranchAId.ToString());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var okAfter = await SendAsync(_f.CompanyAId, _f.BranchAId);
        okAfter.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Header_branch_invalido_falla_403_y_no_reutiliza_contexto_anterior()
    {
        using var okBefore = await SendAsync(_f.CompanyAId, _f.BranchAId);
        okBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await SendAsync(_f.CompanyAId.ToString(), "not-a-branch-guid");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var okAfter = await SendAsync(_f.CompanyAId, _f.BranchAId);
        okAfter.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> SendAsync(Guid companyId, Guid branchId) =>
        SendAsync(companyId.ToString(), branchId.ToString());

    private async Task<HttpResponseMessage> SendAsync(string companyId, string branchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Add("X-Company-Id", companyId);
        request.Headers.Add("X-Branch-Id", branchId);

        return await _f.Client.SendAsync(request);
    }
}
