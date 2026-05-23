using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetBusinessPartner;

public sealed class GetBusinessPartnerHandler
    : IRequestHandler<GetBusinessPartnerQuery, Result<BusinessPartnerDto>>
{
    private readonly IBusinessPartnerRepository _repo;

    public GetBusinessPartnerHandler(IBusinessPartnerRepository repo) => _repo = repo;

    public async Task<Result<BusinessPartnerDto>> Handle(
        GetBusinessPartnerQuery request, CancellationToken ct)
    {
        var bp = await _repo.GetByIdAsync(request.Id, ct);
        return bp is null
            ? Result<BusinessPartnerDto>.Failure("BusinessPartner no encontrado.")
            : Result<BusinessPartnerDto>.Success(BusinessPartnerDto.From(bp));
    }
}
