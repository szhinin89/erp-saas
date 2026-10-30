using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// READINESS-2B-2 — contrato por sucursal de GET /api/v1/companies/operational-readiness contra
/// PostgreSQL (Testcontainers) y el pipeline HTTP real (X-Company-Id/X-Branch-Id, BranchScopeBehavior,
/// IBranchAccessGuard). Mismo patrón que <see cref="CompanyBranchIsolationHttpFixture"/>. Solo puntos
/// físicos: no ejercita el validador electrónico ni transmite nada al SRI.
/// </summary>
public sealed class CompanyOperationalReadinessBranchHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid BranchMatrizId { get; private set; }
    public Guid BranchNorteId { get; private set; }
    public Guid BranchWithoutAccessId { get; private set; }
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
            "ZH-ReadinessBranch-Test",
            $"zh-rdy-{Guid.NewGuid():N}",
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

        var matriz = NewBranch(companyA.Id, "Matriz", "SUC-M", isMainBranch: true);
        var norte = NewBranch(companyA.Id, "Norte", "SUC-N", isMainBranch: false);
        var withoutAccess = NewBranch(companyA.Id, "Sin acceso", "SUC-X", isMainBranch: false);
        var branchB = NewBranch(companyB.Id, "Matriz B", "SUC-B", isMainBranch: true);
        db.Branches.AddRange(matriz, norte, withoutAccess, branchB);
        await db.SaveChangesAsync();
        BranchMatrizId = matriz.Id;
        BranchNorteId = norte.Id;
        BranchWithoutAccessId = withoutAccess.Id;
        BranchBId = branchB.Id;

        // Matriz: caja con punto físico 001-002. Norte: caja sin punto. Empresa B: su propia caja.
        var pointA = await AddPhysicalPointAsync(db, companyA.Id, matriz.Id, "001", "002");
        var pointB = await AddPhysicalPointAsync(db, companyB.Id, branchB.Id, "001", "009");
        db.CashRegisters.AddRange(
            CashRegister.Create(TenantId, companyA.Id, matriz.Id, "CAJA-MATRIZ", "Caja Matriz", _adminId, pointA),
            CashRegister.Create(TenantId, companyA.Id, norte.Id, "CAJA-NORTE", "Caja Norte", _adminId),
            CashRegister.Create(TenantId, companyB.Id, branchB.Id, "CAJA-B", "Caja Empresa B", _adminId, pointB)
        );
        await db.SaveChangesAsync();

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

        var membership = CompanyUserMembership.Create(companyA.Id, user.Id, "Admin", null, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();

        // Autorizado para Matriz y Norte; nunca para "Sin acceso" ni para la sucursal de B.
        db.CompanyUserBranches.AddRange(
            CompanyUserBranch.Create(TenantId, companyA.Id, membership.Id, matriz.Id, _adminId),
            CompanyUserBranch.Create(TenantId, companyA.Id, membership.Id, norte.Id, _adminId)
        );
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddPhysicalPointAsync(
        ErpDbContext db,
        Guid companyId,
        Guid branchId,
        string establishmentCode,
        string pointCode
    )
    {
        var establishment = Establishment.Create(
            TenantId,
            branchId,
            companyId,
            establishmentCode,
            "Establecimiento",
            "Av. Principal 123",
            null,
            true,
            _adminId
        );
        db.Establishments.Add(establishment);
        await db.SaveChangesAsync();

        var point = EmissionPoint.Create(
            TenantId,
            companyId,
            establishment.Id,
            pointCode,
            null,
            EmissionType.Physical,
            true,
            _adminId
        );
        db.EmissionPoints.Add(point);
        await db.SaveChangesAsync();
        return point.Id;
    }

    private Branch NewBranch(Guid companyId, string name, string code, bool isMainBranch) =>
        Branch.Create(
            TenantId,
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
            _adminId,
            companyId: companyId
        );
}

