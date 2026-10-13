using ERP.Domain.Modules.Company.Interfaces;
using RideCompany = ERP.Domain.Modules.Company.Entities.Company;
using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Storage;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Configuration.Entities;
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
        public Mock<ICompanyPrecisionPolicyRepository> PrecisionPolicies { get; } = new();
        public Mock<ICompanyRepository> Companies { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();

        public Fixture()
        {
            Companies.Setup(r => r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RideCompany { Id = CompanyId, TenantId = TenantId, Timezone = "America/Guayaquil" });
            ContentHasher.Setup(h => h.Compute(It.IsAny<string>())).Returns((string text) => new RideContentHasher().Compute(text));
            CurrentUser.SetupGet(u => u.UserId).Returns(UserId);
            PrecisionPolicies
                .Setup(p => p.FindAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    CompanyPrecisionPolicy.CreateStandardCommercial(TenantId, CompanyId, UserId)
                );
        }

        public RidePipeline BuildPipeline() =>
            new(
                SourceXmlProvider.Object,
                ParserResolver.Object,
                TemplateResolver.Object,
                CacheStrategy.Object,
                ContentHasher.Object,
                BrandingProvider.Object,
                Renderer.Object,
                StorageService.Object,
                Repository.Object,
                CurrentUser.Object,
                PrecisionPolicies.Object,
                Companies.Object
            );

        public void SetupAvailableSource(
            RideDocumentType documentType = RideDocumentType.Invoice,
            DateTime? authorizationDateUtc = null
        ) =>
            SourceXmlProvider
                .Setup(p =>
                    p.GetAuthorizedXmlAsync(
                        TenantId,
                        CompanyId,
                        SourceModule,
                        SourceEntityId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<RideSourceXmlLookup>.Success(
                        new RideSourceXmlLookup(
                            RideSourceXmlStatus.Available,
                            AuthorizedXml,
                            ElectronicDocumentId,
                            documentType,
                            authorizationDateUtc
                        )
                    )
                );

        /// <summary>Cache miss completo; devuelve el layout que llega al renderer y la versión de plantilla.</summary>
        public async Task<(InvoiceRideDocumentLayout Layout, string TemplateVersion)> RunCacheMissAsync()
        {
            InvoiceRideDocumentLayout? captured = null;
            ParserResolver
                .Setup(r => r.Resolve(RideDocumentType.Invoice))
                .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
            TemplateResolver
                .Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
                .Returns(new DefaultInvoiceRideTemplate());
            CacheStrategy
                .Setup(c =>
                    c.TryGetCachedAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<RideContentHash>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result<RidePdfMetadataDto?>.Success(null));
            BrandingProvider
                .Setup(b => b.GetAsync(TenantId, CompanyId, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));
            Renderer
                .Setup(r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()))
                .Callback<IRideDocumentLayout, CancellationToken>((l, _) => captured = (InvoiceRideDocumentLayout)l)
                .ReturnsAsync([1, 2, 3]);
            StorageService
                .Setup(s =>
                    s.StoreAsync(
                        TenantId,
                        RideDocumentType.Invoice,
                        ElectronicDocumentId,
                        It.IsAny<string>(),
                        It.IsAny<byte[]>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result<string>.Success("ride/path/v1.pdf"));

            var result = await BuildPipeline()
                .ExecuteAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    forceRegenerate: false,
                    CancellationToken.None
                );

            result.IsSuccess.Should().BeTrue();
            captured.Should().NotBeNull();
            return (captured!, result.Value!.Metadata!.TemplateVersion);
        }

        public void SetupCompanyTimezone(string timezone) =>
            Companies
                .Setup(r => r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RideCompany { Id = CompanyId, TenantId = TenantId, Timezone = timezone });
    }

    [Fact]
    public async Task Authorization_date_from_metadata_is_converted_from_utc_to_company_timezone()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource(
            authorizationDateUtc: new DateTime(2026, 10, 13, 16, 27, 19, DateTimeKind.Utc)
        );

        var (layout, _) = await fixture.RunCacheMissAsync();

        layout.Header.AuthorizationDate.Should().Be(new DateTime(2026, 10, 13, 11, 27, 19));
        layout.Header.AuthorizationDate!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
        layout
            .Header.AuthorizationDate.Value.ToString("dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
            .Should()
            .Be("13/10/2026 11:27:19");
    }

    [Fact]
    public async Task Null_authorization_metadata_yields_null_date_never_an_invented_one()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource(authorizationDateUtc: null);

        var (layout, _) = await fixture.RunCacheMissAsync();

        // El fake parser trae una fecha propia: la metadata del ElectronicDocument es la SSOT.
        layout.Header.AuthorizationDate.Should().BeNull();
    }

    [Fact]
    public async Task Missing_company_fails_closed_without_rendering()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource(
            authorizationDateUtc: new DateTime(2026, 10, 13, 16, 27, 19, DateTimeKind.Utc)
        );
        fixture
            .ParserResolver.Setup(r => r.Resolve(RideDocumentType.Invoice))
            .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
        fixture
            .TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
            .Returns(new DefaultInvoiceRideTemplate());
        fixture
            .Companies.Setup(r => r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RideCompany?)null);

        var result = await fixture
            .BuildPipeline()
            .ExecuteAsync(TenantId, CompanyId, SourceModule, SourceEntityId, false, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        fixture.Renderer.Verify(
            r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Template_version_fingerprint_changes_with_authorization_date_and_timezone_and_excludes_legacy_pdfs()
    {
        var authorizedAt = new DateTime(2026, 10, 13, 16, 27, 19, DateTimeKind.Utc);

        async Task<string> VersionFor(DateTime? utc, string timezone)
        {
            var fixture = new Fixture();
            fixture.SetupCompanyTimezone(timezone);
            fixture.SetupAvailableSource(authorizationDateUtc: utc);
            return (await fixture.RunCacheMissAsync()).TemplateVersion;
        }

        var baseline = await VersionFor(authorizedAt, "America/Guayaquil");
        var sameInputs = await VersionFor(authorizedAt, "America/Guayaquil");
        var otherDate = await VersionFor(authorizedAt.AddSeconds(1), "America/Guayaquil");
        var withoutDate = await VersionFor(null, "America/Guayaquil");
        var otherZone = await VersionFor(authorizedAt, "America/Bogota");

        sameInputs.Should().Be(baseline, "el cache debe seguir siendo determinista");
        new[] { otherDate, withoutDate, otherZone }.Should().NotContain(baseline);
        otherZone.Should().NotBe(otherDate);
        // PDFs previos al fix se persistieron con la versión "precision-1-q4-p2" sin sufijo.
        baseline.Should().NotBe("precision-1-q4-p2").And.StartWith("precision-1-q4-p2-a1-");
        baseline.Length.Should().BeLessThanOrEqualTo(RidePdfDocument.VersionMaxLen);
    }

    [Fact]
    public async Task Missing_company_policy_fails_without_rendering_or_using_cache()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture
            .ParserResolver.Setup(r => r.Resolve(RideDocumentType.Invoice))
            .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
        fixture
            .TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
            .Returns(new DefaultInvoiceRideTemplate());
        fixture
            .PrecisionPolicies.Setup(p =>
                p.FindAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyPrecisionPolicy?)null);
        var result = await fixture
            .BuildPipeline()
            .ExecuteAsync(
                TenantId,
                CompanyId,
                SourceModule,
                SourceEntityId,
                false,
                CancellationToken.None
            );
        result.IsSuccess.Should().BeFalse();
        fixture.Renderer.Invocations.Should().BeEmpty();
        fixture.CacheStrategy.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Source_pending_returns_pending_source_never_throws()
    {
        var fixture = new Fixture();
        fixture
            .SourceXmlProvider.Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<RideSourceXmlLookup>.Success(
                    new RideSourceXmlLookup(
                        RideSourceXmlStatus.PendingSource,
                        null,
                        ElectronicDocumentId,
                        RideDocumentType.Invoice
                    )
                )
            );

        var act = () =>
            fixture
                .BuildPipeline()
                .ExecuteAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    forceRegenerate: false,
                    CancellationToken.None
                );

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeTrue();
        result.Subject.Value!.Outcome.Should().Be(RideOutcome.PendingSource);
    }

    [Fact]
    public async Task Source_not_applicable_returns_not_applicable()
    {
        var fixture = new Fixture();
        fixture
            .SourceXmlProvider.Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<RideSourceXmlLookup>.Success(
                    new RideSourceXmlLookup(
                        RideSourceXmlStatus.NotApplicable,
                        null,
                        ElectronicDocumentId,
                        RideDocumentType.Invoice
                    )
                )
            );

        var result = await fixture
            .BuildPipeline()
            .ExecuteAsync(
                TenantId,
                CompanyId,
                SourceModule,
                SourceEntityId,
                forceRegenerate: false,
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.NotApplicable);
    }

    [Fact]
    public async Task Case1_no_parser_registered_returns_failed_with_explicit_reason_code_never_throws()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture
            .ParserResolver.Setup(r => r.Resolve(It.IsAny<RideDocumentType>()))
            .Returns((IRideXmlParser?)null);

        var act = () =>
            fixture
                .BuildPipeline()
                .ExecuteAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    forceRegenerate: false,
                    CancellationToken.None
                );

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeTrue();
        result.Subject.Value!.Outcome.Should().Be(RideOutcome.Failed);
        result.Subject.Value.ReasonCode.Should().Be("parser_not_registered");
        fixture.TemplateResolver.Verify(
            r => r.Resolve(It.IsAny<RideTemplateSelector>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Case2_parser_registered_but_no_template_returns_failed()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture
            .ParserResolver.Setup(r => r.Resolve(RideDocumentType.Invoice))
            .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
        fixture
            .TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
            .Returns((IRideTemplate?)null);

        var result = await fixture
            .BuildPipeline()
            .ExecuteAsync(
                TenantId,
                CompanyId,
                SourceModule,
                SourceEntityId,
                forceRegenerate: false,
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.Failed);
        result.Value.ReasonCode.Should().Be("template_not_registered");
        fixture.CacheStrategy.Verify(
            c =>
                c.TryGetCachedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<RideContentHash>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Theory]
    [InlineData(false, false, 4, 2)]
    [InlineData(false, true, 6, 4)]
    [InlineData(true, false, 4, 2)]
    [InlineData(true, true, 6, 4)]
    public async Task Case3_parser_and_template_registered_completes_the_full_orchestration_on_cache_miss(
        bool creditNote,
        bool highPrecision,
        int quantityDecimals,
        int priceDecimals
    )
    {
        var fixture = new Fixture();
        var documentType = creditNote ? RideDocumentType.CreditNote : RideDocumentType.Invoice;
        fixture.SetupAvailableSource(documentType);
        fixture
            .PrecisionPolicies.Setup(p =>
                p.FindAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                highPrecision
                    ? CompanyPrecisionPolicy.CreateHighPrecision(TenantId, CompanyId, UserId)
                    : CompanyPrecisionPolicy.CreateStandardCommercial(TenantId, CompanyId, UserId)
            );
        fixture
            .ParserResolver.Setup(r => r.Resolve(documentType))
            .Returns(new FakeRideXmlParser(documentType));
        fixture
            .TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
            .Returns(creditNote ? new CreditNoteRideTemplate() : new DefaultInvoiceRideTemplate());
        fixture
            .ContentHasher.Setup(h => h.Compute(AuthorizedXml))
            .Returns(RideContentHash.Create(new string('a', 64)));
        fixture
            .CacheStrategy.Setup(c =>
                c.TryGetCachedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<RideContentHash>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RidePdfMetadataDto?>.Success(null));
        fixture
            .BrandingProvider.Setup(b =>
                b.GetAsync(TenantId, CompanyId, null, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));
        fixture
            .Renderer.Setup(r =>
                r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([1, 2, 3]);
        fixture
            .StorageService.Setup(s =>
                s.StoreAsync(
                    TenantId,
                    documentType,
                    ElectronicDocumentId,
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<string>.Success("ride/path/v1.pdf"));
        fixture
            .Repository.Setup(r =>
                r.GetByFingerprintAsync(
                    TenantId,
                    ElectronicDocumentId,
                    It.IsAny<RideContentHash>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((RidePdfDocument?)null);

        var result = await fixture
            .BuildPipeline()
            .ExecuteAsync(
                TenantId,
                CompanyId,
                SourceModule,
                SourceEntityId,
                forceRegenerate: false,
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.Generated);
        result.Value.StoragePath.Should().Be("ride/path/v1.pdf");
        result.Value.Metadata.Should().NotBeNull();
        result.Value.Metadata!.WasCached.Should().BeFalse();
        var version = $"precision-1-q{quantityDecimals}-p{priceDecimals}";
        result.Value.Metadata.TemplateVersion.Should().StartWith(version + "-a1-");
        fixture.PrecisionPolicies.Verify(
            p => p.FindAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        fixture.Renderer.Verify(
            r =>
                r.RenderAsync(
                    It.Is<IRideDocumentLayout>(l =>
                        l is InvoiceRideDocumentLayout
                        && ((InvoiceRideDocumentLayout)l).Precision.QuantityDecimals
                            == quantityDecimals
                        && ((InvoiceRideDocumentLayout)l).Precision.SalesUnitPriceDecimals
                            == priceDecimals
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        fixture.CacheStrategy.Verify(
            c =>
                c.TryGetCachedAsync(
                    TenantId,
                    ElectronicDocumentId,
                    It.IsAny<RideContentHash>(),
                    documentType.ToString(),
                    It.Is<string>(v => v.StartsWith(version + "-a1-")),
                    "unversioned",
                    "unversioned",
                    RidePipeline.RideSpecificationVersion,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        fixture.Repository.Verify(
            r => r.AddAsync(It.IsAny<RidePdfDocument>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        fixture.Repository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Cache_hit_short_circuits_without_rendering_or_storing()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture
            .ParserResolver.Setup(r => r.Resolve(RideDocumentType.Invoice))
            .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
        fixture
            .TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
            .Returns(new FakeRideTemplate(RideDocumentType.Invoice));

        var cachedMetadata = new RidePdfMetadataDto(
            "Invoice",
            "unversioned",
            "unversioned",
            "unversioned",
            new string('a', 64),
            DateTime.UtcNow,
            WasCached: true
        );
        fixture
            .CacheStrategy.Setup(c =>
                c.TryGetCachedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<RideContentHash>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RidePdfMetadataDto?>.Success(cachedMetadata));

        var cachedDocument = RidePdfDocument.Create(
            TenantId,
            CompanyId,
            ElectronicDocumentId,
            RideDocumentType.Invoice,
            RideContentHash.Create(new string('a', 64)),
            "Invoice",
            "unversioned",
            "unversioned",
            "unversioned",
            RidePipeline.RideSpecificationVersion,
            UserId
        );
        cachedDocument.MarkGenerated("ride/path/cached.pdf", DateTime.UtcNow, UserId);
        fixture
            .Repository.Setup(r =>
                r.GetByFingerprintAsync(
                    TenantId,
                    ElectronicDocumentId,
                    It.IsAny<RideContentHash>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(cachedDocument);

        var result = await fixture
            .BuildPipeline()
            .ExecuteAsync(
                TenantId,
                CompanyId,
                SourceModule,
                SourceEntityId,
                forceRegenerate: false,
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.Cached);
        result.Value.StoragePath.Should().Be("ride/path/cached.pdf");
        fixture.Renderer.Verify(
            r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        fixture.StorageService.Verify(
            s =>
                s.StoreAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<RideDocumentType>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Force_regenerate_bypasses_cache_hit_and_renders_again()
    {
        var fixture = new Fixture();
        fixture.SetupAvailableSource();
        fixture
            .ParserResolver.Setup(r => r.Resolve(RideDocumentType.Invoice))
            .Returns(new FakeRideXmlParser(RideDocumentType.Invoice));
        fixture
            .TemplateResolver.Setup(r => r.Resolve(It.IsAny<RideTemplateSelector>()))
            .Returns(new FakeRideTemplate(RideDocumentType.Invoice));
        fixture
            .ContentHasher.Setup(h => h.Compute(AuthorizedXml))
            .Returns(RideContentHash.Create(new string('a', 64)));

        var cachedMetadata = new RidePdfMetadataDto(
            "Invoice",
            "unversioned",
            "unversioned",
            "unversioned",
            new string('a', 64),
            DateTime.UtcNow,
            WasCached: true
        );
        fixture
            .CacheStrategy.Setup(c =>
                c.TryGetCachedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<RideContentHash>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RidePdfMetadataDto?>.Success(cachedMetadata));
        fixture
            .BrandingProvider.Setup(b =>
                b.GetAsync(TenantId, CompanyId, null, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));
        fixture
            .Renderer.Setup(r =>
                r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([9, 9, 9]);
        fixture
            .StorageService.Setup(s =>
                s.StoreAsync(
                    TenantId,
                    RideDocumentType.Invoice,
                    ElectronicDocumentId,
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<string>.Success("ride/path/regenerated.pdf"));

        var existingDocument = RidePdfDocument.Create(
            TenantId,
            CompanyId,
            ElectronicDocumentId,
            RideDocumentType.Invoice,
            RideContentHash.Create(new string('a', 64)),
            "Invoice",
            "unversioned",
            "unversioned",
            "unversioned",
            RidePipeline.RideSpecificationVersion,
            UserId
        );
        existingDocument.MarkGenerated("ride/path/old.pdf", DateTime.UtcNow.AddDays(-1), UserId);
        fixture
            .Repository.Setup(r =>
                r.GetByFingerprintAsync(
                    TenantId,
                    ElectronicDocumentId,
                    It.IsAny<RideContentHash>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(existingDocument);

        var result = await fixture
            .BuildPipeline()
            .ExecuteAsync(
                TenantId,
                CompanyId,
                SourceModule,
                SourceEntityId,
                forceRegenerate: true,
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(RideOutcome.Generated);
        result.Value.StoragePath.Should().Be("ride/path/regenerated.pdf");
        fixture.Renderer.Verify(
            r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        fixture.Repository.Verify(
            r => r.AddAsync(It.IsAny<RidePdfDocument>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        fixture.Repository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Source_infrastructure_failure_propagates_as_result_failure_not_success_with_failed_outcome()
    {
        var fixture = new Fixture();
        fixture
            .SourceXmlProvider.Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<RideSourceXmlLookup>.Failure(
                    "El servicio de documentos electrónicos no respondió."
                )
            );

        var result = await fixture
            .BuildPipeline()
            .ExecuteAsync(
                TenantId,
                CompanyId,
                SourceModule,
                SourceEntityId,
                forceRegenerate: false,
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
    }
}
