using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Application.Modules.Ride.UseCases.RegenerateRide;
using FluentAssertions;
using MediatR;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// Verifica que los contratos públicos de ADR-025 §7 existen exactamente con esa forma. La
/// instanciación en sí (no solo reflexión) es la prueba más fuerte: si un campo faltara o
/// tuviera otro tipo, esto no compilaría.
/// </summary>
public sealed class RideContractsTests
{
    [Fact]
    public void GetOrGenerateRideQuery_is_a_company_scoped_mediatr_request_keyed_by_source()
    {
        var query = new GetOrGenerateRideQuery("Sales", Guid.NewGuid());

        query.Should().BeAssignableTo<IRequest<Result<RideGenerationResultDto>>>();
        query.Should().BeAssignableTo<ICompanyScopedRequest>();
        query.SourceModule.Should().Be("Sales");
    }

    [Fact]
    public void RegenerateRideCommand_is_a_company_scoped_mediatr_request_keyed_by_source()
    {
        var command = new RegenerateRideCommand("Sales", Guid.NewGuid());

        command.Should().BeAssignableTo<IRequest<Result<RideGenerationResultDto>>>();
        command.Should().BeAssignableTo<ICompanyScopedRequest>();
        command.SourceModule.Should().Be("Sales");
    }

    [Fact]
    public void RideGenerationResultDto_exposes_outcome_storage_path_metadata_and_reason_code()
    {
        var dto = new RideGenerationResultDto(RideOutcome.Generated, "ride/path/v1.pdf", null, null);

        dto.Outcome.Should().Be(RideOutcome.Generated);
        dto.StoragePath.Should().Be("ride/path/v1.pdf");
        dto.Metadata.Should().BeNull();
        dto.ReasonCode.Should().BeNull();
    }

    [Fact]
    public void RidePdfMetadataDto_exposes_the_five_fingerprint_fields_plus_generated_at_and_cached_flag()
    {
        var generatedAt = DateTime.UtcNow;

        var metadata = new RidePdfMetadataDto(
            TemplateId: "DefaultInvoiceRideTemplate",
            TemplateVersion: "1.0.0",
            BrandingVersion: "1.0.0",
            RendererVersion: "1.0.0",
            SourceXmlHash: new string('a', 64),
            GeneratedAtUtc: generatedAt,
            WasCached: true);

        metadata.TemplateId.Should().Be("DefaultInvoiceRideTemplate");
        metadata.WasCached.Should().BeTrue();
        metadata.GeneratedAtUtc.Should().Be(generatedAt);
    }

    [Theory]
    [InlineData(RideOutcome.Generated)]
    [InlineData(RideOutcome.Cached)]
    [InlineData(RideOutcome.PendingSource)]
    [InlineData(RideOutcome.NotApplicable)]
    [InlineData(RideOutcome.Failed)]
    public void RideOutcome_defines_the_five_values_frozen_by_adr_025(RideOutcome outcome)
    {
        Enum.IsDefined(outcome).Should().BeTrue();
    }
}
