using ERP.Application.Access.DTOs;
using ERP.Domain.Access.Entities;

namespace ERP.Application.Access;

internal static class AccessSessionMapper
{
    public static UserSessionDto ToDto(UserSession s) =>
        new(
            s.Id,
            s.TenantId,
            s.CompanyId,
            s.IdentityUserId,
            s.BranchId,
            s.TerminalId,
            s.Status.ToString(),
            s.StartedAt,
            s.ClosedAt,
            s.ClosedReason
        );

    public static UserSessionAdminDto ToAdminDto(
        UserSession s,
        string? userFullName = null,
        string? username = null,
        string? branchName = null
    ) =>
        new(
            s.Id,
            s.IdentityUserId,
            s.CompanyId,
            s.BranchId,
            s.TerminalId,
            s.Status.ToString(),
            s.CreatedAt,
            s.UpdatedAt,
            userFullName,
            username,
            branchName
        );
}
