using ERP.Application.Audit.DTOs;
using ERP.Application.Common;
using ERP.Domain.Audit.Interfaces;
using MediatR;

namespace ERP.Application.Audit.UseCases.GetMyActivity;

public class GetMyActivityHandler
    : IRequestHandler<GetMyActivityQuery, Result<IReadOnlyList<UserActivityDto>>>
{
    private readonly IUserActivityRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public GetMyActivityHandler(
        IUserActivityRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<UserActivityDto>>> Handle(
        GetMyActivityQuery query,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<IReadOnlyList<UserActivityDto>>.Failure("No autenticado.");

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize =
            query.PageSize < 1 ? 25
            : query.PageSize > 100 ? 100
            : query.PageSize;
        var skip = (page - 1) * pageSize;

        var list = await _repo.GetMyRecentAsync(
            _currentTenant.TenantId,
            _currentUser.UserId,
            query.Module,
            skip,
            pageSize,
            cancellationToken
        );

        var dto = list.Select(x => new UserActivityDto(
                x.Id,
                x.Module,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.Description,
                x.CreatedAt,
                x.UserEmail,
                x.UserFullName
            ))
            .ToList();

        return Result<IReadOnlyList<UserActivityDto>>.Success(dto);
    }
}
