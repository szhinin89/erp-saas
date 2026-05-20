using MediatR;
using ERP.Application.Audit.DTOs;
using ERP.Application.Common;
using ERP.Domain.Audit.Interfaces;

namespace ERP.Application.Audit.UseCases.GetMyActivity;

public class GetMyActivityHandler : IRequestHandler<GetMyActivityQuery, Result<IReadOnlyList<UserActivityDto>>>
{
    private readonly IUserActivityRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public GetMyActivityHandler(
        IUserActivityRepository repo,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<UserActivityDto>>> Handle(GetMyActivityQuery query, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<IReadOnlyList<UserActivityDto>>.Failure("No autenticado.");

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 25 : query.PageSize > 100 ? 100 : query.PageSize;
        var skip = (page - 1) * pageSize;

        var list = await _repo.GetMyRecentAsync(
            _currentSubscriber.SubscriberId,
            _currentUser.UserId,
            query.Module,
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
                x.CreatedAt,
                x.UserEmail,
                x.UserFullName))
            .ToList();

        return Result<IReadOnlyList<UserActivityDto>>.Success(dto);
    }
}
