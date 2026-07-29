using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Ride;
using ERP.Infrastructure.Ride.Rendering;
using ERP.Infrastructure.Ride.Storage;
using ERP.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Recorrido completo de la Fase 8: XML real → parser real → plantilla real → QuestPDF real →
/// storage real (<see cref="LocalFileStorage"/>) → cache real (PostgreSQL vía Testcontainers) →
/// segunda ejecución. La segunda ejecución debe devolver <see cref="RideOutcome.Cached"/> sin
/// volver a invocar el renderer — verificado con un contador real de invocaciones, no una
/// suposición.
/// </summary>
public sealed class RidePipelineStorageAndCacheIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_ride_storage_cache_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly string _fileStorageBasePath = Path.Combine(
        Path.GetTempPath(),
        "ride-storage-cache-tests-" + Guid.NewGuid().ToString("N")
    );

    private sealed class CountingRideRenderer(IRideRenderer inner) : IRideRenderer
    {
        public int CallCount { get; private set; }

        public async Task<byte[]> RenderAsync(
            IRideDocumentLayout layout,
            CancellationToken ct = default
        )
        {
            CallCount++;
            return await inner.RenderAsync(layout, ct);
        }
    }

    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) =>
            taxCode switch
            {
                "VAT" => "2",
                _ => null,
            };
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = NewDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        if (Directory.Exists(_fileStorageBasePath))
            Directory.Delete(_fileStorageBasePath, recursive: true);
    }

    private ErpDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    private static string RealAuthorizedInvoiceXml()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                "1",
                "1",
                "01",
                "001",
                "Av. Amazonas y Naciones Unidas",
                "001",
                "000000123",
                new DateTime(2026, 7, 8)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                "1790012345001",
                "ACME CIA LTDA",
                "ACME",
                "Av. Amazonas y Naciones Unidas",
                null,
                true
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                "05",
                "1710034065",
                "Juan Pérez",
                "Calle Falsa 123",
                "juan@example.com"
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    "SKU-001",
                    "Producto de prueba",
                    2m,
                    10m,
                    0m,
                    20m,
                    [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                ),
            ],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(20m, 0m, 3m, 23m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
            AdditionalInfo: []
        );

        var result = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver()).Build(data);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }

    [Fact]
    public async Task Second_execution_for_the_same_document_returns_cached_without_rendering_again()
    {
        var electronicDocumentId = Guid.NewGuid();
        const string sourceModule = "Sales";
        var sourceEntityId = Guid.NewGuid();
        var xml = RealAuthorizedInvoiceXml();

        var sourceXmlProvider = new Mock<IRideSourceXmlProvider>();
        sourceXmlProvider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<RideSourceXmlLookup>.Success(
                    new RideSourceXmlLookup(
                        RideSourceXmlStatus.Available,
                        xml,
                        electronicDocumentId,
                        RideDocumentType.Invoice
                    )
                )
            );

        var brandingProvider = new Mock<IRideBrandingProvider>();
        brandingProvider
            .Setup(b =>
                b.GetAsync(_tenantId, _companyId, null, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ERP.Domain.Modules.Ride.ValueObjects.RideBranding>.Success(
                    ERP.Domain.Modules.Ride.ValueObjects.RideBranding.Empty()
                )
            );

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FileStorage:BasePath"] = _fileStorageBasePath }
            )
            .Build();
        var fileStorage = new LocalFileStorage(configuration);
        var namingStrategy = new RidePdfStorageNamingStrategy();
        var storageService = new RidePdfStorageService(fileStorage, namingStrategy);
        var countingRenderer = new CountingRideRenderer(
            new QuestPdfRideRenderer(
                RideQrCodeGeneratorTestFactory.Create(),
                RideBarcodeGeneratorTestFactory.Create(),
                new NoOpFileStorage()
            )
        );

        RidePipeline BuildPipeline(ErpDbContext db) =>
            new(
                sourceXmlProvider.Object,
                new RideXmlParserResolver([new InvoiceRideXmlParser()]),
                new RideTemplateResolver([new DefaultInvoiceRideTemplate()]),
                new RideCacheStrategy(
                    new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator())
                ),
                new RideContentHasher(),
                brandingProvider.Object,
                countingRenderer,
                storageService,
                new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator()),
                currentUser.Object
            );

        Result<RideGenerationResultDto> firstResult;
        await using (var db1 = NewDbContext())
        {
            firstResult = await BuildPipeline(db1)
                .ExecuteAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    forceRegenerate: false,
                    CancellationToken.None
                );
        }

        Result<RideGenerationResultDto> secondResult;
        await using (var db2 = NewDbContext())
        {
            secondResult = await BuildPipeline(db2)
                .ExecuteAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    forceRegenerate: false,
                    CancellationToken.None
                );
        }

        firstResult.IsSuccess.Should().BeTrue(firstResult.Error);
        firstResult.Value!.Outcome.Should().Be(RideOutcome.Generated);

        secondResult.IsSuccess.Should().BeTrue(secondResult.Error);
        secondResult.Value!.Outcome.Should().Be(RideOutcome.Cached);
        secondResult.Value.StoragePath.Should().Be(firstResult.Value.StoragePath);

        countingRenderer.CallCount.Should().Be(1);

        await using var verifyDb = NewDbContext();
        var rows = await verifyDb
            .RidePdfDocuments.Where(x =>
                x.TenantId == _tenantId && x.ElectronicDocumentId == electronicDocumentId
            )
            .ToListAsync();
        rows.Should().ContainSingle();
    }

    /// <summary>
    /// H3 (ADR-025 §14/auditoría) — hallazgo real, documentado y aceptado explícitamente por el
    /// usuario durante la Fase 8 ("Verificar solo a nivel de RideCacheStrategy"): la invalidación
    /// de cache por cambio de <c>BrandingVersion</c> está probada y es correcta a nivel de
    /// <see cref="RideCacheStrategy"/> (ver <c>RideBrandingVersionCacheInvalidationTests</c>), pero
    /// <see cref="RidePipeline"/> todavía no obtiene una versión real de branding desde
    /// <c>IRideBrandingProvider</c> — usa la constante neutra <c>"unversioned"</c> para los tres
    /// componentes (plantilla, branding, renderer) mientras ninguno de esos contratos exponga su
    /// propia versión (ver comentario de <see cref="RidePipeline"/>). Este test prueba, de punta a
    /// punta y contra Postgres real, el comportamiento ACTUAL: dos ejecuciones con branding
    /// distinto siguen devolviendo <see cref="RideOutcome.Cached"/> — no es un defecto nuevo de
    /// esta fase, es la deuda ya aceptada en Fase 8, verificada aquí explícitamente en vez de
    /// asumida.
    /// </summary>
    [Fact]
    public async Task H3_pipeline_does_not_yet_invalidate_cache_on_branding_change_gap_accepted_in_phase_8()
    {
        var electronicDocumentId = Guid.NewGuid();
        const string sourceModule = "Sales";
        var sourceEntityId = Guid.NewGuid();
        var xml = RealAuthorizedInvoiceXml();

        var sourceXmlProvider = new Mock<IRideSourceXmlProvider>();
        sourceXmlProvider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<RideSourceXmlLookup>.Success(
                    new RideSourceXmlLookup(
                        RideSourceXmlStatus.Available,
                        xml,
                        electronicDocumentId,
                        RideDocumentType.Invoice
                    )
                )
            );

        var brandingA = ERP.Domain.Modules.Ride.ValueObjects.RideBranding.Create(
            footerText: "Branding original"
        );
        var brandingB = ERP.Domain.Modules.Ride.ValueObjects.RideBranding.Create(
            footerText: "Branding cambiado"
        );
        var brandingProvider = new Mock<IRideBrandingProvider>();
        brandingProvider
            .SetupSequence(b =>
                b.GetAsync(_tenantId, _companyId, null, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ERP.Domain.Modules.Ride.ValueObjects.RideBranding>.Success(brandingA)
            )
            .ReturnsAsync(
                Result<ERP.Domain.Modules.Ride.ValueObjects.RideBranding>.Success(brandingB)
            );

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FileStorage:BasePath"] = _fileStorageBasePath }
            )
            .Build();
        var fileStorage = new LocalFileStorage(configuration);
        var storageService = new RidePdfStorageService(
            fileStorage,
            new RidePdfStorageNamingStrategy()
        );
        var countingRenderer = new CountingRideRenderer(
            new QuestPdfRideRenderer(
                RideQrCodeGeneratorTestFactory.Create(),
                RideBarcodeGeneratorTestFactory.Create(),
                new NoOpFileStorage()
            )
        );

        RidePipeline BuildPipeline(ErpDbContext db) =>
            new(
                sourceXmlProvider.Object,
                new RideXmlParserResolver([new InvoiceRideXmlParser()]),
                new RideTemplateResolver([new DefaultInvoiceRideTemplate()]),
                new RideCacheStrategy(
                    new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator())
                ),
                new RideContentHasher(),
                brandingProvider.Object,
                countingRenderer,
                storageService,
                new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator()),
                currentUser.Object
            );

        Result<RideGenerationResultDto> firstResult;
        await using (var db1 = NewDbContext())
            firstResult = await BuildPipeline(db1)
                .ExecuteAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    forceRegenerate: false,
                    CancellationToken.None
                );

        Result<RideGenerationResultDto> secondResult;
        await using (var db2 = NewDbContext())
            secondResult = await BuildPipeline(db2)
                .ExecuteAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    forceRegenerate: false,
                    CancellationToken.None
                );

        firstResult.Value!.Outcome.Should().Be(RideOutcome.Generated);
        // Comportamiento actual real: NO invalida — brandingB nunca cambió NeutralBrandingVersion.
        secondResult.Value!.Outcome.Should().Be(RideOutcome.Cached);
        countingRenderer
            .CallCount.Should()
            .Be(
                1,
                "documenta el hueco real H3: el renderer no se vuelve a invocar aunque el branding cambió, "
                    + "porque RidePipeline todavía no thread-ea la versión real de IRideBrandingProvider al cache key"
            );
    }

    /// <summary>Confirma actualización real de metadata (GeneratedAtUtc) al forzar una regeneración explícita.</summary>
    [Fact]
    public async Task Force_regenerate_produces_updated_metadata_with_a_later_generatedAt_timestamp()
    {
        var electronicDocumentId = Guid.NewGuid();
        const string sourceModule = "Sales";
        var sourceEntityId = Guid.NewGuid();
        var xml = RealAuthorizedInvoiceXml();

        var sourceXmlProvider = new Mock<IRideSourceXmlProvider>();
        sourceXmlProvider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<RideSourceXmlLookup>.Success(
                    new RideSourceXmlLookup(
                        RideSourceXmlStatus.Available,
                        xml,
                        electronicDocumentId,
                        RideDocumentType.Invoice
                    )
                )
            );

        var brandingProvider = new Mock<IRideBrandingProvider>();
        brandingProvider
            .Setup(b =>
                b.GetAsync(_tenantId, _companyId, null, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ERP.Domain.Modules.Ride.ValueObjects.RideBranding>.Success(
                    ERP.Domain.Modules.Ride.ValueObjects.RideBranding.Empty()
                )
            );

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FileStorage:BasePath"] = _fileStorageBasePath }
            )
            .Build();
        var fileStorage = new LocalFileStorage(configuration);
        var storageService = new RidePdfStorageService(
            fileStorage,
            new RidePdfStorageNamingStrategy()
        );

        RidePipeline BuildPipeline(ErpDbContext db) =>
            new(
                sourceXmlProvider.Object,
                new RideXmlParserResolver([new InvoiceRideXmlParser()]),
                new RideTemplateResolver([new DefaultInvoiceRideTemplate()]),
                new RideCacheStrategy(
                    new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator())
                ),
                new RideContentHasher(),
                brandingProvider.Object,
                new QuestPdfRideRenderer(
                    RideQrCodeGeneratorTestFactory.Create(),
                    RideBarcodeGeneratorTestFactory.Create(),
                    new NoOpFileStorage()
                ),
                storageService,
                new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator()),
                currentUser.Object
            );

        Result<RideGenerationResultDto> firstResult;
        await using (var db1 = NewDbContext())
            firstResult = await BuildPipeline(db1)
                .ExecuteAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    forceRegenerate: false,
                    CancellationToken.None
                );

        await Task.Delay(50);

        Result<RideGenerationResultDto> regeneratedResult;
        await using (var db2 = NewDbContext())
            regeneratedResult = await BuildPipeline(db2)
                .ExecuteAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    forceRegenerate: true,
                    CancellationToken.None
                );

        firstResult.Value!.Outcome.Should().Be(RideOutcome.Generated);
        regeneratedResult.Value!.Outcome.Should().Be(RideOutcome.Generated);
        regeneratedResult.Value.Metadata.Should().NotBeNull();
        regeneratedResult.Value.Metadata!.WasCached.Should().BeFalse();
        regeneratedResult
            .Value.Metadata.GeneratedAtUtc.Should()
            .BeAfter(
                firstResult.Value.Metadata!.GeneratedAtUtc,
                "forceRegenerate debe producir un GeneratedAtUtc real y posterior, no reutilizar la metadata anterior"
            );

        await using var verifyDb = NewDbContext();
        var rows = await verifyDb
            .RidePdfDocuments.Where(x =>
                x.TenantId == _tenantId && x.ElectronicDocumentId == electronicDocumentId
            )
            .ToListAsync();
        rows.Should()
            .ContainSingle(
                "una regeneración sobre el mismo fingerprint actualiza la fila existente, no crea una nueva"
            );
    }

    /// <summary>
    /// Caso obligatorio "Failed": fuerza un fallo real de infraestructura (sin dobles de prueba en
    /// storage) — pre-crea, en el filesystem real, un DIRECTORIO exactamente en la ruta donde
    /// <see cref="LocalFileStorage"/> intentará escribir el PDF. <c>FileStream(..., FileMode.Create)</c>
    /// sobre una ruta que ya es un directorio lanza <see cref="UnauthorizedAccessException"/> real
    /// del sistema operativo — no un <see cref="IOException"/> (que <see cref="RidePdfStorageService"/>
    /// ya trata como éxito idempotente para H4). El pipeline debe convertir esa excepción real en
    /// <see cref="RideOutcome.Failed"/>, nunca dejarla escapar sin controlar.
    /// </summary>
    [Fact]
    public async Task Real_storage_failure_is_converted_to_outcome_failed_never_an_unhandled_exception()
    {
        var electronicDocumentId = Guid.NewGuid();
        const string sourceModule = "Sales";
        var sourceEntityId = Guid.NewGuid();
        var xml = RealAuthorizedInvoiceXml();

        var namingStrategy = new RidePdfStorageNamingStrategy();
        var relativePath = namingStrategy.BuildRelativePath(
            _tenantId,
            RideDocumentType.Invoice,
            electronicDocumentId,
            "unversioned"
        );
        var forbiddenFullPath = Path.GetFullPath(Path.Combine(_fileStorageBasePath, relativePath));
        Directory.CreateDirectory(forbiddenFullPath);

        var sourceXmlProvider = new Mock<IRideSourceXmlProvider>();
        sourceXmlProvider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    _tenantId,
                    _companyId,
                    sourceModule,
                    sourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<RideSourceXmlLookup>.Success(
                    new RideSourceXmlLookup(
                        RideSourceXmlStatus.Available,
                        xml,
                        electronicDocumentId,
                        RideDocumentType.Invoice
                    )
                )
            );

        var brandingProvider = new Mock<IRideBrandingProvider>();
        brandingProvider
            .Setup(b =>
                b.GetAsync(_tenantId, _companyId, null, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ERP.Domain.Modules.Ride.ValueObjects.RideBranding>.Success(
                    ERP.Domain.Modules.Ride.ValueObjects.RideBranding.Empty()
                )
            );

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FileStorage:BasePath"] = _fileStorageBasePath }
            )
            .Build();
        var fileStorage = new LocalFileStorage(configuration);
        var storageService = new RidePdfStorageService(fileStorage, namingStrategy);

        await using var db = NewDbContext();
        var pipeline = new RidePipeline(
            sourceXmlProvider.Object,
            new RideXmlParserResolver([new InvoiceRideXmlParser()]),
            new RideTemplateResolver([new DefaultInvoiceRideTemplate()]),
            new RideCacheStrategy(
                new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator())
            ),
            new RideContentHasher(),
            brandingProvider.Object,
            new QuestPdfRideRenderer(
                RideQrCodeGeneratorTestFactory.Create(),
                RideBarcodeGeneratorTestFactory.Create(),
                new NoOpFileStorage()
            ),
            storageService,
            new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator()),
            currentUser.Object
        );

        var act = () =>
            pipeline.ExecuteAsync(
                _tenantId,
                _companyId,
                sourceModule,
                sourceEntityId,
                forceRegenerate: false,
                CancellationToken.None
            );

        var result = await act.Should()
            .NotThrowAsync(
                "una falla real de storage nunca debe escapar como excepción no controlada"
            );
        result.Subject.IsSuccess.Should().BeTrue();
        result.Subject.Value!.Outcome.Should().Be(RideOutcome.Failed);
        result.Subject.Value.ReasonCode.Should().StartWith("render_pipeline_error:");
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
