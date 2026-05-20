using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembership;

public record RevokeCompanyUserMembershipCommand(
    Guid SubscriberId,
    string UserEmail
) : IRequest<Result<object>>;

