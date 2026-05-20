using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembership;

public record UpsertCompanyUserMembershipCommand(
    Guid SubscriberId,
    string UserEmail,
    string Role,
    Guid? ProfileId = null
) : IRequest<Result<object>>;

