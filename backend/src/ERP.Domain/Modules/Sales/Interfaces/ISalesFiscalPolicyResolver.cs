namespace ERP.Domain.Modules.Sales.Interfaces;

/// <summary>
/// Origen del valor efectivo de <see cref="SalesFiscalPolicyResult.ConsumerFinalMaxAmount"/>.
/// </summary>
public enum ConsumerFinalMaxAmountSource
{
    /// <summary>Configurado explícitamente por la empresa (OrgSetting), incluyendo 0.00.</summary>
    Manual,

    /// <summary>Sin configuración manual — calculado desde el régimen tributario de la empresa.</summary>
    TaxRegimeDefault,

    /// <summary>Sin configuración manual y régimen null/no reconocido — valor seguro por defecto.</summary>
    Fallback,
}

/// <summary>
/// Política fiscal efectiva de Consumidor Final para la empresa activa (tenant+company del
/// contexto de ejecución). <see cref="BlockConsumerFinalCredit"/> es siempre <c>true</c> — regla
/// fija de negocio, nunca editable — se expone igual para que los consumidores no la hardcodeen.
/// </summary>
public sealed record SalesFiscalPolicyResult(
    bool BlockConsumerFinalCredit,
    decimal ConsumerFinalMaxAmount,
    ConsumerFinalMaxAmountSource ConsumerFinalMaxAmountSource,
    string? TaxRegimeCode
);

/// <summary>
/// Único punto de acceso a la política fiscal de Consumidor Final. Todo módulo que necesite
/// bloquear crédito o validar el monto máximo de Consumidor Final debe consumir este resolver —
/// nunca reimplementar la regla ni leer OrgSettings/Company directamente para este propósito.
/// Tenant/Company se resuelven del contexto de ejecución actual (igual que IOrgConfigResolver).
/// </summary>
public interface ISalesFiscalPolicyResolver
{
    Task<SalesFiscalPolicyResult> GetEffectivePolicyAsync(CancellationToken cancellationToken = default);
}
