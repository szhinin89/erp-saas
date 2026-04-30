namespace ERP.Application.Security.UseCases.UpsertSecurityAdminScopes;

public record UpsertSecurityAdminScopesCommand(
    string SubjectType, // "Role" | "User"
    string SubjectKey,  // role name OR userId Guid string
    IReadOnlyList<int> AllowedScopes
);

