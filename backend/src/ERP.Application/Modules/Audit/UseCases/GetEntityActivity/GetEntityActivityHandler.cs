using ERP.Application.Audit.DTOs;
using ERP.Application.Common;
using ERP.Domain.Audit.Interfaces;

namespace ERP.Application.Audit.UseCases.GetEntityActivity;

public class GetEntityActivityHandler
{
    private readonly IUserActivityRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public GetEntityActivityHandler(
        IUserActivityRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<UserActivityDto>>> HandleAsync(
        string entityType,
        Guid entityId,
        int take = 10,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<IReadOnlyList<UserActivityDto>>.Failure("No autenticado.");

        if (entityId == Guid.Empty)
            return Result<IReadOnlyList<UserActivityDto>>.Failure("EntityId requerido.");

        if (string.IsNullOrWhiteSpace(entityType))
            return Result<IReadOnlyList<UserActivityDto>>.Failure("EntityType requerido.");

        if (take < 1) take = 10;
        if (take > 50) take = 50;

        var list = await _repo.GetByEntityAsync(
            _currentTenant.TenantId,
            entityType.Trim(),
            entityId,
            take,
            ct);

        var dto = list
            .Select(x => new UserActivityDto(
                x.Id,
                x.Module,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.Description,
                x.CreatedAt,
                x.UserEmail,
                x.UserFullName))
            .ToList();

        return Result<IReadOnlyList<UserActivityDto>>.Success(dto);
    }
}
