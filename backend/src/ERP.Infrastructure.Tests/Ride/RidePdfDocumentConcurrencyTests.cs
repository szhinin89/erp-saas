using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;
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
/// H4 (ADR-025 §14, Fase 8): dos <see cref="RidePipeline.ExecuteAsync"/> concurrentes para el
/// MISMO documento, cada uno con su propio <see cref="ErpDbContext"/> (simula dos requests HTTP
/// reales, no dos hilos compartiendo el mismo contexto). Toda la sincronización descansa
/// exclusivamente en <c>uq_ride_pdf_document_fingerprint</c> + la recuperación de
/// <see cref="RidePdfDocumentRepository.SaveChangesAsync"/> — sin locks, mutex, semáforos ni
/// cache distribuido.
///
/// Nota honesta (declarada en la auditoría): con huella y render 100% determinísticos, ambos
/// procesos pueden efectivamente renderizar (ninguna sincronización se lo impide sin los
/// mecanismos prohibidos) — lo que sí está garantizado es que el resultado final en base de
/// datos y en storage es único, y que ninguno de los dos ve una excepción.
/// </summary>
public sealed class RidePdfDocumentConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_ride_concurrency_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly string _fileStorageBasePath =
        Path.Combine(Path.GetTempPath(), "ride-concurrency-tests-" + Guid.NewGuid().ToString("N"));

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) => taxCode switch { "VAT" => "2", _ => null };
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
        var options = new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        return new ErpDbContext(options, new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    private static RidePipeline BuildPipeline(
        ErpDbContext db, Mock<IRideSourceXmlProvider> sourceXmlProvider, Mock<ICurrentUser> currentUser,
        Mock<IRideBrandingProvider> brandingProvider, RidePdfStorageService storageService)
        => new(
            sourceXmlProvider.Object,
            new RideXmlParserResolver([new InvoiceRideXmlParser()]),
            new RideTemplateResolver([new DefaultInvoiceRideTemplate()]),
            new RideCacheStrategy(new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator())),
            new RideContentHasher(),
            brandingProvider.Object,
            new QuestPdfRideRenderer(RideQrCodeGeneratorTestFactory.Create(), RideBarcodeGeneratorTestFactory.Create(), new NoOpFileStorage()),
            storageService,
            new RidePdfDocumentRepository(db, new PostgresDatabaseExceptionTranslator()),
            currentUser.Object);

    [Fact]
    public async Task Two_concurrent_requests_for_the_same_document_produce_a_single_final_row_and_no_exceptions()
    {
        var electronicDocumentId = Guid.NewGuid();
        const string sourceModule = "Sales";
        var sourceEntityId = Guid.NewGuid();

        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                "1", "1", "01", "001", "Av. Amazonas y Naciones Unidas", "001", "000000321", new DateTime(2026, 7, 8)),
            Issuer: new ElectronicDocumentIssuerData(
                "1790012345001", "ACME CIA LTDA", "ACME", "Av. Amazonas y Naciones Unidas", null, true),
            Counterparty: new ElectronicDocumentCounterpartyData(
                "05", "1710034065", "Juan Pérez", "Calle Falsa 123", "juan@example.com"),
            Details: [new ElectronicDocumentDetailLine(
                "SKU-001", "Producto de prueba", 1m, 15m, 0m, 15m,
                [new ElectronicDocumentDetailTax("VAT", "2", 15m, 15m, 2.25m)])],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 15m, 2.25m)],
            Totals: new ElectronicDocumentTotals(15m, 0m, 2.25m, 17.25m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 17.25m, null, null)],
            AdditionalInfo: []);
        var xmlResult = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver()).Build(data);
        xmlResult.IsSuccess.Should().BeTrue(xmlResult.Error);
        var xml = xmlResult.Value!.Xml;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _fileStorageBasePath })
            .Build();
        var storageService = new RidePdfStorageService(new LocalFileStorage(configuration), new RidePdfStorageNamingStrategy());

        (RidePipeline pipeline, ErpDbContext db) BuildRequest()
        {
            var db = NewDbContext();
            var sourceXmlProvider = new Mock<IRideSourceXmlProvider>();
            sourceXmlProvider
                .Setup(p => p.GetAuthorizedXmlAsync(_tenantId, _companyId, sourceModule, sourceEntityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<RideSourceXmlLookup>.Success(new RideSourceXmlLookup(
                    RideSourceXmlStatus.Available, xml, electronicDocumentId, RideDocumentType.Invoice)));
            var brandingProvider = new Mock<IRideBrandingProvider>();
            brandingProvider
                .Setup(b => b.GetAsync(_tenantId, _companyId, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));
            var currentUser = new Mock<ICurrentUser>();
            currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

            return (BuildPipeline(db, sourceXmlProvider, currentUser, brandingProvider, storageService), db);
        }

        var (pipelineA, dbA) = BuildRequest();
        var (pipelineB, dbB) = BuildRequest();

        var taskA = pipelineA.ExecuteAsync(_tenantId, _companyId, sourceModule, sourceEntityId, forceRegenerate: false, CancellationToken.None);
        var taskB = pipelineB.ExecuteAsync(_tenantId, _companyId, sourceModule, sourceEntityId, forceRegenerate: false, CancellationToken.None);

        var act = () => Task.WhenAll(taskA, taskB);
        await act.Should().NotThrowAsync();

        await dbA.DisposeAsync();
        await dbB.DisposeAsync();

        var resultA = await taskA;
        var resultB = await taskB;

        resultA.IsSuccess.Should().BeTrue(resultA.Error);
        resultB.IsSuccess.Should().BeTrue(resultB.Error);
        resultA.Value!.Outcome.Should().BeOneOf(RideOutcome.Generated, RideOutcome.Cached);
        resultB.Value!.Outcome.Should().BeOneOf(RideOutcome.Generated, RideOutcome.Cached);

        // Determinismo de storage (misma huella ⇒ misma ruta) garantiza que ambos requests
        // apuntan al mismo archivo aunque hayan "ganado" carreras distintas.
        resultA.Value.StoragePath.Should().Be(resultB.Value.StoragePath);

        await using var verifyDb = NewDbContext();
        var rows = await verifyDb.RidePdfDocuments
            .Where(x => x.TenantId == _tenantId && x.ElectronicDocumentId == electronicDocumentId)
            .ToListAsync();
        rows.Should().ContainSingle("el índice único + la recuperación de SaveChangesAsync deben dejar una sola fila final");
        rows[0].State.Should().Be(RidePdfState.Generated);
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
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
