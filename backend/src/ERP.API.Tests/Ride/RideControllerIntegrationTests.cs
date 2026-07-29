using ERP.API.Tests.Support;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.API.Tests.Ride;

/// <summary>
/// Fase 9 (ADR-025): recorrido HTTP real — HTTP → RideController → MediatR → RidePipeline →
/// Storage → Respuesta — contra PostgreSQL real (Testcontainers) y un <see cref="Program"/> real
/// completo (todo el DI de ERP.API.Program, incluida la wiring real de Ride). El único
/// reemplazo respecto a producción es <c>IFileStorage</c>, apuntado a un directorio temporal
/// (mismo mecanismo ya usado en <see cref="Support.NoOpFileStorage"/> para otras suites, aquí con
/// un <see cref="LocalFileStorage"/> real porque Ride sí necesita leer/escribir archivos reales).
/// </summary>
public sealed class RideControllerIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new();

    public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory => _baseFactory;
    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>Empresa operativa activa para el próximo request — mismo mecanismo que el resto de la suite de integración HTTP.</summary>
    public Guid CurrentCompanyId
    {
        get => _baseFactory.MutableCompany.CompanyId;
        set => _baseFactory.MutableCompany.CompanyId = value;
    }

    // Ruta física real donde LocalFileStorage escribirá — sin BasePath configurado, cae a
    // {CWD}/files (ver LocalFileStorage). No se sobreescribe vía WithWebHostBuilder: envolver la
    // fábrica base rompía la configuración de Jwt:SecretKey ya establecida por
    // PostgreSqlTestWebAppFactory.ConfigureWebHost (el WWW-Authenticate devolvía "signature key
    // was not found" — hallazgo real de esta fase). El aislamiento entre corridas de test queda
    // garantizado igual: cada fixture usa un TenantId nuevo, y la ruta de Ride ya empieza por
    // "ride/{tenantId:N}/..." (ADR-025 §15).
    private string DefaultFileStorageRoot => Path.Combine(Directory.GetCurrentDirectory(), "files", "ride", TenantId.ToString("N"));

    public async Task InitializeAsync()
    {
        // Hallazgo real de esta fase: PostgreSqlTestWebAppFactory.ConfigureWebHost fija
        // Jwt:SecretKey vía AddInMemoryCollection, pero JwtExtensions.AddJwtAuthentication lo lee
        // de forma EAGER (variable de closure, no perezosa) dentro de Program.cs — para cuando
        // ese override in-memory se aplica, AddJwtAuthentication ya capturó el valor de
        // appsettings.json ("CHANGE_ME_..."), y cualquier JWT firmado con la clave de test es
        // rechazado con "signature key was not found" (confirmado empíricamente, no es una
        // suposición). La propia excepción de JwtExtensions ya documenta la vía de override
        // soportada: variable de entorno JWT__SECRETKEY — que sí se carga como fuente de
        // configuración desde la primera línea de Program.cs (WebApplicationBuilder.CreateBuilder
        // registra AddEnvironmentVariables antes de que corra cualquier código de la app), así que
        // no compite con el mismo problema de orden. Fijado aquí, en el test, sin tocar
        // JwtExtensions.cs/Program.cs (explícitamente prohibido esta fase) ni
        // PostgreSqlTestWebAppFactory (infraestructura compartida).
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        Client = Factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateSessionJwt(TenantId, UserId));

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableUser.UserId = UserId;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (Directory.Exists(DefaultFileStorageRoot))
            Directory.Delete(DefaultFileStorageRoot, recursive: true);
    }

    private async Task SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-Ride-Api-Test", $"zh-ride-api-{Guid.NewGuid():N}", adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var companyA = Company.CreateManaged(
            TenantId, taxIdentificationNumber: $"179{TenantId:N}"[..13], legalName: "Empresa A S.A.", createdBy: adminId);
        var companyB = Company.CreateManaged(
            TenantId, taxIdentificationNumber: $"180{TenantId:N}"[..13], legalName: "Empresa B S.A.", createdBy: adminId);
        db.Companies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();
        CompanyAId = companyA.Id;
        CompanyBId = companyB.Id;

        var user = IdentityUser.Create($"ride-api-{Guid.NewGuid():N}", "Test", "User", $"ride-api-{Guid.NewGuid():N}@example.com", "hash", adminId);
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        UserId = user.Id;

        // Usuario legítimo de AMBAS empresas — así "company diferente" prueba el filtro real de
        // datos (EF global query filter), no un simple 403 por falta de membership.
        db.CompanyUserMemberships.Add(CompanyUserMembership.Create(CompanyAId, UserId, "ADMIN", null, adminId));
        db.CompanyUserMemberships.Add(CompanyUserMembership.Create(CompanyBId, UserId, "ADMIN", null, adminId));
        await db.SaveChangesAsync();
    }

    /// <summary>Registra un ElectronicDocument Authorized con XML real para <paramref name="companyId"/>.</summary>
    public async Task<Guid> SeedAuthorizedInvoiceAsync(Guid companyId, string sourceModule, Guid sourceEntityId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var userId = Guid.NewGuid();
        // Clave de acceso única por documento (uq_electronic_document_access_key) — derivada de
        // sourceEntityId (único por llamada en esta suite), cada carácter hex mapeado a un dígito.
        var hexDigits = sourceEntityId.ToString("N")
            .Select(c => char.IsDigit(c) ? c : (char)('0' + (c - 'a')));
        var accessKeyDigits = (new string(hexDigits.ToArray()) + new string('9', 49))[..49];
        var document = ElectronicDocument.Create(
            TenantId, companyId, ElectronicDocumentType.Invoice, sourceModule, sourceEntityId, userId);
        document.MarkXmlGenerated("path/draft.xml", "1.1.0", "1.1.0", userId);
        document.MarkSigned("path/signed.xml", AccessKey.Create(accessKeyDigits), userId);
        document.MarkSent(userId);
        document.MarkReceived(userId);

        var authorizedPath = $"electronic-documents/{TenantId:N}/invoice/{document.Id:N}/authorized.xml";
        var xml = RideApiTestXmlFactory.RealAuthorizedInvoiceXml();
        await using (var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
            await fileStorage.SaveAsync(authorizedPath, content);

        document.MarkAuthorized(
            AuthorizationNumber.Create(accessKeyDigits), DateTime.UtcNow, authorizedPath, userId);

        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();

        return document.Id;
    }
}

