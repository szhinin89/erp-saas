using ERP.Application.Access.DTOs;
using ERP.Domain.Access.Entities;

namespace ERP.Application.Access;

internal static class CompanyUserPreferencesMapper
{
    public static CompanyUserPreferencesDto ToDto(CompanyUserPreferences p) =>
        new(
            p.Id,
            p.TenantId,
            p.CompanyId,
            p.CompanyUserMembershipId,
            p.DefaultBranchId,
            p.LoginMode.ToString()
        );
}
