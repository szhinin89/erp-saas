using ERP.Application.Common;
using ERP.Domain.MasterData.Constants;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// SALES-SETTLEMENT-CREDIT-01 — colaborador de <c>CreateSalesDraftHandler</c>/
/// <c>UpdateSalesDraftHandler</c> para el caso en que NO hay PaymentTermId explícito ni default
/// de cliente resuelto (<c>IPaymentTermDefaultResolver.ResolveForSaleAsync</c> ya se intentó y
/// falló, o el saldo está cubierto y no hace falta intentarlo). Dos responsabilidades separadas:
///
/// 1) <see cref="ResolveCompanyOrManualAsync"/>: cuando queda saldo pendiente, decide si existe
///    una regla de crédito válida — orden b) fecha de vencimiento manual, c) cronograma manual,
///    e) default de empresa (<c>org_settings.invoice.default_payment_term_id</c>, vía
///    <see cref="IInvoiceDefaultsResolver"/>). Si ninguna aplica, falla con el mensaje de negocio
///    aprobado. Los órdenes a) explícito y d) default de cliente se resuelven en el handler
///    directamente vía <see cref="ERP.Application.MasterData.Services.IPaymentTermDefaultResolver"/>
///    — no se duplican aquí.
///
/// 2) <see cref="GetCashFallbackAsync"/>: PaymentTerm "Contado" sembrado por el sistema
///    (<see cref="PaymentTermCodes.Cash"/>) — único fallback permitido para el snapshot de la
///    factura cuando el saldo pendiente es cero (o queda cubierto por dueDate/schedule manual, que
///    no aportan un PaymentTerm concreto) y no hay condición de pago explícita ni default
///    resuelto. Nunca "primer PaymentTerm activo del catálogo" (ADR-033).
/// </summary>
public interface ISalesCreditRequirementPolicy
{
    Task<Result<PaymentTerm?>> ResolveCompanyOrManualAsync(
        DateOnly? dueDate,
        bool hasManualSchedule,
        CancellationToken ct = default
    );

    Task<Result<PaymentTerm>> GetCashFallbackAsync(CancellationToken ct = default);
}

public sealed class SalesCreditRequirementPolicy : ISalesCreditRequirementPolicy
{
    /// <summary>
    /// Mensaje de negocio único de esta regla (SALES-SETTLEMENT-CREDIT-01) — 422 cuando queda
    /// saldo pendiente y no hay fecha de vencimiento, cronograma, ni condición de pago (explícita
    /// o default) que lo respalde.
    /// </summary>
    public const string MissingCreditRuleMessage =
        "Debe definir una fecha de vencimiento, cuotas o una condición de pago para el saldo pendiente.";

    private readonly IPaymentTermRepository _paymentTerms;
    private readonly IInvoiceDefaultsResolver _invoiceDefaults;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;

    public SalesCreditRequirementPolicy(
        IPaymentTermRepository paymentTerms,
        IInvoiceDefaultsResolver invoiceDefaults,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch
    )
    {
        _paymentTerms = paymentTerms;
        _invoiceDefaults = invoiceDefaults;
        _tenant = tenant;
        _company = company;
        _branch = branch;
    }

    public async Task<Result<PaymentTerm?>> ResolveCompanyOrManualAsync(
        DateOnly? dueDate,
        bool hasManualSchedule,
        CancellationToken ct = default
    )
    {
        // b) Fecha de vencimiento manual — el handler construye una cuota única con este valor;
        // esta política no resuelve ningún PaymentTerm concreto para ese caso (null = "manual").
        if (dueDate.HasValue)
            return Result<PaymentTerm?>.Success(null);

        // c) Cronograma manual — el handler ya recibió cmd.Schedule y lo usa tal cual.
        if (hasManualSchedule)
            return Result<PaymentTerm?>.Success(null);

        // e) Default de empresa (org_settings.invoice.default_payment_term_id).
        var tenantId = _tenant.TenantId;
        var defaults = await _invoiceDefaults.GetAsync(
            tenantId,
            _company.CompanyId,
            _branch.BranchId,
            ct
        );
        if (defaults.DefaultPaymentTermId is { } companyDefaultId)
        {
            var pt = await _paymentTerms.GetByIdAsync(tenantId, companyDefaultId, ct);
            if (pt is not null && pt.IsActive)
                return Result<PaymentTerm?>.Success(pt);
        }

        return Result<PaymentTerm?>.ValidationFailure(MissingCreditRuleMessage);
    }

    public async Task<Result<PaymentTerm>> GetCashFallbackAsync(CancellationToken ct = default)
    {
        var pt = await _paymentTerms.GetByCodeAsync(_tenant.TenantId, PaymentTermCodes.Cash, ct);
        if (pt is null || !pt.IsActive)
            return Result<PaymentTerm>.ValidationFailure(
                "No existe una condición de pago de contado activa configurada para esta empresa. "
                    + "Configure una condición de pago antes de continuar."
            );
        return Result<PaymentTerm>.Success(pt);
    }
}
