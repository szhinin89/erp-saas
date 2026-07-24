using ERP.Domain.Modules.Ride.Enums;
using ERP.Infrastructure.Ride.Storage;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Ride;

public sealed class RidePdfStorageNamingStrategyTests
{
    [Fact]
    public void Same_inputs_always_produce_the_same_path()
    {
        var strategy = new RidePdfStorageNamingStrategy();
        var tenantId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();

        var first = strategy.BuildRelativePath(tenantId, RideDocumentType.Invoice, electronicDocumentId, "1.0.0");
        var second = strategy.BuildRelativePath(tenantId, RideDocumentType.Invoice, electronicDocumentId, "1.0.0");

        second.Should().Be(first);
    }

    [Fact]
    public void Path_never_contains_a_timestamp_or_random_component()
    {
        var strategy = new RidePdfStorageNamingStrategy();
        var tenantId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();

        var calls = Enumerable.Range(0, 5)
            .Select(_ => strategy.BuildRelativePath(tenantId, RideDocumentType.Invoice, electronicDocumentId, "1.0.0"))
            .Distinct();

        calls.Should().ContainSingle();
    }

    [Fact]
    public void Different_tenant_produces_a_different_path()
    {
        var strategy = new RidePdfStorageNamingStrategy();
        var electronicDocumentId = Guid.NewGuid();

        var a = strategy.BuildRelativePath(Guid.NewGuid(), RideDocumentType.Invoice, electronicDocumentId, "1.0.0");
        var b = strategy.BuildRelativePath(Guid.NewGuid(), RideDocumentType.Invoice, electronicDocumentId, "1.0.0");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Different_electronic_document_id_produces_a_different_path()
    {
        var strategy = new RidePdfStorageNamingStrategy();
        var tenantId = Guid.NewGuid();

        var a = strategy.BuildRelativePath(tenantId, RideDocumentType.Invoice, Guid.NewGuid(), "1.0.0");
        var b = strategy.BuildRelativePath(tenantId, RideDocumentType.Invoice, Guid.NewGuid(), "1.0.0");

        a.Should().NotBe(b);
    }
}
