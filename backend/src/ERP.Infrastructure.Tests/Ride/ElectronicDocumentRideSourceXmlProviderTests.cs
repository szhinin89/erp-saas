using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Ride.Services;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Ride.ElectronicDocumentsAdapter;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Suite de integración (PostgreSQL real + MediatR real + IFileStorage real vía
/// <see cref="LocalFileStorage"/> apuntando a un directorio temporal) para
/// <see cref="ElectronicDocumentRideSourceXmlProvider"/> — cubre exactamente los 4 escenarios de
/// ADR-025 §8: Authorized+XML, Authorized sin XML (H1 → PendingSource), Rejected → NotApplicable,
/// Cancelled → NotApplicable. No registra <c>CompanyScopeBehavior</c>/<c>ValidationBehavior</c>
/// (irrelevantes para lo que se prueba aquí) — sí registra MediatR real con los handlers reales
/// de ElectronicDocuments, sin mocks.
/// </summary>
public sealed class ElectronicDocumentRideSourceXmlProviderTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_ride_source_xml_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly string _fileStorageBasePath = Path.Combine(
        Path.GetTempPath(),
        "ride-source-xml-tests-" + Guid.NewGuid().ToString("N")
    );

    private ServiceProvider _serviceProvider = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private readonly Guid _userId = Guid.NewGuid();
    private const string SourceModule = "Sales";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FileStorage:BasePath"] = _fileStorageBasePath }
            )
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ERP.Application.DependencyInjection).Assembly)
        );
        services.AddDbContext<ErpDbContext>(
            (sp, options) => options.UseNpgsql(_postgres.GetConnectionString())
        );
        services.AddScoped<ICurrentTenant>(_ => new FixedCurrentTenant(() => _tenantId));
        services.AddScoped<ICurrentCompany>(_ => new FixedCurrentCompany(() => _companyId));
        services.AddScoped<IElectronicDocumentRepository, ElectronicDocumentRepository>();
        services.AddScoped<ERP.Application.Common.Interfaces.IFileStorage, LocalFileStorage>();
        services.AddScoped<IRideSourceXmlProvider, ElectronicDocumentRideSourceXmlProvider>();
        services.AddScoped<
            ERP.Application.Common.Services.ICompanyClock,
            ERP.Infrastructure.Persistence.Services.CompanyClock
        >();

        // ElectronicDocument.SaveChangesAsync publica domain events (ElectronicDocumentAuditHandler,
        // Entity Audit ADR-022) — se registra la infraestructura real, sin mocks, mismo patrón que
        // PricingRuleAuditIntegrationTests.
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped(typeof(IAuditReader<>), typeof(EfAuditReader<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => _tenantId,
            () => _companyId,
            _userId
        ));

        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(_fileStorageBasePath))
            Directory.Delete(_fileStorageBasePath, recursive: true);
    }

    [Fact]
    public async Task Authorized_document_with_stored_xml_returns_available()
    {
        const string xmlContent = "<factura>contenido de prueba</factura>";
        var sourceEntityId = Guid.NewGuid();
        var authorizedPath =
            $"electronic-documents/{_tenantId:N}/invoice/{Guid.NewGuid():N}/authorized.xml";

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var fileStorage =
                scope.ServiceProvider.GetRequiredService<ERP.Application.Common.Interfaces.IFileStorage>();
            await using var contentStream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(xmlContent)
            );
            await fileStorage.SaveAsync(authorizedPath, contentStream);

            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var document = BuildAuthorizedDocument(sourceEntityId, authorizedPath);
            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        var result = await GetAuthorizedXmlAsync(sourceEntityId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(RideSourceXmlStatus.Available);
        result.Value.AuthorizedXml.Should().Be(xmlContent);
        // H5 — la traducción ElectronicDocumentType (ElectronicDocuments) → RideDocumentType (Ride)
        // es responsabilidad exclusiva de ElectronicDocumentRideSourceXmlProvider (ADR-025 §8);
        // este assert prueba que la traducción ocurrió, no solo que el campo tiene un valor.
        result.Value.DocumentType.Should().Be(RideDocumentType.Invoice);
    }

    [Fact]
    public async Task H2_lookup_is_keyed_by_sourceModule_and_sourceEntityId_never_by_ElectronicDocument_Id()
    {
        // H2 (ADR-025 §8/auditoría): el contrato real de lectura de ElectronicDocuments se
        // identifica por (SourceModule, SourceEntityId), nunca por ElectronicDocument.Id — este
        // test prueba la negativa explícita: consultar con el Id interno del agregado (que existe
        // y es una fila real en la base) debe comportarse como "no encontrado", nunca como un
        // alias válido de SourceEntityId.
        const string xmlContent = "<factura>contenido de prueba</factura>";
        var sourceEntityId = Guid.NewGuid();
        var authorizedPath =
            $"electronic-documents/{_tenantId:N}/invoice/{Guid.NewGuid():N}/authorized.xml";
        Guid electronicDocumentId;

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var fileStorage =
                scope.ServiceProvider.GetRequiredService<ERP.Application.Common.Interfaces.IFileStorage>();
            await using var contentStream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(xmlContent)
            );
            await fileStorage.SaveAsync(authorizedPath, contentStream);

            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var document = BuildAuthorizedDocument(sourceEntityId, authorizedPath);
            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync();
            electronicDocumentId = document.Id;
        }

        electronicDocumentId
            .Should()
            .NotBe(
                sourceEntityId,
                "la prueba solo es válida si el Id interno del agregado es distinto de SourceEntityId"
            );

        var byRealSourceEntityId = await GetAuthorizedXmlAsync(sourceEntityId);
        var byInternalAggregateId = await GetAuthorizedXmlAsync(electronicDocumentId);

        byRealSourceEntityId.IsSuccess.Should().BeTrue();
        byRealSourceEntityId.Value!.Status.Should().Be(RideSourceXmlStatus.Available);

        byInternalAggregateId.IsSuccess.Should().BeTrue();
        byInternalAggregateId
            .Value!.Status.Should()
            .Be(
                RideSourceXmlStatus.NotApplicable,
                "buscar por ElectronicDocument.Id (en vez de SourceEntityId) no debe encontrar el documento"
            );
    }

    [Fact]
    public async Task Authorized_document_without_stored_xml_returns_pending_source_never_throws()
    {
        var sourceEntityId = Guid.NewGuid();

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var document = BuildAuthorizedDocument(sourceEntityId, authorizedXmlPath: null);
            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        var act = () => GetAuthorizedXmlAsync(sourceEntityId);

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeTrue();
        result.Subject.Value!.Status.Should().Be(RideSourceXmlStatus.PendingSource);
        result.Subject.Value.AuthorizedXml.Should().BeNull();
    }

    [Fact]
    public async Task Rejected_document_returns_not_applicable()
    {
        var sourceEntityId = Guid.NewGuid();

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var document = BuildRejectedDocument(sourceEntityId);
            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        var result = await GetAuthorizedXmlAsync(sourceEntityId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(RideSourceXmlStatus.NotApplicable);
    }

    [Fact]
    public async Task Cancelled_document_returns_not_applicable()
    {
        var sourceEntityId = Guid.NewGuid();

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var document = BuildCancelledDocument(sourceEntityId);
            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        var result = await GetAuthorizedXmlAsync(sourceEntityId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(RideSourceXmlStatus.NotApplicable);
    }

    private async Task<Result<RideSourceXmlLookup>> GetAuthorizedXmlAsync(Guid sourceEntityId)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IRideSourceXmlProvider>();
        return await provider.GetAuthorizedXmlAsync(
            _tenantId,
            _companyId,
            SourceModule,
            sourceEntityId
        );
    }

    private ElectronicDocument BuildReceivedDocument(Guid sourceEntityId)
    {
        var document = ElectronicDocument.Create(
            _tenantId,
            _companyId,
            ElectronicDocumentType.Invoice,
            SourceModule,
            sourceEntityId,
            _userId
        );
        document.MarkXmlGenerated("path/draft.xml", "1.1.0", "1.1.0", _userId);
        document.MarkSigned("path/signed.xml", AccessKey.Create(new string('7', 49)), _userId);
        document.MarkSent(_userId);
        document.MarkReceived(_userId);
        return document;
    }

    private ElectronicDocument BuildAuthorizedDocument(
        Guid sourceEntityId,
        string? authorizedXmlPath
    )
    {
        var document = BuildReceivedDocument(sourceEntityId);
        document.MarkAuthorized(
            AuthorizationNumber.Create(new string('7', 49)),
            DateTime.UtcNow,
            authorizedXmlPath,
            _userId
        );
        return document;
    }

    private ElectronicDocument BuildRejectedDocument(Guid sourceEntityId)
    {
        var document = BuildReceivedDocument(sourceEntityId);
        document.MarkRejected("El SRI rechazó el comprobante.", _userId);
        return document;
    }

    private ElectronicDocument BuildCancelledDocument(Guid sourceEntityId)
    {
        var document = BuildAuthorizedDocument(sourceEntityId, authorizedXmlPath: null);
        document.MarkCancelled("Anulado por el usuario.", _userId);
        return document;
    }
}

file sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
{
    public Guid TenantId => tenantId();
    public string? Slug => null;
}

file sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
{
    public Guid CompanyId => companyId();
    public bool IsAuthenticated => true;
    public bool HasCompanyContext => true;
}

file sealed class FixedAuditContext(Func<Guid> tenantId, Func<Guid> companyId, Guid userId)
    : IAuditContext
{
    public Guid TenantId => tenantId();
    public Guid CompanyId => companyId();
    public AuditActor Actor =>
        new(
            TenantId: tenantId(),
            UserId: userId,
            UserName: "Test User",
            FullName: "Test User",
            Email: "test.user@example.com",
            RoleName: "Admin",
            CorrelationId: "test-correlation",
            RequestId: "test-request",
            Source: AuditSource.UserAction
        );
}
