using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Security.UseCases.UpsertSecurityAdminScopes;

public record UpsertSecurityAdminScopesCommand(
    string SubjectType,
    string SubjectKey,
    IReadOnlyList<int> AllowedScopes
) : IRequest<Result<bool>>;
