using ERP.Application.Common;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Storage;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// End-to-end del pipeline con dobles de prueba (Fase 5, ADR-025) — ninguno de los 3 casos
/// mínimos exigidos conoce Factura, Nota de Crédito ni ningún otro comprobante concreto.
/// </summary>
public sealed class RidePipelineTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ElectronicDocumentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string SourceModule = "Sales";
    private static readonly Guid SourceEntityId = Guid.NewGuid();
    private const string AuthorizedXml = "<factura>contenido de prueba</factura>";

    private sealed class Fixture
    {
        public Mock<IRideSourceXmlProvider> SourceXmlProvider { get; } = new();
        public Mock<IRideXmlParserResolver> ParserResolver { get; } = new();
        public Mock<IRideTemplateResolver> TemplateResolver { get; } = new();
        public Mock<IRideCacheStrategy> CacheStrategy { get; } = new();
        public Mock<IRideContentHasher> ContentHasher { get; } = new();
        public Mock<IRideBrandingProvider> BrandingProvider { get; } = new();
        public Mock<IRideRenderer> Renderer { get; } = new();
        public Mock<IRidePdfStorageService> StorageService { get; } = new();
        public Mock<IRidePdfDocumentRepository> Repository { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();

        public Fixture()
        {
            CurrentUser.SetupGet(u => u.UserId).Returns(UserId);
        }

        public RidePipeline BuildPipeline() => new(
            SourceXmlProvider.Object, ParserResolver.Object, TemplateResolver.Object,
            CacheStrategy.Object, ContentHasher.Object, BrandingProvider.Object,
            Renderer.Object, StorageService.Object, Repository.Object, CurrentUser.Object);

        public void SetupAvailableSource(RideDocumentType documentType = RideDocumentType.Invoice) =>
            SourceXmlProvider
                .Setup(p => p.GetAuthorizedXmlAsync(TenantId, CompanyId, SourceModule, SourceEntityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<RideSourceXmlLookup>.Success(new RideSourceXmlLookup(
                    RideSourceXmlStatus.Available, AuthorizedXml, ElectronicDocumentId, documentType)));
    }

    [Fact]
    public async Task Source_pending_returns_pending_source_never_throws()
    {
        var fixture = new Fixture();
        fixture.SourceXmlProvider
            .Setup(p => p.GetAuthorizedXmlAsync(TenantId, CompanyId, SourceModule, SourceEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RideSourceXmlLookup>.Success(new RideSourceXmlLookup(
                RideSourceXmlStatus.PendingSource, null, ElectronicDocumentId, RideDocumentType.Invoice)));

        var act = () => fixture.BuildPipeline().ExecuteAsync(
            TenantId, CompanyId, SourceModule, SourceEntityId, forceRegenerate: false, CancellationToken.None);

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeTrue();
        result.Subject.Value!.Outcome.Should().Be(RideOutcome.PendingSource);
    }

    [Fact]
    public async Task Source_not_applicable_returns_not_applicable()
    {
        var fixture = new Fixture();
        fixture.SourceXmlProvider
            .Setup(p => p.GetAuthorizedXmlAsync(TenantId, CompanyId, SourceModule, SourceEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RideSourceXmlLookup>.Success(new RideSourceXmlLookup(
                RideSourceXmlStatus.NotApplicable, null, ElectronicDocumentId, RideDocumentType.Invoice)));

        var result = await fixture.BuildPipeline().ExecuteAsync(
            TenantId, CompanyId, SourceModule, SourceEntityId, forceRegenerate: false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }

    [Fact]
    public async Task Case1_no_parser_registered_returns_failed_with_explicit_reason_code_never_throws()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture.ParserResolver.Setup(r => r.Resolve(It.IsAny<RideDocumentType>())).Returns((IRideXmlParser?)null);

        var act = () => fixture.BuildPipeline().ExecuteAsync(
            TenantId, CompanyId, SourceModule, SourceEntityId, forceRegenerate: false, CancellationToken.None);

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeTrue();
        result.Subject.Value!.Outcome.Should().Be(RideOutcome.Failed);
        result.Subject.Value.ReasonCode.Should().Be("parser_not_registered");
        fixture.TemplateResolver.Verify(r => r.Resolve(It.IsAny<RideTemplateSelector>()), Times.Never);
    }

    [Fact]
    public async Task Case2_parser_registered_but_no_template_returns_failed()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture.ParserResolver.Setup(r => r.Resolve(RideDocumentType.Invoice))
            .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
        fixture.TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>())).Returns((IRideTemplate?)null);

        var result = await fixture.BuildPipeline().ExecuteAsync(
            TenantId, CompanyId, SourceModule, SourceEntityId, forceRegenerate: false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.Failed);
        result.Value.ReasonCode.Should().Be("template_not_registered");
        fixture.CacheStrategy.Verify(
            c => c.TryGetCachedAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RideContentHash>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Case3_parser_and_template_registered_completes_the_full_orchestration_on_cache_miss()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture.ParserResolver.Setup(r => r.Resolve(RideDocumentType.Invoice))
            .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
        fixture.TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
            .Returns(new FakeRideTemplate(RideDocumentType.Invoice));
        fixture.ContentHasher.Setup(h => h.Compute(AuthorizedXml)).Returns(RideContentHash.Create(new string('a', 64)));
        fixture.CacheStrategy
            .Setup(c => c.TryGetCachedAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RideContentHash>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RidePdfMetadataDto?>.Success(null));
        fixture.BrandingProvider
            .Setup(b => b.GetAsync(TenantId, CompanyId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));
        fixture.Renderer.Setup(r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        fixture.StorageService
            .Setup(s => s.StoreAsync(TenantId, RideDocumentType.Invoice, ElectronicDocumentId, It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("ride/path/v1.pdf"));
        fixture.Repository
            .Setup(r => r.GetByFingerprintAsync(
                TenantId, ElectronicDocumentId, It.IsAny<RideContentHash>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RidePdfDocument?)null);

        var result = await fixture.BuildPipeline().ExecuteAsync(
            TenantId, CompanyId, SourceModule, SourceEntityId, forceRegenerate: false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.Generated);
        result.Value.StoragePath.Should().Be("ride/path/v1.pdf");
        result.Value.Metadata.Should().NotBeNull();
        result.Value.Metadata!.WasCached.Should().BeFalse();
        fixture.Repository.Verify(r => r.AddAsync(It.IsAny<RidePdfDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cache_hit_short_circuits_without_rendering_or_storing()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture.ParserResolver.Setup(r => r.Resolve(RideDocumentType.Invoice))
            .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
        fixture.TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
            .Returns(new FakeRideTemplate(RideDocumentType.Invoice));

        var cachedMetadata = new RidePdfMetadataDto("Invoice", "unversioned", "unversioned", "unversioned", new string('a', 64), DateTime.UtcNow, WasCached: true);
        fixture.CacheStrategy
            .Setup(c => c.TryGetCachedAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RideContentHash>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RidePdfMetadataDto?>.Success(cachedMetadata));

        var cachedDocument = RidePdfDocument.Create(
            TenantId, CompanyId, ElectronicDocumentId, RideDocumentType.Invoice, RideContentHash.Create(new string('a', 64)),
            "Invoice", "unversioned", "unversioned", "unversioned", RidePipeline.RideSpecificationVersion, UserId);
        cachedDocument.MarkGenerated("ride/path/cached.pdf", DateTime.UtcNow, UserId);
        fixture.Repository
            .Setup(r => r.GetByFingerprintAsync(
                TenantId, ElectronicDocumentId, It.IsAny<RideContentHash>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDocument);

        var result = await fixture.BuildPipeline().ExecuteAsync(
            TenantId, CompanyId, SourceModule, SourceEntityId, forceRegenerate: false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.Cached);
        result.Value.StoragePath.Should().Be("ride/path/cached.pdf");
        fixture.Renderer.Verify(r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.StorageService.Verify(
            s => s.StoreAsync(It.IsAny<Guid>(), It.IsAny<RideDocumentType>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Force_regenerate_bypasses_cache_hit_and_renders_again()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture.ParserResolver.Setup(r => r.Resolve(RideDocumentType.Invoice))
            .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
        fixture.TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
            .Returns(new FakeRideTemplate(RideDocumentType.Invoice));
        fixture.ContentHasher.Setup(h => h.Compute(AuthorizedXml)).Returns(RideContentHash.Create(new string('a', 64)));

        var cachedMetadata = new RidePdfMetadataDto("Invoice", "unversioned", "unversioned", "unversioned", new string('a', 64), DateTime.UtcNow, WasCached: true);
        fixture.CacheStrategy
            .Setup(c => c.TryGetCachedAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RideContentHash>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RidePdfMetadataDto?>.Success(cachedMetadata));
        fixture.BrandingProvider
            .Setup(b => b.GetAsync(TenantId, CompanyId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));
        fixture.Renderer.Setup(r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([9, 9, 9]);
        fixture.StorageService
            .Setup(s => s.StoreAsync(TenantId, RideDocumentType.Invoice, ElectronicDocumentId, It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("ride/path/regenerated.pdf"));

        var existingDocument = RidePdfDocument.Create(
            TenantId, CompanyId, ElectronicDocumentId, RideDocumentType.Invoice, RideContentHash.Create(new string('a', 64)),
            "Invoice", "unversioned", "unversioned", "unversioned", RidePipeline.RideSpecificationVersion, UserId);
        existingDocument.MarkGenerated("ride/path/old.pdf", DateTime.UtcNow.AddDays(-1), UserId);
        fixture.Repository
            .Setup(r => r.GetByFingerprintAsync(
                TenantId, ElectronicDocumentId, It.IsAny<RideContentHash>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDocument);

        var result = await fixture.BuildPipeline().ExecuteAsync(
            TenantId, CompanyId, SourceModule, SourceEntityId, forceRegenerate: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.Generated);
        result.Value.StoragePath.Should().Be("ride/path/regenerated.pdf");
        fixture.Renderer.Verify(r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Repository.Verify(r => r.AddAsync(It.IsAny<RidePdfDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Source_infrastructure_failure_propagates_as_result_failure_not_success_with_failed_outcome()
    {
        var fixture = new Fixture();
        fixture.SourceXmlProvider
            .Setup(p => p.GetAuthorizedXmlAsync(TenantId, CompanyId, SourceModule, SourceEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RideSourceXmlLookup>.Failure("El servicio de documentos electrónicos no respondió."));

        var result = await fixture.BuildPipeline().ExecuteAsync(
            TenantId, CompanyId, SourceModule, SourceEntityId, forceRegenerate: false, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
