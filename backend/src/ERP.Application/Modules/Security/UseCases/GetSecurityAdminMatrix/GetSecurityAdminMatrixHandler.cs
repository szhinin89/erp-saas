using MediatR;
using ERP.Application.Common;
using ERP.Application.Security.DTOs;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Security.Interfaces;

namespace ERP.Application.Security.UseCases.GetSecurityAdminMatrix;

public class GetSecurityAdminMatrixHandler : IRequestHandler<GetSecurityAdminMatrixQuery, Result<(IReadOnlyList<SecurityUserDto> Users, IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments)>>
{
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly IUserRepository _userRepository;
    private readonly ISecurityRepository _securityRepository;

    public GetSecurityAdminMatrixHandler(
        ICurrentSubscriber currentSubscriber,
        IUserRepository userRepository,
        ISecurityRepository securityRepository)
    {
        _currentSubscriber = currentSubscriber;
        _userRepository = userRepository;
        _securityRepository = securityRepository;
    }

    public async Task<Result<(IReadOnlyList<SecurityUserDto> Users, IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments)>> Handle(
        GetSecurityAdminMatrixQuery query, CancellationToken ct)
    {
        if (!_currentSubscriber.IsAuthenticated || _currentSubscriber.SubscriberId == Guid.Empty)
            return Result<(IReadOnlyList<SecurityUserDto>, IReadOnlyList<SecurityAdminScopeAssignmentDto>)>.Failure("Subscriber inválido.");

        var users = await _userRepository.GetAllByTenantAsync(_currentSubscriber.SubscriberId, ct);
        var assignments = await _securityRepository.GetAdminScopesAsync(_currentSubscriber.SubscriberId, ct);

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

