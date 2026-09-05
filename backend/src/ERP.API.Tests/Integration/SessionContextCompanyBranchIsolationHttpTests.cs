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
/// FASE 4C (ZH-AUTH-HTTP-COMPANY-BRANCH-ISOLATION-04C) — complementa
/// <see cref="CompanyBranchIsolationHttpTests"/> con dos escenarios que ese fixture no puede cubrir
/// porque requiere un usuario con membresía en Empresa A pero SIN ninguna relación con Empresa B
/// (ni siquiera lectura):
///
///   1. GET /api/v1/session/context: la resolución de "empresa operativa" a partir del header
///      X-Company-Id (<see cref="ERP.Application.Access.CompanyContextProvider.ResolveOperationalCompanyIdAsync"/>)
///      confiaba en el header sin verificar CompanyUserMembership — un usuario podía leer
///      TradeName/LegalName/logo de una empresa ajena del mismo tenant solo enviando su Id. Bug
///      productivo encontrado durante esta fase; corregido en CompanyContextProvider (ver commit) y
///      cubierto aquí como regresión HTTP real, no solo unitaria.
///   2. Header de sucursal "viejo" tras un cambio de empresa: un usuario con acceso legítimo a dos
///      empresas (A y B) envía X-Company-Id=B con X-Branch-Id de una sucursal de A — el pipeline
///      debe rechazar aunque el usuario sí tenga autorización en ambas empresas por separado
///      (a diferencia de CompanyBranchIsolationHttpTests, donde Empresa B nunca está autorizada).
/// </summary>
public sealed class SessionContextCompanyBranchIsolationHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid BranchAId { get; private set; }
    public Guid BranchBId { get; private set; }
    public string CompanyBLegalName { get; } = $"Empresa B Confidencial {Guid.NewGuid():N}";
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
            "ZH-SessionCompanyIsolation-Test",
            $"zh-sci-{Guid.NewGuid():N}",
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
            legalName: CompanyBLegalName,
            createdBy: _adminId
        );
        db.Companies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();
        CompanyAId = companyA.Id;
        CompanyBId = companyB.Id;

        var branchA = NewBranch(TenantId, companyA.Id, "Matriz A", "SUC-A");
        var branchB = NewBranch(TenantId, companyB.Id, "Matriz B", "SUC-B");
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

        // Membresía y branch únicamente en Empresa A. Empresa B existe en el mismo tenant
        // pero el usuario nunca fue asignado a ella — ni membership ni CompanyUserBranch.
        var membershipA = CompanyUserMembership.Create(companyA.Id, user.Id, "Admin", null, _adminId);
        db.CompanyUserMemberships.Add(membershipA);
        await db.SaveChangesAsync();

        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, companyA.Id, membershipA.Id, branchA.Id, _adminId)
        );
        await db.SaveChangesAsync();
    }

    private static Branch NewBranch(Guid tenantId, Guid companyId, string name, string code) =>
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
            tenantId, // createdBy no se usa en las aserciones; reutiliza tenantId como valor válido
            companyId: companyId
        );
}

[Trait("Category", "PostgreSql")]
public sealed class SessionContextCompanyBranchIsolationHttpTests
    : IClassFixture<SessionContextCompanyBranchIsolationHttpFixture>
{
    private const string SessionContextEndpoint = "/api/v1/session/context";
    private readonly SessionContextCompanyBranchIsolationHttpFixture _f;

    public SessionContextCompanyBranchIsolationHttpTests(
        SessionContextCompanyBranchIsolationHttpFixture fixture
    ) => _f = fixture;

    [Fact]
    public async Task Session_context_con_X_Company_Id_de_empresa_no_asignada_no_filtra_su_razon_social()
    {
        // Regresión del bug encontrado en esta fase: GetSessionContextQuery es ITenantScopedRequest
        // (no ICompanyScopedRequest), así que CompanyScopeBehavior nunca corre el guard para esta
        // query — la única barrera es que CompanyContextProvider valide membership antes de confiar
        // en el header. Antes del fix, este request devolvía 200 con Tenant.DisplayName =
        // CompanyBLegalName sin que el usuario tuviera ninguna relación con Empresa B.
        using var request = new HttpRequestMessage(HttpMethod.Get, SessionContextEndpoint);
        request.Headers.Add("X-Company-Id", _f.CompanyBId.ToString());

        using var response = await _f.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        body.Should()
            .NotContain(
                _f.CompanyBLegalName,
                "el usuario no tiene membership en Empresa B; el header no debe bastar para leer su razón social"
            );
    }

    [Fact]
    public async Task Session_context_con_X_Company_Id_y_X_Branch_Id_validos_devuelve_esa_sucursal()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SessionContextEndpoint);
        request.Headers.Add("X-Company-Id", _f.CompanyAId.ToString());
        request.Headers.Add("X-Branch-Id", _f.BranchAId.ToString());

        using var response = await _f.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain(_f.BranchAId.ToString());
    }
}

