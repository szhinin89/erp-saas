using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Payables.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

/// <summary>SUPPLIER-PAYMENTS-FRONTEND-15E — fila de listado de la pantalla de Pagos a Proveedores.</summary>
public sealed record SupplierPaymentListItemDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    DateOnly PaymentDate,
    decimal TotalAmount,
    string SystemNumber,
    string? ReceiptNumber,
    string DisplayNumber,
    string Status,
    DateTime CreatedAt
);

public sealed record SupplierPaymentsListResponse(
    IReadOnlyList<SupplierPaymentListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetSupplierPaymentByIdQuery(Guid Id)
    : IRequest<Result<SupplierPaymentDto>>,
        IBranchScopedRequest;

public sealed record GetSupplierPaymentsListQuery(
    Guid? SupplierId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<Result<SupplierPaymentsListResponse>>, IBranchScopedRequest;

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetSupplierPaymentByIdHandler
    : IRequestHandler<GetSupplierPaymentByIdQuery, Result<SupplierPaymentDto>>
{
    private readonly ISupplierPaymentRepository _repo;
    private readonly ICurrentTenant _t;

    public GetSupplierPaymentByIdHandler(ISupplierPaymentRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<SupplierPaymentDto>> Handle(
        GetSupplierPaymentByIdQuery q,
        CancellationToken ct
    )
    {
        var payment = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (payment is null)
            return Result<SupplierPaymentDto>.NotFound("Pago a proveedor no encontrado.");

        return Result<SupplierPaymentDto>.Success(SupplierPaymentDtoMapper.ToDto(payment));
    }
}

public sealed class GetSupplierPaymentsListHandler
    : IRequestHandler<GetSupplierPaymentsListQuery, Result<SupplierPaymentsListResponse>>
{
    private readonly ISupplierPaymentRepository _repo;
    private readonly IBusinessPartnerRepository _partners;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetSupplierPaymentsListHandler(
        ISupplierPaymentRepository repo,
        IBusinessPartnerRepository partners,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _partners = partners;
        _t = t;
        _c = c;
    }

    public async Task<Result<SupplierPaymentsListResponse>> Handle(
        GetSupplierPaymentsListQuery q,
        CancellationToken ct
    )
    {
        SupplierPaymentStatus? status = null;
        if (
            !string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<SupplierPaymentStatus>(q.Status.Trim(), ignoreCase: true, out var parsedStatus)
        )
            status = parsedStatus;

        var (items, total) = await _repo.SearchAsync(
            _t.TenantId,
            _c.CompanyId,
            q.SupplierId,
            status,
            q.Page,
            q.PageSize,
            ct
        );

        var names = await _partners.GetNamesByIdsAsync(items.Select(x => x.SupplierId).Distinct(), ct);
        var dtos = items
            .Select(p => new SupplierPaymentListItemDto(
                p.Id,
                p.SupplierId,
                names.GetValueOrDefault(p.SupplierId, string.Empty),
                p.PaymentDate,
                p.TotalAmount,
                p.SystemNumber,
                p.ReceiptNumber,
                p.DisplayNumber,
                p.Status.ToString(),
                p.CreatedAt
            ))
            .ToList();

        return Result<SupplierPaymentsListResponse>.Success(
            new SupplierPaymentsListResponse(dtos, total, q.Page, q.PageSize)
        );
    }
}
