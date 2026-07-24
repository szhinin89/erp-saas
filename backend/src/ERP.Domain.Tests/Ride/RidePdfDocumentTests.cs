using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Events;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Ride;

public sealed class RidePdfDocumentTests
{
    private static RideContentHash ValidHash() => RideContentHash.Create(new string('a', 64));

    private static RidePdfDocument NewPending() => RidePdfDocument.Create(
        tenantId: Guid.NewGuid(),
        companyId: Guid.NewGuid(),
        electronicDocumentId: Guid.NewGuid(),
        documentType: RideDocumentType.Invoice,
        sourceXmlHash: ValidHash(),
        templateId: "DefaultInvoiceRideTemplate",
        templateVersion: "1.0.0",
        brandingVersion: "1.0.0",
        rendererVersion: "1.0.0",
        rideSpecificationVersion: "1.0.0",
        createdBy: Guid.NewGuid());

    [Fact]
    public void Create_with_valid_data_starts_in_pending()
    {
        var document = NewPending();

        document.State.Should().Be(RidePdfState.Pending);
        document.StoragePath.Should().BeNull();
        document.RetryCount.Should().Be(0);
        document.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_with_empty_electronic_document_id_throws()
    {
        var act = () => RidePdfDocument.Create(
            tenantId: Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            electronicDocumentId: Guid.Empty,
            documentType: RideDocumentType.Invoice,
            sourceXmlHash: ValidHash(),
            templateId: "DefaultInvoiceRideTemplate",
            templateVersion: "1.0.0",
            brandingVersion: "1.0.0",
            rendererVersion: "1.0.0",
            rideSpecificationVersion: "1.0.0",
            createdBy: Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*documento electrónico de origen*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_with_missing_template_version_throws(string? templateVersion)
    {
        var act = () => RidePdfDocument.Create(
            tenantId: Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            electronicDocumentId: Guid.NewGuid(),
            documentType: RideDocumentType.Invoice,
            sourceXmlHash: ValidHash(),
            templateId: "DefaultInvoiceRideTemplate",
            templateVersion: templateVersion!,
            brandingVersion: "1.0.0",
            rendererVersion: "1.0.0",
            rideSpecificationVersion: "1.0.0",
            createdBy: Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*versión de plantilla*");
    }

    [Fact]
    public void MarkGenerated_from_pending_transitions_and_raises_event()
    {
        var document = NewPending();
        var generatedAt = DateTime.UtcNow;

        document.MarkGenerated("ride/tenant/invoice/doc/v1.pdf", generatedAt, Guid.NewGuid());

        document.State.Should().Be(RidePdfState.Generated);
        document.StoragePath.Should().Be("ride/tenant/invoice/doc/v1.pdf");
        document.GeneratedAtUtc.Should().Be(generatedAt);
        document.DomainEvents.Should().ContainSingle(e => e is RidePdfGeneratedEvent);
    }

    [Fact]
    public void MarkGenerated_from_generated_throws_and_regenerate_must_be_used_instead()
    {
        var document = NewPending();
        document.MarkGenerated("ride/path/v1.pdf", DateTime.UtcNow, Guid.NewGuid());

        var act = () => document.MarkGenerated("ride/path/v2.pdf", DateTime.UtcNow, Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MarkRegenerated*");
    }

    [Fact]
    public void MarkRegenerated_from_generated_updates_storage_and_raises_event()
    {
        var document = NewPending();
        document.MarkGenerated("ride/path/v1.pdf", DateTime.UtcNow, Guid.NewGuid());
        var regeneratedAt = DateTime.UtcNow.AddMinutes(5);

        document.MarkRegenerated("ride/path/v1-refresh.pdf", regeneratedAt, Guid.NewGuid());

        document.State.Should().Be(RidePdfState.Generated);
        document.StoragePath.Should().Be("ride/path/v1-refresh.pdf");
        document.DomainEvents.Should().ContainSingle(e => e is RidePdfRegeneratedEvent);
    }

    [Fact]
    public void MarkRegenerated_from_pending_throws_because_nothing_was_generated_yet()
    {
        var document = NewPending();

        var act = () => document.MarkRegenerated("ride/path/v1.pdf", DateTime.UtcNow, Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_from_pending_transitions_and_increments_retry_count()
    {
        var document = NewPending();

        document.MarkFailed("El renderer lanzó una excepción.", Guid.NewGuid());

        document.State.Should().Be(RidePdfState.Failed);
        document.LastError.Should().Be("El renderer lanzó una excepción.");
        document.RetryCount.Should().Be(1);
        document.DomainEvents.Should().ContainSingle(e => e is RidePdfGenerationFailedEvent);
    }

    [Fact]
    public void MarkFailed_from_generated_throws_because_a_successful_record_is_never_downgraded()
    {
        var document = NewPending();
        document.MarkGenerated("ride/path/v1.pdf", DateTime.UtcNow, Guid.NewGuid());

        var act = () => document.MarkFailed("motivo cualquiera", Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkPendingSource_from_pending_transitions_without_raising_a_domain_event()
    {
        var document = NewPending();

        document.MarkPendingSource(Guid.NewGuid());

        document.State.Should().Be(RidePdfState.PendingSource);
        document.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MarkPendingSource_from_generated_throws()
    {
        var document = NewPending();
        document.MarkGenerated("ride/path/v1.pdf", DateTime.UtcNow, Guid.NewGuid());

        var act = () => document.MarkPendingSource(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Retry_after_failure_can_reach_generated()
    {
        var document = NewPending();
        document.MarkFailed("timeout de storage", Guid.NewGuid());

        document.MarkGenerated("ride/path/v1.pdf", DateTime.UtcNow, Guid.NewGuid());

        document.State.Should().Be(RidePdfState.Generated);
        document.RetryCount.Should().Be(1);
    }
}
