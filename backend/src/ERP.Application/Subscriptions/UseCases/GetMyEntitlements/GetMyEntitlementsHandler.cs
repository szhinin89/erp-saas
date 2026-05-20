using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Subscriptions.UseCases.GetMyEntitlements;

public sealed class GetMyEntitlementsHandler : IRequestHandler<GetMyEntitlementsQuery, Result<SubscriberEntitlementsSnapshot>>
{
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ISubscriberEntitlementsService _entitlements;

    public GetMyEntitlementsHandler(ICurrentSubscriber currentSubscriber, ISubscriberEntitlementsService entitlements)
    {
        _currentSubscriber = currentSubscriber;
        _entitlements = entitlements;
    }

    public async Task<Result<SubscriberEntitlementsSnapshot>> Handle(GetMyEntitlementsQuery request, CancellationToken ct)
    {
        if (!_currentSubscriber.IsAuthenticated || _currentSubscriber.SubscriberId == Guid.Empty)
            return Result<SubscriberEntitlementsSnapshot>.Failure("No autenticado.");

        var snapshot = await _entitlements.GetEntitlementsSnapshotAsync(_currentSubscriber.SubscriberId, ct);
        return Result<SubscriberEntitlementsSnapshot>.Success(snapshot);
    }
}
