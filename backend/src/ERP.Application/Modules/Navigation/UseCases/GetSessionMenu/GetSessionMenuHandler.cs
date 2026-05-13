using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using MediatR;

namespace ERP.Application.Navigation.UseCases.GetSessionMenu;

public sealed class GetSessionMenuHandler : IRequestHandler<GetSessionMenuQuery, Result<IReadOnlyList<SessionMenuGroupDto>>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantSessionMenuResolver _menuResolver;

    public GetSessionMenuHandler(ICurrentTenant currentTenant, ITenantSessionMenuResolver menuResolver)
    {
        _currentTenant = currentTenant;
        _menuResolver = menuResolver;
    }

    public Task<Result<IReadOnlyList<SessionMenuGroupDto>>> HandleAsync(CancellationToken ct = default)
        => Handle(new GetSessionMenuQuery(), ct);

    public async Task<Result<IReadOnlyList<SessionMenuGroupDto>>> Handle(GetSessionMenuQuery request, CancellationToken ct)
    {
        var menu = await _menuResolver.ResolveForTenantAsync(_currentTenant.TenantId, ct);
        return Result<IReadOnlyList<SessionMenuGroupDto>>.Success(menu);
    }
}
