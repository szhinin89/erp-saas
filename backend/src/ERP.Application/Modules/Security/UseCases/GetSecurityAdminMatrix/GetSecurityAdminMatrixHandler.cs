using ERP.Application.Common;
using ERP.Application.Security.DTOs;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Security.Interfaces;

namespace ERP.Application.Security.UseCases.GetSecurityAdminMatrix;

public class GetSecurityAdminMatrixHandler
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IUserRepository _userRepository;
    private readonly ISecurityRepository _securityRepository;

    public GetSecurityAdminMatrixHandler(
        ICurrentTenant currentTenant,
        IUserRepository userRepository,
        ISecurityRepository securityRepository)
    {
        _currentTenant = currentTenant;
        _userRepository = userRepository;
        _securityRepository = securityRepository;
    }

    public async Task<Result<(IReadOnlyList<SecurityUserDto> Users, IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments)>> HandleAsync(
        CancellationToken ct = default)
    {
        if (!_currentTenant.IsAuthenticated || _currentTenant.TenantId == Guid.Empty)
            return Result<(IReadOnlyList<SecurityUserDto>, IReadOnlyList<SecurityAdminScopeAssignmentDto>)>.Failure("Tenant inválido.");

        var users = await _userRepository.GetAllByTenantAsync(_currentTenant.TenantId, ct);
        var assignments = await _securityRepository.GetAdminScopesAsync(_currentTenant.TenantId, ct);

        var userDtos = users.Select(u => new SecurityUserDto(
            u.Id,
            u.FullName,
            u.Email.Value,
            u.Role,
            u.IsActive
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

