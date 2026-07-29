using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Security.UseCases.UpsertSecurityAdminScopes;

public record UpsertSecurityAdminScopesCommand(
    string SubjectType,
    string SubjectKey,
    IReadOnlyList<int> AllowedScopes
) : IRequest<Result<bool>>;
