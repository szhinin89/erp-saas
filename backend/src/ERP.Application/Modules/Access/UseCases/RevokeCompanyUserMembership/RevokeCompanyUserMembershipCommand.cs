using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembership;

public record RevokeCompanyUserMembershipCommand(Guid TenantId, Guid CompanyId, string Username)
    : IRequest<Result<object>>;
