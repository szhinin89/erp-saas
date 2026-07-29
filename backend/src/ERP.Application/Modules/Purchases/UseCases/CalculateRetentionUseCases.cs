using ERP.Application.Common;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.Services;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── DTO ─────────────────────────────────────────────────────────────────

public sealed record RetentionPreviewDto(
    IReadOnlyList<RetentionLineDto> Lines,
    decimal TotalRetainedVat,
    decimal TotalRetainedIncome,
    decimal TotalRetainedIsd,
    decimal TotalRetained,
    string? SkipReason
);

public sealed record RetentionLineDto(
    string TaxType,
    string RetentionCode,
    string RetentionCodeName,
    decimal TaxableBase,
    decimal RetentionPct,
    decimal AmountRetained
);

// ── Query ───────────────────────────────────────────────────────────────

/// <summary>
/// Cierre de gap BranchScopeBehavior: opera sobre un PurchaseInvoice puntual, igual que
/// ConfirmPurchaseCommand/CancelPurchaseCommand/ApplyGlobalDiscountCommand (todos
/// IBranchScopedRequest) — exigir sucursal activa aquí es consistente con el resto de
/// operaciones de una misma factura de compra.
/// </summary>
public sealed record CalculateRetentionQuery(Guid PurchaseInvoiceId)
    : IRequest<Result<RetentionPreviewDto>>,
        IBranchScopedRequest;

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class CalculateRetentionHandler
    : IRequestHandler<CalculateRetentionQuery, Result<RetentionPreviewDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IRetentionCodeResolver _retCodeResolver;
    private readonly ICurrentTenant _t;

    public CalculateRetentionHandler(
        IPurchaseInvoiceRepository repo,
        IBusinessPartnerRoleRepository roleRepo,
        IRetentionCodeResolver retCodeResolver,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _roleRepo = roleRepo;
        _retCodeResolver = retCodeResolver;
        _t = t;
    }

    public async Task<Result<RetentionPreviewDto>> Handle(
        CalculateRetentionQuery q,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, q.PurchaseInvoiceId, ct);
        if (inv is null)
            return Result<RetentionPreviewDto>.NotFound("Compra no encontrada.");

        if (inv.Status != ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed)
            return Result<RetentionPreviewDto>.ValidationFailure(
                "Solo se pueden calcular retenciones de compras confirmadas."
            );

        var supplierRole = await _roleRepo.GetByTypeAsync(inv.SupplierId, RoleType.Supplier, ct);
        var config = supplierRole?.SupplierConfig;

        var isExempt = config?.IsRetentionExempt ?? false;

        // Resolver códigos y tasas desde catálogo SRI
        string? vatCode = config?.DefaultRetentionVatCode;
        decimal vatPct = 0;
        string? vatName = null;

        if (!string.IsNullOrWhiteSpace(vatCode))
        {
            var info = await _retCodeResolver.GetRetentionCodeAsync(vatCode, "IVA", ct);
            if (info is not null)
            {
                vatPct = info.Percentage;
                vatName = info.Name;
            }
            else
                vatCode = null;
        }

        string? incomeCode = config?.DefaultRetentionIncomeCode;
        decimal incomePct = 0;
        string? incomeName = null;

        if (!string.IsNullOrWhiteSpace(incomeCode))
        {
            var info = await _retCodeResolver.GetRetentionCodeAsync(incomeCode, "RENTA", ct);
            if (info is not null)
            {
                incomePct = info.Percentage;
                incomeName = info.Name;
            }
            else
                incomeCode = null;
        }

        // Base imponible renta = Sum(TaxableBase) de las líneas (subtotal - descuento)
        var taxableBaseIncome = inv.Lines.Sum(l => l.TaxableBase);

        var result = RetentionCalculator.Calculate(
            inv.TotalVat,
            taxableBaseIncome,
            isExempt,
            vatCode,
            vatPct,
            vatName,
            incomeCode,
            incomePct,
            incomeName
        );

        var dto = new RetentionPreviewDto(
            result
                .Lines.Select(l => new RetentionLineDto(
                    l.TaxType,
                    l.RetentionCode,
                    l.RetentionCodeName,
                    l.TaxableBase,
                    l.RetentionPct,
                    l.AmountRetained
                ))
                .ToList(),
            result.TotalRetainedVat,
            result.TotalRetainedIncome,
            result.TotalRetainedIsd,
            result.TotalRetained,
            result.SkipReason
        );

        return Result<RetentionPreviewDto>.Success(dto);
    }
}
