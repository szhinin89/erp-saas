using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetBusinessPartnerRoles;

public sealed record GetBusinessPartnerRolesQuery(
    Guid  BusinessPartnerId,
    bool? OnlyActive = true)
    : IRequest<Result<IReadOnlyList<BusinessPartnerRoleDto>>>, ISubscriberScopedRequest;
