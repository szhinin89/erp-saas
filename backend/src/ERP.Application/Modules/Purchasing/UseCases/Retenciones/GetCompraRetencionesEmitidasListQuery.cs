using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.Retenciones;

public sealed record GetPurchaseRetentionsEmitidasListQuery(Guid? SupplierId)
    : IRequest<Result<IReadOnlyList<IssuedRetentionListItemDto>>>;

public sealed class GetPurchaseRetentionsEmitidasListQueryHandler
    : IRequestHandler<GetPurchaseRetentionsEmitidasListQuery, Result<IReadOnlyList<IssuedRetentionListItemDto>>>
{
    private readonly IPurchBillRepository _compraRepository;
    private readonly ICurrentTenant  _currentTenant;

    public GetPurchaseRetentionsEmitidasListQueryHandler(
        IPurchBillRepository compraRepository,
        ICurrentTenant currentTenant)
    {
        _compraRepository = compraRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<IReadOnlyList<IssuedRetentionListItemDto>>> Handle(
        GetPurchaseRetentionsEmitidasListQuery request,
        CancellationToken ct)
    {
        var items = await _compraRepository.GetIssuedRetentionsAsync(
            _currentTenant.TenantId, request.SupplierId, ct);
        var dto = items.Select(r => new IssuedRetentionListItemDto(
            r.Id, r.SupplierId, r.AccessKey, r.Status, r.TotalRetained, r.IssueDate)).ToList();
        return Result<IReadOnlyList<IssuedRetentionListItemDto>>.Success(dto);
    }
}