[Trait("Category", "PostgreSql")]
public sealed class RideControllerIntegrationTests : IClassFixture<RideControllerIntegrationFixture>
{
    private readonly RideControllerIntegrationFixture _f;

    // Program.cs registra JsonStringEnumConverter globalmente (ver Program.cs línea ~59), así que
    // la API serializa RideOutcome como string ("Generated", no 0). ReadFromJsonAsync<T> con
    // opciones por defecto no lo sabe y lanza JsonException al leer $.data.outcome — mismo
    // convertidor replicado aquí, solo dentro del test, sin tocar Program.cs ni los contratos.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public RideControllerIntegrationTests(RideControllerIntegrationFixture fixture) => _f = fixture;

    [Fact]
    public async Task Unauthenticated_request_returns_401_same_as_any_protected_controller()
    {
        using var anonymousClient = _f.Factory.CreateClient();

        var response = await anonymousClient.GetAsync($"/api/v1/ride?sourceModule=Sales&sourceEntityId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Owning_company_user_gets_200_with_generated_outcome_end_to_end()
    {
        _f.CurrentCompanyId = _f.CompanyAId;
        var sourceEntityId = Guid.NewGuid();
        await _f.SeedAuthorizedInvoiceAsync(_f.CompanyAId, "Sales", sourceEntityId);

        var response = await _f.Client.GetAsync($"/api/v1/ride?sourceModule=Sales&sourceEntityId={sourceEntityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RideApiResponseEnvelope>(ResponseJsonOptions);
        body!.Data!.Outcome.Should().Be(RideOutcome.Generated);
        body.Data.StoragePath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Nonexistent_document_returns_200_with_not_applicable()
    {
        _f.CurrentCompanyId = _f.CompanyAId;

        var response = await _f.Client.GetAsync($"/api/v1/ride?sourceModule=Sales&sourceEntityId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RideApiResponseEnvelope>(ResponseJsonOptions);
        body!.Data!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }

    [Fact]
    public async Task Other_company_user_never_receives_another_companys_ride()
    {
        var sourceEntityId = Guid.NewGuid();
        await _f.SeedAuthorizedInvoiceAsync(_f.CompanyAId, "Sales", sourceEntityId);

        // Mismo usuario, contexto operativo en la OTRA empresa (miembro legítimo de ambas).
        _f.CurrentCompanyId = _f.CompanyBId;

        var response = await _f.Client.GetAsync($"/api/v1/ride?sourceModule=Sales&sourceEntityId={sourceEntityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RideApiResponseEnvelope>(ResponseJsonOptions);
        body!.Data!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }

    [Fact]
    public async Task Regenerate_owning_company_returns_200_generated()
    {
        _f.CurrentCompanyId = _f.CompanyAId;
        var sourceEntityId = Guid.NewGuid();
        await _f.SeedAuthorizedInvoiceAsync(_f.CompanyAId, "Sales", sourceEntityId);

        var response = await _f.Client.PostAsJsonAsync(
            "/api/v1/ride/regenerate", new { sourceModule = "Sales", sourceEntityId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RideApiResponseEnvelope>(ResponseJsonOptions);
        body!.Data!.Outcome.Should().Be(RideOutcome.Generated);
    }

    [Fact]
    public async Task Regenerate_nonexistent_document_returns_200_not_applicable()
    {
        _f.CurrentCompanyId = _f.CompanyAId;

        var response = await _f.Client.PostAsJsonAsync(
            "/api/v1/ride/regenerate", new { sourceModule = "Sales", sourceEntityId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RideApiResponseEnvelope>(ResponseJsonOptions);
        body!.Data!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }

    [Fact]
    public async Task Regenerate_other_company_document_returns_200_not_applicable()
    {
        var sourceEntityId = Guid.NewGuid();
        await _f.SeedAuthorizedInvoiceAsync(_f.CompanyAId, "Sales", sourceEntityId);
        _f.CurrentCompanyId = _f.CompanyBId;

        var response = await _f.Client.PostAsJsonAsync(
            "/api/v1/ride/regenerate", new { sourceModule = "Sales", sourceEntityId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RideApiResponseEnvelope>(ResponseJsonOptions);
        body!.Data!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }
}

/// <summary>Forma mínima del envelope necesaria para deserializar en los tests — nunca un DTO de negocio nuevo.</summary>
internal sealed record RideApiResponseEnvelope(RideGenerationResultDto? Data);
