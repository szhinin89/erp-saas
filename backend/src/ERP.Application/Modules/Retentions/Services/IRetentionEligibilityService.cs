namespace ERP.Application.Modules.Retentions.Services;

/// <summary>
/// RETENTIONS-ELIGIBILITY-01 — resultado de solo lectura de la evaluación de elegibilidad
/// tributaria para emitir una retención sobre un documento origen ya resuelto (empresa +
/// proveedor + base retenible). No representa ningún estado persistido — es un cálculo puntual,
/// nunca cacheado ni guardado. Ninguno de sus campos expone stack traces ni mensajes técnicos
/// crudos: <see cref="Reasons"/> son textos de negocio controlados, listos para mostrarse a un
/// futuro handler/UI.
/// </summary>
/// <param name="CanRetainVat">
/// True si la retención de IVA puede emitirse: la empresa retiene IVA
/// (<c>Company.WithholdsVat</c>), el proveedor no está exento, el documento tiene base
/// retenible de IVA y existe un código de retención IVA activo en catálogo.
/// </param>
/// <param name="CanRetainIncome">Igual que <see cref="CanRetainVat"/> pero para Renta (<c>Company.WithholdsRenta</c>).</param>
/// <param name="IsSupplierExempt"><c>SupplierRoleConfig.IsRetentionExempt</c> del proveedor evaluado.</param>
/// <param name="HasRetainableBase">True si el documento origen tiene base retenible de IVA y/o Renta mayor a cero.</param>
/// <param name="MissingRetentionCode">
/// True si, para al menos uno de los dos impuestos que la empresa sí está habilitada a retener,
/// no existe código de retención activo resuelto vía <c>IRetentionCodeResolver</c>/<c>SriRetentionCodes</c>.
/// Nunca implica un porcentaje o código asumido — solo bloquea esa retención con una razón clara.
/// </param>
/// <param name="IsSupplierRequiredToKeepAccounting">
/// Dato informativo de <c>SupplierRoleConfig.IsRequiredToKeepAccounting</c>, incluido tal cual
/// existe hoy — esta subfase NO lo usa para diferenciar porcentajes de retención (deuda heredada
/// documentada en RETENTIONS-MODULE-DESIGN-01.md, explícitamente fuera de alcance de E1).
/// </param>
/// <param name="SuggestedVatRetentionCode">Código de retención IVA resuelto activo, si aplica; null si no hay código configurado/activo.</param>
/// <param name="SuggestedIncomeRetentionCode">Código de retención Renta resuelto activo, si aplica; null si no hay código configurado/activo.</param>
/// <param name="Reasons">Razones de negocio, en orden de evaluación, de por qué cada impuesto es o no elegible.</param>
public sealed record RetentionEligibilityResult(
    bool CanRetainVat,
    bool CanRetainIncome,
    bool IsSupplierExempt,
    bool HasRetainableBase,
    bool MissingRetentionCode,
    bool IsSupplierRequiredToKeepAccounting,
    string? SuggestedVatRetentionCode,
    string? SuggestedIncomeRetentionCode,
    IReadOnlyList<string> Reasons
)
{
    /// <summary>True si al menos uno de los dos impuestos (IVA/Renta) es elegible para retención.</summary>
    public bool IsEligible => CanRetainVat || CanRetainIncome;
}

/// <summary>
/// RETENTIONS-ELIGIBILITY-01 — guarda de solo lectura previa a cualquier emisión de retención.
/// No persiste nada, no crea <c>RetentionDocument</c> (todavía no existe en esta subfase) y no
/// toca <c>AccountsPayable</c> ni contabilidad. La condición de agente de retención sale
/// exclusivamente de <c>Company</c>/<c>SupplierRoleConfig</c> resueltos server-side — nunca del
/// body/usuario (mismo principio ya vigente en el proyecto de no confiar en el body para
/// autoridad de tenant/company/branch).
/// </summary>
public interface IRetentionEligibilityService
{
    /// <summary>
    /// Evalúa elegibilidad de retención IVA/Renta para un proveedor dentro de una empresa, dadas
    /// las bases retenibles ya calculadas por el documento origen (p. ej. <c>ExpenseDocument</c>).
    /// </summary>
    /// <param name="tenantId">Tenant del contexto autenticado — nunca del body.</param>
    /// <param name="companyId">Empresa operativa del contexto autenticado — nunca del body.</param>
    /// <param name="supplierId">Proveedor/sujeto a retener, resuelto del documento origen.</param>
    /// <param name="vatRetainableBase">Base retenible de IVA del documento origen (p. ej. <c>ExpenseDocument.TotalVat</c>).</param>
    /// <param name="incomeRetainableBase">Base retenible de Renta del documento origen (p. ej. suma de <c>TaxableBase</c> de líneas).</param>
    Task<RetentionEligibilityResult> EvaluateAsync(
        Guid tenantId,
        Guid companyId,
        Guid supplierId,
        decimal vatRetainableBase,
        decimal incomeRetainableBase,
        CancellationToken ct = default
    );
}
