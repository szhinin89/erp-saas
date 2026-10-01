using ERP.Application.Common;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Retentions.Services;
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
/// Vista previa de la retención de una compra en BORRADOR (ZH-PURCHASE-RETENTION-CONFIRM-01): la
/// retención se define antes de confirmar y se emite dentro de <see cref="ConfirmPurchaseCommand"/>
/// (RetentionIntent), así que sobre una compra ya confirmada no hay nada que previsualizar.
/// Cierre de gap BranchScopeBehavior: opera sobre un PurchaseInvoice puntual, igual que
/// ConfirmPurchaseCommand/CancelPurchaseCommand/ApplyGlobalDiscountCommand (todos IBranchScopedRequest).
/// </summary>
public sealed record CalculateRetentionQuery(Guid PurchaseInvoiceId)
    : IRequest<Result<RetentionPreviewDto>>,
        IBranchScopedRequest;

// ── Handler ─────────────────────────────────────────────────────────────

/// <summary>
/// ZH-PURCHASE-RETENTION-CONFIRM-01 — un solo motor de elegibilidad: los candidatos salen de
/// <see cref="IRetentionEligibilityService"/> (el mismo que revalida <c>RetentionIssuer</c> al
/// emitir: empresa agente de retención de IVA/Renta, proveedor exento, base retenible, defaults
/// activos del proveedor en la empresa del contexto autenticado contra el catálogo SRI) sobre las
/// mismas bases que la emisión (<see cref="PurchaseRetentionSource"/>). <see cref="RetentionCalculator"/>
/// solo aporta el cálculo de los montos propuestos (base × %), que el usuario ve precargados.
/// </summary>
public sealed class CalculateRetentionHandler
    : IRequestHandler<CalculateRetentionQuery, Result<RetentionPreviewDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IRetentionEligibilityService _eligibility;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;

    public CalculateRetentionHandler(
        IPurchaseInvoiceRepository repo,
        IRetentionEligibilityService eligibility,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b
    )
    {
        _repo = repo;
        _eligibility = eligibility;
        _t = t;
        _c = c;
        _b = b;
    }

    public async Task<Result<RetentionPreviewDto>> Handle(
        CalculateRetentionQuery q,
        CancellationToken ct
    )
    {
        // GetByIdAsync filtra tenant+company; el branch se valida explícitamente, igual que
        // GetPurchaseByIdHandler (ZH-PURCHASES-RETENTION-OWNERSHIP-01).
        var inv = await _repo.GetByIdAsync(_t.TenantId, q.PurchaseInvoiceId, ct);
        if (inv is null || inv.BranchId != _b.BranchId)
            return Result<RetentionPreviewDto>.NotFound("Compra no encontrada.");

        if (inv.Status != ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft)
            return Result<RetentionPreviewDto>.ValidationFailure(
                "La retención se define antes de confirmar: solo se calcula sobre compras en borrador."
            );

        var vatBase = PurchaseRetentionSource.VatRetainableBase(inv);
        var incomeBase = PurchaseRetentionSource.IncomeRetainableBase(inv);
        var eligibility = await _eligibility.EvaluateAsync(
            _t.TenantId,
            _c.CompanyId,
            inv.SupplierId,
            vatBase,
            incomeBase,
            ct
        );

        var candidates = eligibility
            .Candidates.Select(c => new RetentionCandidate(
                c.TaxType,
                c.RetentionCode,
                c.RetentionPct,
                c.RetentionCodeName
            ))
            .ToList();
        var result = RetentionCalculator.Calculate(
            vatBase,
            incomeBase,
            eligibility.IsSupplierExempt,
            candidates
        );

        // Sin líneas: el motivo es el de la elegibilidad (empresa no agente, proveedor exento, sin
        // base, sin código activo), nunca uno distinto calculado aparte.
        string? skipReason = null;
        if (result.Lines.Count == 0)
            skipReason = eligibility.Reasons.Count > 0
                ? string.Join(" ", eligibility.Reasons)
                : result.SkipReason;

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
            skipReason
        );

        return Result<RetentionPreviewDto>.Success(dto);
    }
}
