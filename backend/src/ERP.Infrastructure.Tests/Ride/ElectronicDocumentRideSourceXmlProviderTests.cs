using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Ride.Services;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
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
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// PostgreSQL + canonical internal reads + real file storage. Covers source states, explicit scope,
/// background PDF generation through the shared pipeline and unchanged HTTP authentication guards.
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
        services.AddScoped<IElectronicDocumentInternalReader, ElectronicDocumentInternalReader>();
        services.AddScoped<IRideSourceXmlProvider, ElectronicDocumentRideSourceXmlProvider>();
        services.AddScoped(_ =>
        {
            var mock = new Mock<ISalesInvoiceRepository>();
            mock.Setup(r =>
                    r.GetByIdAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((SalesInvoice?)null);
            return mock.Object;
        });
        services.AddScoped(_ => Mock.Of<ICompanyRepository>());
        // ZH-EDOC-COMMUNICATIONS-01 — ElectronicDocumentAuthorizedCommunicationHandler (MediatR real, ver
        // comentario de clase) solo necesita el servicio genérico de comunicaciones: un test double vacío
        // alcanza (estos escenarios prueban el RIDE, no el correo).
        services.AddScoped(_ =>
            Mock.Of<ERP.Application.Modules.Communications.ElectronicDocuments.IElectronicDocumentCommunicationService>()
        );
        services.AddLogging();
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
        var authorizedAtUtc = new DateTime(2026, 10, 13, 16, 27, 19, DateTimeKind.Utc);
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
            var document = BuildAuthorizedDocument(sourceEntityId, authorizedPath, authorizedAtUtc);
            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        var result = await GetAuthorizedXmlAsync(sourceEntityId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(RideSourceXmlStatus.Available);
        result.Value.AuthorizedXml.Should().Be(xmlContent);
        // SSOT de la fecha de autorización: ElectronicDocument.AuthorizationDate (UTC), nunca el XML.
        result.Value.AuthorizationDateUtc.Should().Be(authorizedAtUtc);
        result.Value.AuthorizationDateUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
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

    /// <summary>
    /// SRI-ELECTRONIC-DOCUMENTS-QA-FIX-01 — cross-tenant vía el contexto ambiental real (mismo
    /// mecanismo que producción: <c>GetElectronicDocumentQuery</c>/<c>GetElectronicDocumentXmlQuery</c>
    /// resuelven tenant/empresa desde <c>ICurrentTenant</c>/<c>ICurrentCompany</c>, nunca desde
    /// los parámetros de este método). Se muta el campo ambiental <c>_tenantId</c> del fixture —
    /// que también controla el <c>tenantId</c> explícito pasado por <see cref="GetAuthorizedXmlAsync"/>,
    /// así que el guard de SRI-RIDE-SOURCE-CONTEXT-CLEANUP-02 (ver <see cref="GetAuthorizedXmlAsync_with_mismatched_tenantId_parameter_fails_closed_without_querying"/>)
    /// no interfiere aquí: ambos valores siguen coincidiendo entre sí, solo cambian respecto al
    /// tenant dueño del documento sembrado.
    /// </summary>
    [Fact]
    public async Task Authorized_document_is_never_returned_to_a_different_ambient_tenant()
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

        var owningTenantId = _tenantId;
        _tenantId = Guid.NewGuid();
        try
        {
            var result = await GetAuthorizedXmlAsync(sourceEntityId);

            result.IsSuccess.Should().BeTrue();
            result.Value!.Status.Should().Be(RideSourceXmlStatus.NotApplicable);
            result.Value.AuthorizedXml.Should().BeNull();
        }
        finally
        {
            _tenantId = owningTenantId;
        }
    }

    /// <summary>
    /// SRI-RIDE-SOURCE-CONTEXT-CLEANUP-02 — <paramref name="tenantId"/>/<paramref name="companyId"/>
    /// ya no son parámetros decorativos: <see cref="ElectronicDocumentRideSourceXmlProvider"/> los
    /// valida contra <c>ICurrentTenant</c>/<c>ICurrentCompany</c> antes de consultar nada. Un
    /// <c>tenantId</c> explícito que no coincide con el ambiental (esto nunca debería pasar en
    /// producción — ambos siempre se derivan del mismo contexto autenticado) falla cerrado con
    /// <c>Forbidden</c>, sin llegar a enviar ningún request MediatR.
    /// </summary>
    [Fact]
    public async Task GetAuthorizedXmlAsync_with_mismatched_tenantId_parameter_fails_closed_without_querying()
    {
        var sourceEntityId = Guid.NewGuid();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IRideSourceXmlProvider>();
        var result = await provider.GetAuthorizedXmlAsync(
            Guid.NewGuid(), // no coincide con el ICurrentTenant ambiental (_tenantId)
            _companyId,
            SourceModule,
            sourceEntityId
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
    }

    /// <summary>Mismo guard que arriba, para el lado de <c>companyId</c>.</summary>
    [Fact]
    public async Task GetAuthorizedXmlAsync_with_mismatched_companyId_parameter_fails_closed_without_querying()
    {
        var sourceEntityId = Guid.NewGuid();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IRideSourceXmlProvider>();
        var result = await provider.GetAuthorizedXmlAsync(
            _tenantId,
            Guid.NewGuid(), // no coincide con el ICurrentCompany ambiental (_companyId)
            SourceModule,
            sourceEntityId
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
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


    [Fact]
    public async Task Background_attachment_uses_real_adapter_pipeline_renderer_and_postgres_without_http()
    {
        using var job = JobExecutionContext.Begin(_tenantId, _companyId);
        var http = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        var tenant = new CurrentTenantService(http);
        var company = new CurrentCompanyService(http);
        var user = new CurrentUserService(http);
        user.IsAuthenticated.Should().BeFalse();
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<ERP.Application.Common.Interfaces.IFileStorage>();
        var sourceId = Guid.NewGuid();
        var path = $"background/{sourceId:N}.xml";
        using (var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            RidePipelineQuestPdfIntegrationTests.RealInvoiceXml())))
            await files.SaveAsync(path, content);
        var document = BuildAuthorizedDocument(sourceId, path);
        db.ElectronicDocuments.Add(document);
        db.CompanyPrecisionPolicies.Add(ERP.Domain.Configuration.Entities.CompanyPrecisionPolicy
            .CreateStandardCommercial(_tenantId, _companyId, _userId));
        await db.SaveChangesAsync();

        var reader = new ElectronicDocumentInternalReader(
            new ElectronicDocumentRepository(db, scope.ServiceProvider.GetRequiredService<ERP.Application.Common.Services.ICompanyClock>()), files, tenant, company);
        var adapter = new ElectronicDocumentRideSourceXmlProvider(reader, tenant, company);
        var repository = new ERP.Infrastructure.Persistence.Repositories.Ride.RidePdfDocumentRepository(
            db, new PostgresDatabaseExceptionTranslator());
        var branding = new Mock<ERP.Application.Modules.Ride.Branding.IRideBrandingProvider>();
        branding.Setup(b => b.GetAsync(_tenantId, _companyId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ERP.Domain.Modules.Ride.ValueObjects.RideBranding>.Success(
                ERP.Domain.Modules.Ride.ValueObjects.RideBranding.Empty()));
        var pipeline = new RidePipeline(adapter,
            new ERP.Application.Modules.Ride.Parsers.RideXmlParserResolver([
                new ERP.Application.Modules.Ride.Parsers.InvoiceRideXmlParser()]),
            new ERP.Application.Modules.Ride.Templates.RideTemplateResolver([
                new ERP.Application.Modules.Ride.Templates.DefaultInvoiceRideTemplate()]),
            new RideCacheStrategy(repository), new RideContentHasher(), branding.Object,
            new ERP.Infrastructure.Ride.Rendering.QuestPdfRideRenderer(
                RideQrCodeGeneratorTestFactory.Create(), RideBarcodeGeneratorTestFactory.Create(), files),
            new ERP.Infrastructure.Ride.Storage.RidePdfStorageService(files,
                new ERP.Infrastructure.Ride.Storage.RidePdfStorageNamingStrategy()),
            repository, user,
            new ERP.Infrastructure.Persistence.Repositories.CompanyConfig.CompanyPrecisionPolicyRepository(db),
            new ERP.Infrastructure.Persistence.Repositories.CompanyRepository(db));
        var background = new RideBackgroundDocumentService(new RideDocumentService(pipeline), tenant, company);
        var provider = new ERP.Application.Modules.Ride.Communications.RidePdfCommunicationAttachmentProvider(
            background, files, Microsoft.Extensions.Logging.Abstractions.NullLogger<
                ERP.Application.Modules.Ride.Communications.RidePdfCommunicationAttachmentProvider>.Instance);
        var reference = new ERP.Application.Modules.Communications.Services.CommunicationAttachmentReference(
            _tenantId, _companyId, ERP.Domain.Modules.Communications.Enums.CommunicationAttachmentType.RidePdf,
            document.Id, SourceModule, sourceId);
        var result = await provider.ResolveAsync(reference);
        result.SkippedReason.Should().BeNull();
        System.Text.Encoding.ASCII.GetString(result.Content!, 0, 5).Should().Be("%PDF-");
        var stored = await db.RidePdfDocuments.SingleAsync(r => r.ElectronicDocumentId == document.Id);
        stored.CompanyId.Should().Be(_companyId);
        stored.TenantId.Should().Be(_tenantId);
        var cached = await background.GetOrGenerateAsync(_tenantId, _companyId, SourceModule, sourceId);
        cached.Value!.Outcome.Should().Be(ERP.Application.Modules.Ride.DTOs.RideOutcome.Cached);

        (await background.GetOrGenerateAsync(Guid.NewGuid(), _companyId, SourceModule, sourceId))
            .Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        (await background.GetOrGenerateAsync(_tenantId, Guid.NewGuid(), SourceModule, sourceId))
            .Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        (await reader.GetXmlAsync(_tenantId, Guid.NewGuid(), SourceModule, sourceId,
            ElectronicDocumentXmlVariant.Authorized)).Code.Should().Be(ApiResponseCodes.Common.Forbidden);
    }


    [Fact]
    public async Task Internal_reader_does_not_expose_a_document_from_another_company()
    {
        using var job = JobExecutionContext.Begin(_tenantId, _companyId);
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var otherCompany = Company.CreateManaged(_tenantId, "1790012345002", "Other company", createdBy: _userId);
        db.Companies.Add(otherCompany);
        var sourceId = Guid.NewGuid();
        db.ElectronicDocuments.Add(ElectronicDocument.Create(_tenantId, otherCompany.Id,
            ElectronicDocumentType.Invoice, SourceModule, sourceId, _userId));
        await db.SaveChangesAsync();
        var http = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        var reader = new ElectronicDocumentInternalReader(
            new ElectronicDocumentRepository(db, scope.ServiceProvider.GetRequiredService<ERP.Application.Common.Services.ICompanyClock>()),
            scope.ServiceProvider.GetRequiredService<ERP.Application.Common.Interfaces.IFileStorage>(),
            new CurrentTenantService(http), new CurrentCompanyService(http));
        var document = await reader.GetAsync(_tenantId, _companyId, SourceModule, sourceId);
        document.Value.Should().BeNull();
        var xml = await reader.GetXmlAsync(_tenantId, _companyId, SourceModule, sourceId,
            ElectronicDocumentXmlVariant.Authorized);
        xml.IsSuccess.Should().BeFalse();
        xml.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Http_query_without_authentication_still_rejects_even_with_job_scope()
    {
        using var job = JobExecutionContext.Begin(_tenantId, _companyId);
        var http = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        var company = new CurrentCompanyService(http);
        var user = new CurrentUserService(http);
        var guard = new CompanyAccessGuard(null!, null!, user, new CurrentTenantService(http),
            company, null!, null!, null!);
        var behavior = new ERP.Application.Behaviors.CompanyScopeBehavior<
            ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide.GetOrGenerateRideQuery,
            Result<ERP.Application.Modules.Ride.DTOs.RideGenerationResultDto>>(guard, company);
        var reached = false;
        var act = () => behavior.Handle(new ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide
            .GetOrGenerateRideQuery(SourceModule, Guid.NewGuid()),
            _ => { reached = true; throw new InvalidOperationException("Handler reached"); }, default);
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("No autenticado.");
        reached.Should().BeFalse();
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
        string? authorizedXmlPath,
        DateTime? authorizedAtUtc = null
    )
    {
        var document = BuildReceivedDocument(sourceEntityId);
        document.MarkAuthorized(
            AuthorizationNumber.Create(new string('7', 49)),
            authorizedAtUtc ?? DateTime.UtcNow,
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
        // ZH-RETENTION-SRI-ANNULMENT-01: Cancelled solo tras una anulación SRI confirmada con evidencia.
        var annulmentRequestId = Guid.NewGuid();
        document.MarkAnnulmentPending(annulmentRequestId, _userId);
        document.ConfirmExternalAnnulment(
            annulmentRequestId,
            new ERP.Domain.Modules.ElectronicDocuments.ValueObjects.ExternalAnnulmentEvidence(
                new DateOnly(2026, 10, 3),
                "SRI-TRAMITE-1",
                _userId
            ),
            _userId
        );
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
