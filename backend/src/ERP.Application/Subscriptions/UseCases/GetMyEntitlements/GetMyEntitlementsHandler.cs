using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Subscriptions.UseCases.GetMyEntitlements;

public sealed class GetMyEntitlementsHandler : IRequestHandler<GetMyEntitlementsQuery, Result<TenantEntitlementsSnapshot>>
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantEntitlementsService _entitlements;

    public GetMyEntitlementsHandler(ICurrentTenant currentTenant, ITenantEntitlementsService entitlements)
    {
        _currentTenant = currentTenant;
        _entitlements = entitlements;
    }

    public async Task<Result<TenantEntitlementsSnapshot>> Handle(GetMyEntitlementsQuery request, CancellationToken ct)
    {
        if (!_currentTenant.IsAuthenticated || _currentTenant.TenantId == Guid.Empty)
            return Result<TenantEntitlementsSnapshot>.Failure("No autenticado.");

        var snapshot = await _entitlements.GetEntitlementsSnapshotAsync(_currentTenant.TenantId, ct);
        return Result<TenantEntitlementsSnapshot>.Success(snapshot);
    }
}
