using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Services;

namespace ERP.Application.Modules.Ride.UseCases.RegenerateRide;

/// <summary>Delega íntegramente en <see cref="IRideDocumentService"/> — sin lógica propia (ADR-025 §7).</summary>
public sealed class RegenerateRideCommandHandler
    : IRequestHandler<RegenerateRideCommand, Result<RideGenerationResultDto>>
{
    private readonly IRideDocumentService _rideDocumentService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public RegenerateRideCommandHandler(
        IRideDocumentService rideDocumentService, ICurrentTenant currentTenant, ICurrentCompany currentCompany)
    {
        _rideDocumentService = rideDocumentService;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public Task<Result<RideGenerationResultDto>> Handle(RegenerateRideCommand request, CancellationToken cancellationToken)
        => _rideDocumentService.RegenerateAsync(
            _currentTenant.TenantId, _currentCompany.CompanyId, request.SourceModule, request.SourceEntityId, cancellationToken);
}
