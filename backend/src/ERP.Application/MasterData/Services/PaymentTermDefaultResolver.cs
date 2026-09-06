using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.MasterData.Services;

public sealed class PaymentTermDefaultResolver : IPaymentTermDefaultResolver
{
    private readonly IPaymentTermRepository _paymentTerms;
    private readonly ICompanyBpPurchaseSettingsRepository _purchaseSettings;
    private readonly ICurrentTenant _tenant;

    public PaymentTermDefaultResolver(
        IPaymentTermRepository paymentTerms,
        ICompanyBpPurchaseSettingsRepository purchaseSettings,
        ICurrentTenant tenant
    )
    {
        _paymentTerms = paymentTerms;
        _purchaseSettings = purchaseSettings;
        _tenant = tenant;
    }

    public async Task<Result<PaymentTerm>> ResolveForPurchaseAsync(
        Guid supplierId,
        Guid? explicitPaymentTermId,
        CancellationToken ct = default
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

        var companyDefault = await _purchaseSettings.GetByBusinessPartnerAsync(supplierId, ct);
        if (companyDefault?.PaymentTermId is { } defaultId)
        {
            var defaultPt = await _paymentTerms.GetByIdAsync(tenantId, defaultId, ct);
            if (defaultPt is not null && defaultPt.IsActive)
                return Result<PaymentTerm>.Success(defaultPt);
        }

        return Result<PaymentTerm>.ValidationFailure(
            "Debe seleccionar una condición de pago; este proveedor no tiene una configurada "
                + "para esta empresa."
        );
    }
}
