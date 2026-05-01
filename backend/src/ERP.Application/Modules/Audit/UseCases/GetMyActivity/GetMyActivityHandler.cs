using ERP.Application.Audit.DTOs;
using ERP.Application.Common;
using ERP.Domain.Audit.Interfaces;

namespace ERP.Application.Audit.UseCases.GetMyActivity;

public class GetMyActivityHandler
{
    private readonly IUserActivityRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public GetMyActivityHandler(
        IUserActivityRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<UserActivityDto>>> HandleAsync(
        string? module,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<IReadOnlyList<UserActivityDto>>.Failure("No autenticado.");

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var skip = (page - 1) * pageSize;
        var list = await _repo.GetMyRecentAsync(
            _currentTenant.TenantId,
            _currentUser.UserId,
            module,
            skip,
            pageSize,
            ct);

        var dto = list
            .Select(x => new UserActivityDto(
                x.Id,
                x.Module,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.Description,
                x.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<UserActivityDto>>.Success(dto);
    }
}

