using MediatR;
using ERP.Application.Common;
using ERP.Domain.Security.Entities;
using ERP.Domain.Security.Interfaces;

namespace ERP.Application.Security.UseCases.UpsertSecurityAdminScopes;

public class UpsertSecurityAdminScopesHandler : IRequestHandler<UpsertSecurityAdminScopesCommand, Result<bool>>
{
    private static readonly int[] AllScopes =
    [
        (int)SecurityAdminScope.ManageRoles,
        (int)SecurityAdminScope.ManageModules,
        (int)SecurityAdminScope.ManageScreens,
        (int)SecurityAdminScope.ManageProcesses,
    ];

    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;
    private readonly ISecurityRepository _repository;

    public UpsertSecurityAdminScopesHandler(
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ISecurityRepository repository)
    {
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(UpsertSecurityAdminScopesCommand command, CancellationToken ct)
    {
        if (!_currentSubscriber.IsAuthenticated || _currentSubscriber.SubscriberId == Guid.Empty)
            return Result<bool>.Failure("Subscriber inválido.");

        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<bool>.Failure("No autenticado.");

        var subjectType = command.SubjectType.Trim();
        if (subjectType is not ("Role" or "User"))
            return Result<bool>.Failure("SubjectType inválido.");

        var subjectKey = command.SubjectKey.Trim();
        if (string.IsNullOrWhiteSpace(subjectKey))
            return Result<bool>.Failure("SubjectKey requerido.");

        var allowed = new HashSet<int>(command.AllowedScopes.Distinct());

        foreach (var scope in AllScopes)
        {
            var shouldAllow = allowed.Contains(scope);
            var existing = await _repository.GetAdminScopeAsync(_currentSubscriber.SubscriberId, subjectType, subjectKey, scope, ct);
            if (existing is null)
            {
                var created = SecurityAdminScopeAssignment.Create(
                    _currentSubscriber.SubscriberId,
                    subjectType,
                    subjectKey,
                    scope,
                    shouldAllow,
                    createdBy: _currentUser.UserId);
                await _repository.AddAsync(created, ct);
            }
            else
            {
                existing.SetAllowed(shouldAllow, updatedBy: _currentUser.UserId);
            }
        }

        await _repository.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