[Trait("Category", "PostgreSql")]
public sealed class CompanyOperationalReadinessBranchHttpTests
    : IClassFixture<CompanyOperationalReadinessBranchHttpFixture>
{
    private const string Endpoint = "/api/v1/companies/operational-readiness";
    private readonly CompanyOperationalReadinessBranchHttpFixture _f;

    public CompanyOperationalReadinessBranchHttpTests(
        CompanyOperationalReadinessBranchHttpFixture fixture
    ) => _f = fixture;

    [Fact]
    public async Task Sucursal_matriz_devuelve_solo_sus_cajas_con_punto_y_estado_fiscal_fisico()
    {
        var data = await GetReadinessAsync(_f.BranchMatrizId);

        data.GetProperty("branchId").GetGuid().Should().Be(_f.BranchMatrizId);
        var register = data.GetProperty("cashRegisters").EnumerateArray().Should().ContainSingle().Subject;
        register.GetProperty("code").GetString().Should().Be("CAJA-MATRIZ");
        register.GetProperty("establishmentCode").GetString().Should().Be("001");
        register.GetProperty("emissionPointCode").GetString().Should().Be("002");
        register.GetProperty("emissionType").GetString().Should().Be("Physical");
        register.GetProperty("operationalStatus").GetString().Should().Be("Ready");
        register.GetProperty("fiscalStatus").GetString().Should().Be("NotVerified");
        ItemStatus(data, "cashRegister.withEmissionPoint").Should().Be("Ready");
    }

    [Fact]
    public async Task Sucursal_norte_no_hereda_las_cajas_de_la_matriz()
    {
        var data = await GetReadinessAsync(_f.BranchNorteId);

        data.GetProperty("branchId").GetGuid().Should().Be(_f.BranchNorteId);
        var register = data.GetProperty("cashRegisters").EnumerateArray().Should().ContainSingle().Subject;
        register.GetProperty("code").GetString().Should().Be("CAJA-NORTE");
        register.GetProperty("emissionPointId").ValueKind.Should().Be(JsonValueKind.Null);
        register.GetProperty("operationalStatus").GetString().Should().Be("Missing");
        register.GetProperty("fiscalStatus").GetString().Should().Be("NotApplicable");
        data.GetProperty("canSell").GetBoolean().Should().BeFalse();
        ItemStatus(data, "cashRegister.withEmissionPoint").Should().Be("Missing");
    }

    [Fact]
    public async Task Las_cajas_de_otra_empresa_nunca_aparecen()
    {
        foreach (var branchId in new[] { _f.BranchMatrizId, _f.BranchNorteId })
        {
            using var response = await SendAsync(_f.CompanyAId, branchId.ToString());
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotContain("CAJA-B");
        }
    }

    [Fact]
    public async Task Sin_X_Branch_Id_se_rechaza_y_no_se_elige_otra_sucursal()
    {
        using var response = await SendAsync(_f.CompanyAId, branchId: null);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().NotContain("CAJA-MATRIZ").And.NotContain("CAJA-NORTE");
    }

    [Fact]
    public async Task Sucursal_de_la_empresa_sin_autorizacion_del_usuario_se_rechaza()
    {
        using var response = await SendAsync(_f.CompanyAId, _f.BranchWithoutAccessId.ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sucursal_de_otra_empresa_se_rechaza_sin_filtrar_datos()
    {
        using var response = await SendAsync(_f.CompanyAId, _f.BranchBId.ToString());
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().NotContain("CAJA-B").And.NotContain(_f.BranchBId.ToString());
    }

    private async Task<JsonElement> GetReadinessAsync(Guid branchId)
    {
        using var response = await SendAsync(_f.CompanyAId, branchId.ToString());
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }

    private static string? ItemStatus(JsonElement data, string code) =>
        data.GetProperty("sections")
            .EnumerateArray()
            .SelectMany(s => s.GetProperty("items").EnumerateArray())
            .Single(i => i.GetProperty("code").GetString() == code)
            .GetProperty("status")
            .GetString();

    private async Task<HttpResponseMessage> SendAsync(Guid companyId, string? branchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Add("X-Company-Id", companyId.ToString());
        if (branchId is not null)
            request.Headers.Add("X-Branch-Id", branchId);

        return await _f.Client.SendAsync(request);
    }
}
