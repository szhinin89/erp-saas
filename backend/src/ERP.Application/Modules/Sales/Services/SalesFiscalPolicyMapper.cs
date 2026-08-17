using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.Policies;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// Único punto de conversión de <see cref="SalesFiscalPolicyResult"/> (dominio) a
/// <see cref="SalesFiscalPolicyDto"/> (contrato de API) — usado tanto por el endpoint de
/// Fiscal/Tributario (Company Settings) como por SalesRuntimeContext, para que ambos expongan
/// exactamente los mismos valores y mensajes sin duplicar el mapeo.
/// </summary>
public static class SalesFiscalPolicyMapper
{
    public static SalesFiscalPolicyDto ToDto(SalesFiscalPolicyResult policy) =>
        new(
            policy.BlockConsumerFinalCredit,
            policy.ConsumerFinalMaxAmount,
            policy.ConsumerFinalMaxAmountSource.ToString(),
            policy.TaxRegimeCode,
            SalesFiscalPolicyMessages.CreditBlockedMessage,
            SalesFiscalPolicyMessages.AmountExceededMessage(policy.ConsumerFinalMaxAmount)
        );
}
