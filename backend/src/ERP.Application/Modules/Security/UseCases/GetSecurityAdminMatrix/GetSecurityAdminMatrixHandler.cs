using MediatR;
using ERP.Application.Common;
using ERP.Application.Security.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Security.Interfaces;

namespace ERP.Application.Security.UseCases.GetSecurityAdminMatrix;

public class GetSecurityAdminMatrixHandler : IRequestHandler<GetSecurityAdminMatrixQuery, Result<(IReadOnlyList<SecurityUserDto> Users, IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments)>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IAccessRepository _accessRepository;
    private readonly ISecurityRepository _securityRepository;

    public GetSecurityAdminMatrixHandler(
        ICurrentTenant currentTenant,
        IAccessRepository accessRepository,
        ISecurityRepository securityRepository)
    {
        _currentTenant = currentTenant;
        _accessRepository = accessRepository;
        _securityRepository = securityRepository;
    }

    public async Task<Result<(IReadOnlyList<SecurityUserDto> Users, IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments)>> Handle(
        GetSecurityAdminMatrixQuery query, CancellationToken cancellationToken)
    {
        if (!(_currentTenant.TenantId != Guid.Empty) || _currentTenant.TenantId == Guid.Empty)
            return Result<(IReadOnlyList<SecurityUserDto>, IReadOnlyList<SecurityAdminScopeAssignmentDto>)>.Failure("Tenant inválido.");

        var users = await _accessRepository.GetActiveIdentityUsersForTenantAsync(_currentTenant.TenantId, cancellationToken);
        var memberships = await _accessRepository.GetCompanyUserMembershipsByTenantAsync(
            _currentTenant.TenantId, onlyActive: true, cancellationToken);
        var roleByUser = memberships
            .GroupBy(m => m.IdentityUserId)
            .ToDictionary(g => g.Key, g => g.First().Role);
        var membershipIdByUser = memberships
            .GroupBy(m => m.IdentityUserId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var assignments = await _securityRepository.GetAdminScopesAsync(_currentTenant.TenantId, cancellationToken);

        var userDtos = users.Select(u => new SecurityUserDto(
            u.Id,
            u.Username,
            u.FullName,
            u.Email?.Value,
            roleByUser.TryGetValue(u.Id, out var role) ? role : SecurityRoles.User,
            u.IsActive,
            membershipIdByUser.TryGetValue(u.Id, out var membershipId) ? membershipId : Guid.Empty
        )).ToList();

        var assignmentDtos = assignments.Select(a => new SecurityAdminScopeAssignmentDto(
            a.SubjectType,
            a.SubjectKey,
            a.Scope,
            a.IsAllowed
        )).ToList();

        return Result<(IReadOnlyList<SecurityUserDto>, IReadOnlyList<SecurityAdminScopeAssignmentDto>)>.Success((userDtos, assignmentDtos));
    }
}
