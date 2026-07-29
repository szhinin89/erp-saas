using ERP.Application.Audit.DTOs;
using ERP.Application.Common;
using ERP.Domain.Audit.Interfaces;
using MediatR;

namespace ERP.Application.Audit.UseCases.GetEntityActivity;

public class GetEntityActivityHandler
    : IRequestHandler<GetEntityActivityQuery, Result<IReadOnlyList<UserActivityDto>>>
{
    private readonly IUserActivityRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public GetEntityActivityHandler(
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
        GetEntityActivityQuery query,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<IReadOnlyList<UserActivityDto>>.Failure("No autenticado.");

        if (query.EntityId == Guid.Empty)
            return Result<IReadOnlyList<UserActivityDto>>.Failure("EntityId requerido.");

        if (string.IsNullOrWhiteSpace(query.EntityType))
            return Result<IReadOnlyList<UserActivityDto>>.Failure("EntityType requerido.");

        var take =
            query.Take < 1 ? 10
            : query.Take > 50 ? 50
            : query.Take;

        var list = await _repo.GetByEntityAsync(
            _currentTenant.TenantId,
            query.EntityType.Trim(),
            query.EntityId,
            take,
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
