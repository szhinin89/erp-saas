using MediatR;
using ERP.Application.Common;
using ERP.Application.Security.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Security.Interfaces;

namespace ERP.Application.Security.UseCases.GetSecurityAdminMatrix;

public class GetSecurityAdminMatrixHandler : IRequestHandler<GetSecurityAdminMatrixQuery, Result<(IReadOnlyList<SecurityUserDto> Users, IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments)>>
{
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly IAccessRepository _accessRepository;
    private readonly ISecurityRepository _securityRepository;

    public GetSecurityAdminMatrixHandler(
        ICurrentSubscriber currentSubscriber,
        IAccessRepository accessRepository,
        ISecurityRepository securityRepository)
    {
        _currentSubscriber = currentSubscriber;
        _accessRepository = accessRepository;
        _securityRepository = securityRepository;
    }

    public async Task<Result<(IReadOnlyList<SecurityUserDto> Users, IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments)>> Handle(
        GetSecurityAdminMatrixQuery query, CancellationToken ct)
    {
        if (!_currentSubscriber.IsAuthenticated || _currentSubscriber.SubscriberId == Guid.Empty)
            return Result<(IReadOnlyList<SecurityUserDto>, IReadOnlyList<SecurityAdminScopeAssignmentDto>)>.Failure("Subscriber inválido.");

        var users = await _accessRepository.GetActiveIdentityUsersForSubscriberAsync(_currentSubscriber.SubscriberId, ct);
        var memberships = await _accessRepository.GetCompanyUserMembershipsBySubscriberAsync(
            _currentSubscriber.SubscriberId, onlyActive: true, ct);
        var roleByUser = memberships
            .GroupBy(m => m.IdentityUserId)
            .ToDictionary(g => g.Key, g => g.First().Role);

        var assignments = await _securityRepository.GetAdminScopesAsync(_currentSubscriber.SubscriberId, ct);

        var userDtos = users.Select(u => new SecurityUserDto(
            u.Id,
            u.FullName,
            u.Email.Value,
            roleByUser.TryGetValue(u.Id, out var role) ? role : "User",
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
