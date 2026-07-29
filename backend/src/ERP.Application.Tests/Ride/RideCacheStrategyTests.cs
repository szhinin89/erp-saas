using ERP.Application.Modules.Ride.Services;
using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// Prueba que <see cref="RideCacheStrategy"/> detecta un cambio en cada uno de los 5 componentes
/// de la huella de ADR-025 §14 — cualquier diferencia produce un cache-miss.
/// </summary>
public sealed class RideCacheStrategyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ElectronicDocumentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly RideContentHash Hash = RideContentHash.Create(new string('a', 64));

    private static RidePdfDocument GeneratedDocument(RideContentHash? hash = null) =>
        Generated(hash ?? Hash, "t1", "b1", "r1", "s1");

    private static RidePdfDocument Generated(
        RideContentHash hash,
        string tv,
        string bv,
        string rv,
        string sv
    )
    {
        var document = RidePdfDocument.Create(
            TenantId,
            Guid.NewGuid(),
            ElectronicDocumentId,
            RideDocumentType.Invoice,
            hash,
            "Invoice",
            tv,
            bv,
            rv,
            sv,
            UserId
        );
        document.MarkGenerated("ride/path.pdf", DateTime.UtcNow, UserId);
        return document;
    }

    private static Mock<IRidePdfDocumentRepository> RepositoryMatchingOnly(
        RideContentHash hash,
        string tv,
        string bv,
        string rv,
        string sv,
        RidePdfDocument document
    )
    {
        var repo = new Mock<IRidePdfDocumentRepository>();
        repo.Setup(r =>
                r.GetByFingerprintAsync(
                    TenantId,
                    ElectronicDocumentId,
                    hash,
                    tv,
                    bv,
                    rv,
                    sv,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(document);
        return repo;
    }

    [Fact]
    public async Task Exact_fingerprint_match_on_a_generated_row_returns_cached_metadata()
    {
        var repo = RepositoryMatchingOnly(Hash, "t1", "b1", "r1", "s1", GeneratedDocument());
        var strategy = new RideCacheStrategy(repo.Object);

        var result = await strategy.TryGetCachedAsync(
            TenantId,
            ElectronicDocumentId,
            Hash,
            "Invoice",
            "t1",
            "b1",
            "r1",
            "s1"
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.WasCached.Should().BeTrue();
    }

    [Fact]
    public async Task Different_source_xml_hash_is_a_cache_miss()
    {
        var repo = RepositoryMatchingOnly(Hash, "t1", "b1", "r1", "s1", GeneratedDocument());
        var strategy = new RideCacheStrategy(repo.Object);
        var differentHash = RideContentHash.Create(new string('b', 64));

        var result = await strategy.TryGetCachedAsync(
            TenantId,
            ElectronicDocumentId,
            differentHash,
            "Invoice",
            "t1",
            "b1",
            "r1",
            "s1"
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Different_template_version_is_a_cache_miss()
    {
        var repo = RepositoryMatchingOnly(Hash, "t1", "b1", "r1", "s1", GeneratedDocument());
        var strategy = new RideCacheStrategy(repo.Object);

        var result = await strategy.TryGetCachedAsync(
            TenantId,
            ElectronicDocumentId,
            Hash,
            "Invoice",
            "t2",
            "b1",
            "r1",
            "s1"
        );

        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Different_branding_version_is_a_cache_miss()
    {
        var repo = RepositoryMatchingOnly(Hash, "t1", "b1", "r1", "s1", GeneratedDocument());
        var strategy = new RideCacheStrategy(repo.Object);

        var result = await strategy.TryGetCachedAsync(
            TenantId,
            ElectronicDocumentId,
            Hash,
            "Invoice",
            "t1",
            "b2",
            "r1",
            "s1"
        );

        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Different_renderer_version_is_a_cache_miss()
    {
        var repo = RepositoryMatchingOnly(Hash, "t1", "b1", "r1", "s1", GeneratedDocument());
        var strategy = new RideCacheStrategy(repo.Object);

        var result = await strategy.TryGetCachedAsync(
            TenantId,
            ElectronicDocumentId,
            Hash,
            "Invoice",
            "t1",
            "b1",
            "r2",
            "s1"
        );

        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Different_ride_specification_version_is_a_cache_miss()
    {
        var repo = RepositoryMatchingOnly(Hash, "t1", "b1", "r1", "s1", GeneratedDocument());
        var strategy = new RideCacheStrategy(repo.Object);

        var result = await strategy.TryGetCachedAsync(
            TenantId,
            ElectronicDocumentId,
            Hash,
            "Invoice",
            "t1",
            "b1",
            "r1",
            "s2"
        );

        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task A_matching_row_that_is_not_yet_generated_is_never_served_as_cached()
    {
        var pending = RidePdfDocument.Create(
            TenantId,
            Guid.NewGuid(),
            ElectronicDocumentId,
            RideDocumentType.Invoice,
            Hash,
            "Invoice",
            "t1",
            "b1",
            "r1",
            "s1",
            UserId
        );
        var repo = RepositoryMatchingOnly(Hash, "t1", "b1", "r1", "s1", pending);
        var strategy = new RideCacheStrategy(repo.Object);

        var result = await strategy.TryGetCachedAsync(
            TenantId,
            ElectronicDocumentId,
            Hash,
            "Invoice",
            "t1",
            "b1",
            "r1",
            "s1"
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
