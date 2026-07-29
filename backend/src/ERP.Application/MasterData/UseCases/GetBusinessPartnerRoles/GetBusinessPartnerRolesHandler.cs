using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetBusinessPartnerRoles;

public sealed class GetBusinessPartnerRolesHandler
    : IRequestHandler<GetBusinessPartnerRolesQuery, Result<IReadOnlyList<BusinessPartnerRoleDto>>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;

    public GetBusinessPartnerRolesHandler(IBusinessPartnerRoleRepository roleRepo) => _roleRepo = roleRepo;

    public async Task<Result<IReadOnlyList<BusinessPartnerRoleDto>>> Handle(
        GetBusinessPartnerRolesQuery q, CancellationToken cancellationToken)
    {
        var roles = await _roleRepo.GetByBusinessPartnerAsync(q.BusinessPartnerId, q.OnlyActive, cancellationToken);
        var dtos = (IReadOnlyList<BusinessPartnerRoleDto>)roles.Select(BusinessPartnerRoleDto.From).ToList();
        return Result<IReadOnlyList<BusinessPartnerRoleDto>>.Success(dtos);
    }
}
