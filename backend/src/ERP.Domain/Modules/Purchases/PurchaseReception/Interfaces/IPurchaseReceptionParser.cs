using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;

/// <summary>
/// Interpreta un archivo de recepción SRI y lo convierte en <see cref="PurchaseReceptionRecord"/>.
/// Solo interpreta texto — no conoce base de datos ni EF Core.
/// </summary>
public interface IPurchaseReceptionParser
{
    Task<PurchaseReceptionParseResult> ParseAsync(Stream fileContent, CancellationToken cancellationToken = default);
}
