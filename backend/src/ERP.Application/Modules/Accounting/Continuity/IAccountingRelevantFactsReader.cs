namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — hechos operativos contablemente relevantes ya registrados. Una vigencia
/// Enabled pasada solo es válida si ninguno quedaría sujeto retroactivamente (accounting.md §20).
/// </summary>
public interface IAccountingRelevantFactsReader
{
    /// <summary>Existe algún hecho no borrador con fecha de negocio igual o posterior a <paramref name="date"/>.</summary>
    Task<bool> ExistsOnOrAfterAsync(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct);
}
