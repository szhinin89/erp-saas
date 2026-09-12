using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
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

/// <summary>
/// PAYABLES-BRANCH-SCOPE-DECISION-01 — CxP/pagos a proveedor son company-level, no branch-level
/// (decisión de negocio): un pago a proveedor pertenece a la empresa, no a una sucursal operativa
/// concreta (el <c>BranchId</c> que registra el pago es solo trazabilidad de origen, no un filtro de
/// acceso). Marcado <see cref="ICompanyScopedRequest"/> (no <c>IBranchScopedRequest</c>) para no
/// exigir sucursal activa al consultar.
/// </summary>
public sealed record GetSupplierPaymentByIdQuery(Guid Id)
    : IRequest<Result<SupplierPaymentDto>>,
        ICompanyScopedRequest;

/// <summary>PAYABLES-BRANCH-SCOPE-DECISION-01 — ver <see cref="GetSupplierPaymentByIdQuery"/>.</summary>
public sealed record GetSupplierPaymentsListQuery(
    Guid? SupplierId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<Result<SupplierPaymentsListResponse>>, ICompanyScopedRequest;

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetSupplierPaymentByIdHandler
    : IRequestHandler<GetSupplierPaymentByIdQuery, Result<SupplierPaymentDto>>
{
    private readonly ISupplierPaymentRepository _repo;
    private readonly IAccountsPayableRepository _accountsPayables;
    private readonly ICurrentTenant _t;

    public GetSupplierPaymentByIdHandler(
        ISupplierPaymentRepository repo,
        IAccountsPayableRepository accountsPayables,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _accountsPayables = accountsPayables;
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

        var displayInfo = await ResolveInstallmentDisplayInfoAsync(payment, ct);
        return Result<SupplierPaymentDto>.Success(SupplierPaymentDtoMapper.ToDto(payment, displayInfo));
    }

    /// <summary>
    /// SUPPLIER-PAYMENT-DETAIL-APPLICATION-LINE-DISPLAY-NAMES-01 — proyección de solo lectura:
    /// resuelve, por cada cuota aplicada, el documento/cuota origen contra <c>AccountsPayable</c>
    /// (mismo repositorio/método — <see cref="IAccountsPayableRepository.GetByInstallmentIdAsync"/>
    /// — que ya usa <c>RegisterSupplierPaymentUseCases</c> para validar la cuota al registrar el
    /// pago). Si una cuota ya no puede resolverse (caso excepcional, nunca esperado en operación
    /// normal), se omite silenciosamente — el detalle del pago nunca debe romperse por esto, el
    /// frontend cae a un fallback técnico con el Id crudo.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, InstallmentDisplayInfo>> ResolveInstallmentDisplayInfoAsync(
        SupplierPayment payment,
        CancellationToken ct
    )
    {
        var result = new Dictionary<Guid, InstallmentDisplayInfo>();
        var payablesByInstallment = new Dictionary<Guid, AccountsPayable>();

        foreach (var line in payment.ApplicationLines)
        {
            var installmentId = line.AccountsPayableInstallmentId;
            if (result.ContainsKey(installmentId))
                continue;

            if (!payablesByInstallment.TryGetValue(installmentId, out var payable))
            {
                payable = await _accountsPayables.GetByInstallmentIdAsync(_t.TenantId, installmentId, ct);
                if (payable is not null)
                    payablesByInstallment[installmentId] = payable;
            }

            var installment = payable?.Installments.FirstOrDefault(i => i.Id == installmentId);
            if (payable is null || installment is null)
                continue;

            result[installmentId] = new InstallmentDisplayInfo(
                payable.DocumentNumber,
                installment.InstallmentNumber,
                installment.DueDate,
                payable.IssueDate,
                payable.OriginType.ToString()
            );
        }

        return result;
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
