using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.GetUserSessionsPaged;

public sealed class GetUserSessionsPagedHandler
    : IRequestHandler<GetUserSessionsPagedQuery, Result<PagedResult<UserSessionAdminDto>>>
{
    private readonly IUserSessionRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetUserSessionsPagedHandler(
        IUserSessionRepository repository,
        ICurrentTenant currentTenant
    )
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<PagedResult<UserSessionAdminDto>>> Handle(
        GetUserSessionsPagedQuery request,
        CancellationToken cancellationToken
    )
    {
        UserSessionStatus? status = null;
        if (
            !string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<UserSessionStatus>(request.Status, true, out var parsed)
        )
            status = parsed;

        var (items, total) = await _repository.GetPagedAsync(
            _currentTenant.TenantId,
            request.IdentityUserId,
            request.CompanyId,
            status,
            request.FromUtc,
            request.ToUtc,
            request.PageNumber,
            request.PageSize,
            cancellationToken
        );

        var dtos = items.Select(AccessSessionMapper.ToAdminDto).ToList();
        var result = new PagedResult<UserSessionAdminDto>(
            dtos,
            request.PageNumber,
            request.PageSize,
            total
        );

        return Result<PagedResult<UserSessionAdminDto>>.Success(result);
    }
}
