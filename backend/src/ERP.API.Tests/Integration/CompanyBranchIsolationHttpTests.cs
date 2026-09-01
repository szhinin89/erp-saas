using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

namespace ERP.API.Tests.Integration;

/// <summary>
/// FASE 4C (ZH-AUTH-HTTP-COMPANY-BRANCH-ISOLATION-04C) — prueba de integración HTTP real, contra
/// PostgreSQL (Testcontainers) y el <see cref="Program"/> completo, de lo que FASE 4B ya probó a
/// nivel unitario con mocks: que un request con X-Company-Id (Empresa A, con membership real) y
/// X-Branch-Id de una sucursal de Empresa B nunca pasa por BranchScopeBehavior/IBranchAccessGuard.
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
/// Nota de diseño (regla 4 de la fase): igual que CajaVentasFlowFixture, este factory reemplaza
/// ICurrentTenant/ICurrentCompany/ICurrentUser por dobles mutables fijados directamente en el
/// fixture — la empresa operativa NO se resuelve leyendo el header X-Company-Id real (eso ya lo
/// cubre CurrentCompanyService, una clase trivial de una línea, sin lógica de negocio que probar).
/// Lo que sí viaja como header HTTP real, exactamente como lo hace el frontend, es X-Branch-Id —
/// que es también el único de los dos por el que puede colarse una sucursal cruzada, porque
/// CompanyScopeBehavior ya fija la empresa operativa antes de que BranchScopeBehavior/
/// IBranchAccessGuard evalúen la sucursal. Construir un factory que además parsee X-Company-Id
/// desde el header habría requerido reimplementar el middleware de autenticación de pruebas ya
/// existente — fuera de alcance para esta fase (regla 5: no convertir esto en refactor de auth).
/// </summary>
public sealed class CompanyBranchIsolationHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new();
    private Guid _adminId;

    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid BranchAId { get; private set; }
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
        _baseFactory.MutableCompany.CompanyId = CompanyAId;
        _baseFactory.MutableUser.UserId = UserId;
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

        var branchA = NewBranch(TenantId, companyA.Id, "Matriz A", "SUC-A", _adminId);
        var branchB = NewBranch(TenantId, companyB.Id, "Matriz B", "SUC-B", _adminId);
        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();
        BranchAId = branchA.Id;
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
        Guid createdBy
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
            isMainBranch: true,
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
    public async Task Request_con_X_Company_Id_Empresa_A_y_X_Branch_Id_de_Empresa_B_es_rechazado_403()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Add("X-Branch-Id", _f.BranchBId.ToString());

        var response = await _f.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should()
            .NotContain(
                _f.BranchBId.ToString(),
                "el body de un 403 nunca debe filtrar información de la sucursal cruzada"
            );
    }

    [Fact]
    public async Task Request_con_X_Company_Id_y_X_Branch_Id_ambos_de_Empresa_A_es_aceptado_200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Add("X-Branch-Id", _f.BranchAId.ToString());

        var response = await _f.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
