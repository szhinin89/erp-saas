using ERP.Application.Common;
using ERP.Application.MasterData;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetBusinessPartner;

public sealed class GetBusinessPartnerHandler
    : IRequestHandler<GetBusinessPartnerQuery, Result<BusinessPartnerDto>>
{
    private readonly IBusinessPartnerRepository _repo;
    private readonly IBusinessPartnerOperationalLinkEnricher _linkEnricher;

    public GetBusinessPartnerHandler(
        IBusinessPartnerRepository repo,
        IBusinessPartnerOperationalLinkEnricher linkEnricher)
    {
        _repo = repo;
        _linkEnricher = linkEnricher;
    }

    public async Task<Result<BusinessPartnerDto>> Handle(
        GetBusinessPartnerQuery request, CancellationToken ct)
    {
        var bp = await _repo.GetByIdAsync(request.Id, ct);
        if (bp is null)
            return Result<BusinessPartnerDto>.Failure("BusinessPartner no encontrado.");

        var enriched = await _linkEnricher.EnrichAsync(bp, ct);
        return Result<BusinessPartnerDto>.Success(enriched);
    }
}
