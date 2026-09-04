using ERP.Application.Common;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Retentions.Enums;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── DTO ─────────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-ELIGIBILITY-01 — resultado expuesto a Application/API. <see cref="IsSupportedInThisPhase"/>
/// distingue explícitamente "no soportado todavía por esta subfase" (p. ej. <c>PurchaseInvoice</c>,
/// <c>Manual</c>) de "no elegible por regla fiscal" (<see cref="RetentionEligibilityResult.IsEligible"/>
/// en false con <c>Reasons</c> pobladas) — nunca deben confundirse.
/// </summary>
public sealed record RetentionEligibilityDto(
    RetentionSourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    bool IsSupportedInThisPhase,
    bool CanRetainVat,
    bool CanRetainIncome,
    bool IsSupplierExempt,
    bool HasRetainableBase,
    bool MissingRetentionCode,
    bool IsSupplierRequiredToKeepAccounting,
    string? SuggestedVatRetentionCode,
    string? SuggestedIncomeRetentionCode,
    IReadOnlyList<string> Reasons
);

// ── Query ───────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-ELIGIBILITY-01 — evalúa si el ERP puede emitir una retención (IVA y/o Renta) para
/// un documento origen dado, ANTES de que exista <c>RetentionDocument</c> (todavía no se crea en
/// esta subfase). Solo lectura: no persiste nada, no toca CxP ni contabilidad.
///
/// Branch-scoped: mismo criterio que <c>CalculateRetentionQuery</c> de Purchases — operar sobre
/// un documento puntual de un módulo ya branch-scoped exige sucursal activa.
/// </summary>
public sealed record GetRetentionEligibilityQuery(
    RetentionSourceDocumentType SourceDocumentType,
    Guid SourceDocumentId
) : IRequest<Result<RetentionEligibilityDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class GetRetentionEligibilityValidator : AbstractValidator<GetRetentionEligibilityQuery>
{
    public GetRetentionEligibilityValidator()
    {
        RuleFor(x => x.SourceDocumentId).NotEmpty();
        RuleFor(x => x.SourceDocumentType).IsInEnum();
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class GetRetentionEligibilityHandler
    : IRequestHandler<GetRetentionEligibilityQuery, Result<RetentionEligibilityDto>>
{
    private readonly IExpenseDocumentRepository _expenseRepo;
    private readonly IRetentionEligibilityService _eligibilityService;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;

    public GetRetentionEligibilityHandler(
        IExpenseDocumentRepository expenseRepo,
        IRetentionEligibilityService eligibilityService,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch
    )
    {
        _expenseRepo = expenseRepo;
        _eligibilityService = eligibilityService;
        _tenant = tenant;
        _company = company;
        _branch = branch;
    }

    public async Task<Result<RetentionEligibilityDto>> Handle(
        GetRetentionEligibilityQuery q,
        CancellationToken ct
    )
    {
        // PurchaseInvoice/Manual: fuera de alcance de RETENTIONS-ELIGIBILITY-01 (ver
        // RETENTIONS-MODULE-DESIGN-01.md — Compras sigue usando IssuedWithholding sin cambios en
        // E1, y Manual está reservado sin implementación). Resultado explícito de "no soportado",
        // nunca confundido con "no elegible por regla fiscal": ningún campo de elegibilidad se
        // evalúa (todos quedan en su valor neutro/false) y la única señal es
        // IsSupportedInThisPhase=false + la razón en Reasons.
        if (q.SourceDocumentType != RetentionSourceDocumentType.ExpenseDocument)
        {
            return Result<RetentionEligibilityDto>.Success(
                new RetentionEligibilityDto(
                    q.SourceDocumentType,
                    q.SourceDocumentId,
                    IsSupportedInThisPhase: false,
                    CanRetainVat: false,
                    CanRetainIncome: false,
                    IsSupplierExempt: false,
                    HasRetainableBase: false,
                    MissingRetentionCode: false,
                    IsSupplierRequiredToKeepAccounting: false,
                    SuggestedVatRetentionCode: null,
                    SuggestedIncomeRetentionCode: null,
                    Reasons: new[]
                    {
                        $"NotSupportedInThisPhase: la evaluación de elegibilidad para "
                            + $"{q.SourceDocumentType} no está implementada en RETENTIONS-ELIGIBILITY-01.",
                    }
                )
            );
        }

        var tid = _tenant.TenantId;

        // Fail-closed: GetByIdAsync ya filtra por TenantId (ForOperationalScope + CompanyId del
        // contexto activo); el BranchId se verifica explícitamente aquí porque el repositorio no
        // lo filtra — mismo patrón exacto que CancelExpenseDocumentHandler
        // (document is null || document.BranchId != _branch.BranchId ⇒ NotFound), nunca
        // IgnoreQueryFilters.
        var document = await _expenseRepo.GetByIdAsync(tid, q.SourceDocumentId, ct);
        if (document is null || document.BranchId != _branch.BranchId)
            return Result<RetentionEligibilityDto>.NotFound("Documento de gasto no encontrado.");

        if (document.Status != ExpenseStatus.Confirmed)
            return Result<RetentionEligibilityDto>.ValidationFailure(
                "Solo se puede evaluar elegibilidad de retención sobre gastos confirmados."
            );

        var vatRetainableBase = document.TotalVat;
        var incomeRetainableBase = document.Lines.Sum(l => l.TaxableBase);

        var eligibility = await _eligibilityService.EvaluateAsync(
            tid,
            _company.CompanyId,
            document.SupplierId,
            vatRetainableBase,
            incomeRetainableBase,
            ct
        );

        return Result<RetentionEligibilityDto>.Success(
            new RetentionEligibilityDto(
                q.SourceDocumentType,
                q.SourceDocumentId,
                IsSupportedInThisPhase: true,
                eligibility.CanRetainVat,
                eligibility.CanRetainIncome,
                eligibility.IsSupplierExempt,
                eligibility.HasRetainableBase,
                eligibility.MissingRetentionCode,
                eligibility.IsSupplierRequiredToKeepAccounting,
                eligibility.SuggestedVatRetentionCode,
                eligibility.SuggestedIncomeRetentionCode,
                eligibility.Reasons
            )
        );
    }
}
