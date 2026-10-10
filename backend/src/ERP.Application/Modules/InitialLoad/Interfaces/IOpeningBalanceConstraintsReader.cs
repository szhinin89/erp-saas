using ERP.Domain.Modules.Company.Entities;

namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// IL-5A — lectura de solo consulta de lo que gobierna <c>Company.OpeningBalanceDate</c> en la
/// empresa operativa: si ya existen operaciones reales y las fechas de corte de las cargas
/// iniciales confirmadas. Company scope por filtros globales fail-closed.
/// </summary>
public interface IOpeningBalanceConstraintsReader
{
    /// <param name="currentOpeningBalanceDate">
    /// Fecha vigente de la empresa: es el corte de los saldos iniciales de CxC ya confirmados (que
    /// no guardan su propio corte).
    /// </param>
    Task<OpeningBalanceDateConstraints> GetAsync(DateOnly? currentOpeningBalanceDate, CancellationToken ct);

    /// <summary>
    /// <c>Company.OpeningBalanceDate</c> de la empresa operativa: único corte de apertura de saldos
    /// (SSOT) que leen las cargas de saldos iniciales (CxC IL-5, CxP IL-6). Null si aún no se definió.
    /// </summary>
    Task<DateOnly?> GetOpeningBalanceDateAsync(CancellationToken ct);
}
