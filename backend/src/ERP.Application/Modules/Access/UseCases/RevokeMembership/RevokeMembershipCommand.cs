using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Access.UseCases.RevokeMembership;

public record RevokeMembershipCommand(
    Guid TenantId,
    string UserEmail
) : IRequest<Result<object>>;

