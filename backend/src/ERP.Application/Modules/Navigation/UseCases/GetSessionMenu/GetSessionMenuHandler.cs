using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using MediatR;

namespace ERP.Application.Navigation.UseCases.GetSessionMenu;

public sealed class GetSessionMenuHandler : IRequestHandler<GetSessionMenuQuery, Result<IReadOnlyList<SessionMenuGroupDto>>>
{
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ISubscriberSessionMenuResolver _menuResolver;

    public GetSessionMenuHandler(ICurrentSubscriber currentSubscriber, ISubscriberSessionMenuResolver menuResolver)
    {
        _currentSubscriber = currentSubscriber;
        _menuResolver = menuResolver;
    }

    public Task<Result<IReadOnlyList<SessionMenuGroupDto>>> HandleAsync(CancellationToken ct = default)
        => Handle(new GetSessionMenuQuery(), ct);

    public async Task<Result<IReadOnlyList<SessionMenuGroupDto>>> Handle(GetSessionMenuQuery request, CancellationToken ct)
    {
        var menu = await _menuResolver.ResolveForSubscriberAsync(_currentSubscriber.SubscriberId, ct);
        return Result<IReadOnlyList<SessionMenuGroupDto>>.Success(menu);
    }
}
