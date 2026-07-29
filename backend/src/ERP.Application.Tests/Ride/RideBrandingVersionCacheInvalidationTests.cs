using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.Services;
using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// Fase 8 (decisión confirmada): demuestra que un cambio real de contenido de branding produce
/// un <see cref="RideBrandingVersion"/> distinto, y que <see cref="RideCacheStrategy"/> (real
/// desde la Fase 5) trata esa diferencia como cache-miss — verificado a nivel de la estrategia,
/// no como invalidación automática end-to-end de <c>RidePipeline</c> (protegido esta fase, ver
/// auditoría).
/// </summary>
public sealed class RideBrandingVersionCacheInvalidationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ElectronicDocumentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly RideContentHash Hash = RideContentHash.Create(new string('a', 64));

    [Fact]
    public void Identical_branding_content_produces_the_same_version()
    {
        var a = RideBranding.Create("logo.png", "#112233", "#445566", "Gracias");
        var b = RideBranding.Create("logo.png", "#112233", "#445566", "Gracias");

        RideBrandingVersion.Compute(a).Should().Be(RideBrandingVersion.Compute(b));
    }

    [Fact]
    public void Changing_any_branding_field_produces_a_different_version()
    {
        var original = RideBranding.Create("logo.png", "#112233", "#445566", "Gracias");
        var updated = RideBranding.Create("logo.png", "#FF0000", "#445566", "Gracias");

        RideBrandingVersion.Compute(original).Should().NotBe(RideBrandingVersion.Compute(updated));
    }

    [Fact]
    public async Task Cache_strategy_treats_an_updated_branding_version_as_a_miss()
    {
        var oldBranding = RideBranding.Create(
            "logo.png",
            "#112233",
            "#445566",
            "Gracias por su compra"
        );
        var newBranding = RideBranding.Create(
            "logo.png",
            "#000000",
            "#445566",
            "Gracias por su compra"
        );
        var oldVersion = RideBrandingVersion.Compute(oldBranding);
        var newVersion = RideBrandingVersion.Compute(newBranding);
        oldVersion.Should().NotBe(newVersion);

        var generatedWithOldBranding = RidePdfDocument.Create(
            TenantId,
            Guid.NewGuid(),
            ElectronicDocumentId,
            RideDocumentType.Invoice,
            Hash,
            "Invoice",
            "1.0.0",
            oldVersion,
            "1.0.0",
            "1.0.0",
            UserId
        );
        generatedWithOldBranding.MarkGenerated("ride/path.pdf", DateTime.UtcNow, UserId);

        var repository = new Mock<IRidePdfDocumentRepository>();
        repository
            .Setup(r =>
                r.GetByFingerprintAsync(
                    TenantId,
                    ElectronicDocumentId,
                    Hash,
                    "1.0.0",
                    oldVersion,
                    "1.0.0",
                    "1.0.0",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(generatedWithOldBranding);
        var strategy = new RideCacheStrategy(repository.Object);

        var hitWithOldVersion = await strategy.TryGetCachedAsync(
            TenantId,
            ElectronicDocumentId,
            Hash,
            "Invoice",
            "1.0.0",
            oldVersion,
            "1.0.0",
            "1.0.0"
        );
        var missWithNewVersion = await strategy.TryGetCachedAsync(
            TenantId,
            ElectronicDocumentId,
            Hash,
            "Invoice",
            "1.0.0",
            newVersion,
            "1.0.0",
            "1.0.0"
        );

        hitWithOldVersion.Value.Should().NotBeNull();
        missWithNewVersion.Value.Should().BeNull();
    }
}
