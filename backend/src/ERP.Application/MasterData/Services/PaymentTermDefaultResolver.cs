using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.MasterData.Services;

public sealed class PaymentTermDefaultResolver : IPaymentTermDefaultResolver
{
    private readonly IPaymentTermRepository _paymentTerms;
    private readonly ICompanyBpPurchaseSettingsRepository _purchaseSettings;
    private readonly ICompanyBpTradingSettingsRepository _tradingSettings;
    private readonly ICurrentTenant _tenant;

    public PaymentTermDefaultResolver(
        IPaymentTermRepository paymentTerms,
        ICompanyBpPurchaseSettingsRepository purchaseSettings,
        ICompanyBpTradingSettingsRepository tradingSettings,
        ICurrentTenant tenant
    )
    {
        _paymentTerms = paymentTerms;
        _purchaseSettings = purchaseSettings;
        _tradingSettings = tradingSettings;
        _tenant = tenant;
    }

    public Task<Result<PaymentTerm>> ResolveForPurchaseAsync(
        Guid supplierId,
        Guid? explicitPaymentTermId,
        CancellationToken ct = default
    ) =>
        ResolveAsync(
            explicitPaymentTermId,
            async () => (await _purchaseSettings.GetByBusinessPartnerAsync(supplierId, ct))?.PaymentTermId,
            ct
        );

    public Task<Result<PaymentTerm>> ResolveForSaleAsync(
        Guid customerId,
        Guid? explicitPaymentTermId,
        CancellationToken ct = default
    ) =>
        ResolveAsync(
            explicitPaymentTermId,
            async () => (await _tradingSettings.GetByBusinessPartnerAsync(customerId, ct))?.PaymentTermId,
            ct
        );

    /// <summary>
    /// ADR-033: cadena única compartida por Compras/Gastos (Fase 3b) y Ventas (Fase 3c) —
    /// explícito (validado activo) → default company-scoped del tercero (validado activo) →
    /// exigir selección explícita. Nunca "primer registro", nunca inferencia por días, nunca un
    /// PaymentTerm inactivo. <paramref name="resolveDefaultId"/> es la única diferencia entre
    /// compra y venta: de dónde sale el Guid del default (CompanyBpPurchaseSettings vs
    /// CompanyBpTradingSettings) — el resto de la regla es idéntico y vive en un solo lugar.
    /// </summary>
    private async Task<Result<PaymentTerm>> ResolveAsync(
        Guid? explicitPaymentTermId,
        Func<Task<Guid?>> resolveDefaultId,
        CancellationToken ct
    )
    {
        var tenantId = _tenant.TenantId;

        if (explicitPaymentTermId is { } explicitId)
        {
            var explicitPt = await _paymentTerms.GetByIdAsync(tenantId, explicitId, ct);
            if (explicitPt is null)
                return Result<PaymentTerm>.ValidationFailure("La condición de pago no existe.");
            if (!explicitPt.IsActive)
                return Result<PaymentTerm>.ValidationFailure(
                    "La condición de pago se encuentra inactiva."
                );
            return Result<PaymentTerm>.Success(explicitPt);
        }

        var defaultId = await resolveDefaultId();
        if (defaultId is { } dId)
        {
            var defaultPt = await _paymentTerms.GetByIdAsync(tenantId, dId, ct);
            if (defaultPt is not null && defaultPt.IsActive)
                return Result<PaymentTerm>.Success(defaultPt);
        }

        return Result<PaymentTerm>.ValidationFailure(
            "Debe seleccionar una condición de pago; no hay una configurada para esta empresa."
        );
    }
}
