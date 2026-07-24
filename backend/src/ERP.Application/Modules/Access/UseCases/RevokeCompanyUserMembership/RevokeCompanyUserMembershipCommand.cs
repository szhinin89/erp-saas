using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembership;

public record RevokeCompanyUserMembershipCommand(
    Guid TenantId,
    string Username
) : IRequest<Result<object>>;