/// <summary>
/// Segundo fixture: usuario con acceso legítimo (membership + CompanyUserBranch) tanto a Empresa A
/// como a Empresa B — a diferencia de <see cref="CompanyBranchIsolationHttpFixture"/>, aquí Empresa
/// B SÍ está autorizada. Esto aísla el caso "header de sucursal viejo tras cambiar de empresa" del
/// caso "empresa nunca autorizada": si el rechazo se mantiene incluso con acceso a ambas empresas,
/// confirma que el backend valida la combinación (company, branch) como una unidad y no solo
/// "¿el usuario tiene acceso a esta empresa?" + "¿el usuario tiene acceso a esta sucursal en algún
/// lado?" por separado.
/// </summary>
public sealed class MultiCompanyStaleBranchHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
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
            "ZH-MultiCompanyStaleBranch-Test",
            $"zh-mcsb-{Guid.NewGuid():N}",
            _adminId
        );
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var companyA = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa A Multi S.A.",
            createdBy: _adminId
        );
        var companyB = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa B Multi S.A.",
            createdBy: _adminId
        );
        db.Companies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();
        CompanyAId = companyA.Id;
        CompanyBId = companyB.Id;

        var branchA = Branch.Create(
            TenantId,
            "Matriz A",
            "Av. Principal 123",
            "SUC-A",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            isMainBranch: true,
            _adminId,
            companyId: companyA.Id
        );
        var branchB = Branch.Create(
            TenantId,
            "Matriz B",
            "Av. Secundaria 456",
            "SUC-B",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            isMainBranch: true,
            _adminId,
            companyId: companyB.Id
        );
        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();
        BranchAId = branchA.Id;
        BranchBId = branchB.Id;

        var user = IdentityUser.Create(
            $"usuario-{Guid.NewGuid():N}",
            "Usuario",
            "Multi",
            $"usuario-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        UserId = user.Id;

        // Usuario autorizado en AMBAS empresas, cada una con su propia sucursal asignada.
        var membershipA = CompanyUserMembership.Create(companyA.Id, user.Id, "Admin", null, _adminId);
        var membershipB = CompanyUserMembership.Create(companyB.Id, user.Id, "Admin", null, _adminId);
        db.CompanyUserMemberships.AddRange(membershipA, membershipB);
        await db.SaveChangesAsync();

        db.CompanyUserBranches.AddRange(
            CompanyUserBranch.Create(TenantId, companyA.Id, membershipA.Id, branchA.Id, _adminId),
            CompanyUserBranch.Create(TenantId, companyB.Id, membershipB.Id, branchB.Id, _adminId)
        );
        await db.SaveChangesAsync();
    }
}

[Trait("Category", "PostgreSql")]
public sealed class MultiCompanyStaleBranchHttpTests
    : IClassFixture<MultiCompanyStaleBranchHttpFixture>
{
    private const string CashRegistersEndpoint = "/api/v1/cash-registers";
    private const string SessionContextEndpoint = "/api/v1/session/context";
    private readonly MultiCompanyStaleBranchHttpFixture _f;

    public MultiCompanyStaleBranchHttpTests(MultiCompanyStaleBranchHttpFixture fixture) =>
        _f = fixture;

    [Fact]
    public async Task Empresa_B_con_branch_de_Empresa_A_falla_403_aunque_ambas_empresas_esten_autorizadas()
    {
        using var response = await SendAsync(CashRegistersEndpoint, _f.CompanyBId, _f.BranchAId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Empresa_B_con_su_propia_branch_sigue_siendo_aceptada_200()
    {
        using var response = await SendAsync(CashRegistersEndpoint, _f.CompanyBId, _f.BranchBId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Session_context_con_Empresa_B_y_branch_vieja_de_Empresa_A_no_filtra_esa_sucursal()
    {
        using var response = await SendAsync(SessionContextEndpoint, _f.CompanyBId, _f.BranchAId);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should()
            .NotContain(
                _f.BranchAId.ToString(),
                "el header de sucursal de Empresa A no debe adoptarse como contexto de Empresa B"
            );
    }

    private Task<HttpResponseMessage> SendAsync(string endpoint, Guid companyId, Guid branchId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Add("X-Company-Id", companyId.ToString());
        request.Headers.Add("X-Branch-Id", branchId.ToString());
        return _f.Client.SendAsync(request);
    }
}
