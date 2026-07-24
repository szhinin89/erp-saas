using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;

namespace ERP.Application.Modules.Purchases.UseCases;

public sealed record GetWithholdingByIdQuery(Guid Id)
    : IRequest<Result<IssuedWithholdingDto>>, IBranchScopedRequest;

public sealed record GetWithholdingByPurchaseQuery(Guid PurchaseInvoiceId)
    : IRequest<Result<IssuedWithholdingDto?>>, IBranchScopedRequest;

public sealed class GetWithholdingByIdHandler
    : IRequestHandler<GetWithholdingByIdQuery, Result<IssuedWithholdingDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    public GetWithholdingByIdHandler(IPurchaseInvoiceRepository repo, ICurrentTenant t) { _repo = repo; _t = t; }

    public async Task<Result<IssuedWithholdingDto>> Handle(GetWithholdingByIdQuery q, CancellationToken ct)
    {
        var wh = await _repo.GetWithholdingByIdAsync(_t.TenantId, q.Id, ct);
        return wh is null
            ? Result<IssuedWithholdingDto>.NotFound("Retención no encontrada.")
            : Result<IssuedWithholdingDto>.Success(Map.ToDto(wh));
    }
}

public sealed class GetWithholdingByPurchaseHandler
    : IRequestHandler<GetWithholdingByPurchaseQuery, Result<IssuedWithholdingDto?>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    public GetWithholdingByPurchaseHandler(IPurchaseInvoiceRepository repo, ICurrentTenant t) { _repo = repo; _t = t; }

    public async Task<Result<IssuedWithholdingDto?>> Handle(GetWithholdingByPurchaseQuery q, CancellationToken ct)
    {
        var wh = await _repo.GetWithholdingByPurchaseIdAsync(_t.TenantId, q.PurchaseInvoiceId, ct);
        return Result<IssuedWithholdingDto?>.Success(wh is null ? null : Map.ToDto(wh));
    }
}

file static class Map
{
    public static IssuedWithholdingDto ToDto(IssuedWithholding w) => new(
        w.Id, w.PurchaseInvoiceId, w.SupplierId, w.EmissionPointId,
        w.WithholdingNumber, w.IssueDate, w.AccessKey,
        w.TotalRetainedVat, w.TotalRetainedIncome, w.TotalRetainedIsd,
        w.TotalRetained, w.Status.ToString(),
        w.Details.Select(d => new IssuedWithholdingDetailDto(
            d.Id, d.TaxType, d.RetentionCode, d.RetentionCodeDescription,
            d.TaxableBase, d.RetentionPct, d.AmountRetained)).ToList(),
        w.CreatedAt, w.UpdatedAt);
}
