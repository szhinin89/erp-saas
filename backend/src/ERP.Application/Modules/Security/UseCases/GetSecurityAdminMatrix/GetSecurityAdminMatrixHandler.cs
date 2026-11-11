using ERP.Application.Access.UseCases;
using ERP.Application.Common;
using ERP.Application.Security.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Security.Interfaces;
using MediatR;

namespace ERP.Application.Security.UseCases.GetSecurityAdminMatrix;

public class GetSecurityAdminMatrixHandler
    : IRequestHandler<
        GetSecurityAdminMatrixQuery,
        Result<(
            IReadOnlyList<SecurityUserDto> Users,
            IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments
        )>
    >
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IAccessRepository _accessRepository;
    private readonly ISecurityRepository _securityRepository;
    private readonly ICurrentCompany _currentCompany;

    public GetSecurityAdminMatrixHandler(
        ICurrentTenant currentTenant,
        IAccessRepository accessRepository,
        ISecurityRepository securityRepository,
        ICurrentCompany currentCompany
    )
    {
        _currentCompany = currentCompany;
        _currentTenant = currentTenant;
        _accessRepository = accessRepository;
        _securityRepository = securityRepository;
    }

    public async Task<
        Result<(
            IReadOnlyList<SecurityUserDto> Users,
            IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments
        )>
    > Handle(GetSecurityAdminMatrixQuery query, CancellationToken cancellationToken)
    {
        if (!(_currentTenant.TenantId != Guid.Empty) || _currentTenant.TenantId == Guid.Empty)
            return Result<(
                IReadOnlyList<SecurityUserDto>,
                IReadOnlyList<SecurityAdminScopeAssignmentDto>
            )>.Failure("Tenant inválido.");

        if (!_currentCompany.HasCompanyContext)
            return Result<(
                IReadOnlyList<SecurityUserDto>,
                IReadOnlyList<SecurityAdminScopeAssignmentDto>
            )>.Forbidden("No hay una empresa activa en la sesión.");

        // IAM-V1-F1: solo identidades administrables de la empresa operativa — membership activa
        // en ella y sin GlobalUserRole activo. Nunca otras empresas del tenant ni cuentas globales.
        var companyMemberships = await _accessRepository.GetCompanyUserMembershipsByCompanyAsync(
            _currentCompany.CompanyId,
            onlyActive: true,
            cancellationToken
        );
        var globalUserIds = await IamIdentityScope.GetGlobalUserIdsAsync(
            _accessRepository,
            companyMemberships.Select(m => m.IdentityUserId),
            cancellationToken
        );
        var memberships = companyMemberships
            .Where(m => !globalUserIds.Contains(m.IdentityUserId))
            .ToList();
        var users = (
            await _accessRepository.GetUsersByIdsAsync(
                memberships.Select(m => m.IdentityUserId).Distinct().ToList(),
                cancellationToken
            )
        )
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToList();
        var visibleUserKeys = users.Select(u => u.Id.ToString()).ToHashSet(
            StringComparer.OrdinalIgnoreCase
        );
        var roleByUser = memberships
            .GroupBy(m => m.IdentityUserId)
            .ToDictionary(g => g.Key, g => g.First().Role);
        var membershipIdByUser = memberships
            .GroupBy(m => m.IdentityUserId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var assignments = await _securityRepository.GetAdminScopesAsync(
            _currentTenant.TenantId,
            cancellationToken
        );

        var userDtos = users
            .Select(u => new SecurityUserDto(
                u.Id,
                u.Username,
                u.FullName,
                u.Email?.Value,
                roleByUser.TryGetValue(u.Id, out var role) ? role : SecurityRoles.User,
                u.IsActive,
                membershipIdByUser.TryGetValue(u.Id, out var membershipId)
                    ? membershipId
                    : Guid.Empty
            ))
            .ToList();

        var assignmentDtos = assignments
            .Where(a => a.SubjectType != "User" || visibleUserKeys.Contains(a.SubjectKey))
            .Select(a => new SecurityAdminScopeAssignmentDto(
                a.SubjectType,
                a.SubjectKey,
                a.Scope,
                a.IsAllowed
            ))
            .ToList();

        return Result<(
            IReadOnlyList<SecurityUserDto>,
            IReadOnlyList<SecurityAdminScopeAssignmentDto>
        )>.Success((userDtos, assignmentDtos));
    }
}
