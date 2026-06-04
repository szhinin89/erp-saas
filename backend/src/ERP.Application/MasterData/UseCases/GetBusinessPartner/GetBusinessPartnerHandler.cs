using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetBusinessPartner;

public sealed class GetBusinessPartnerHandler
    : IRequestHandler<GetBusinessPartnerQuery, Result<BusinessPartnerDetailDto>>
{
    private readonly IBusinessPartnerRepository     _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;

    public GetBusinessPartnerHandler(
        IBusinessPartnerRepository     bpRepo,
        IBusinessPartnerRoleRepository roleRepo)
        => (_bpRepo, _roleRepo) = (bpRepo, roleRepo);

    public async Task<Result<BusinessPartnerDetailDto>> Handle(
        GetBusinessPartnerQuery q, CancellationToken ct)
    {
        var bp = await _bpRepo.GetByIdAsync(q.Id, ct);
        if (bp is null)
            return Result<BusinessPartnerDetailDto>.NotFound("BusinessPartner no encontrado.");

        var roles = await _roleRepo.GetByBusinessPartnerAsync(q.Id, onlyActive: null, ct);
        var roleDtos = roles.Select(BusinessPartnerRoleDto.From).ToList();

        return Result<BusinessPartnerDetailDto>.Success(
            BusinessPartnerDetailDto.From(bp, roleDtos));
    }
}
