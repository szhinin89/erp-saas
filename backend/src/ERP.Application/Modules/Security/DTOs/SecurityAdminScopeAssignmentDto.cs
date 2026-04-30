namespace ERP.Application.Security.DTOs;

public record SecurityAdminScopeAssignmentDto(
    string SubjectType,
    string SubjectKey,
    int Scope,
    bool IsAllowed
);

