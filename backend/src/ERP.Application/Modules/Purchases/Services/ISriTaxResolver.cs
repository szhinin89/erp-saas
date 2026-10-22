using ERP.Application.Common.Services;

namespace ERP.Application.Modules.Purchases.Services;

public interface ISriTaxResolver : Common.Services.ISriTaxResolver
{
    // Reconocimiento documental exclusivo de Compras; no habilita selección manual.
    Task<SriTaxCatalogEntry?> GetXmlIceCatalogEntryAsync(string iceCode, CancellationToken ct = default);
}
