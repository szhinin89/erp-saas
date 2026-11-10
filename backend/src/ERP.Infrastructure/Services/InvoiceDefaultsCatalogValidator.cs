using ERP.Application.Common.Services;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;
using ERP.Domain.Modules.SriCatalogs.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>SALES-PREFERENCES-INTEGRITY-01: única implementación de <see cref="IInvoiceDefaultsCatalogValidator"/>.</summary>
public sealed class InvoiceDefaultsCatalogValidator : IInvoiceDefaultsCatalogValidator
{
    /// <summary>Política vigente: el default de ventas solo puede ser Factura (01).</summary>
    private static readonly string[] SalesDefaultDocTypeCodes = [SriDocumentTypeCodes.Invoice];

    private readonly ISriDocTypeCatalogResolver _docTypes;
    private readonly ISriCatalogLookupRepository _sriCatalog;
    private readonly IPaymentTermRepository _paymentTerms;

    public InvoiceDefaultsCatalogValidator(
        ISriDocTypeCatalogResolver docTypes,
        ISriCatalogLookupRepository sriCatalog,
        IPaymentTermRepository paymentTerms
    )
    {
        _docTypes = docTypes;
        _sriCatalog = sriCatalog;
        _paymentTerms = paymentTerms;
    }

    public async Task<IReadOnlyList<string>> GetAllowedDocTypeCodesAsync(
        CancellationToken ct = default
    )
    {
        var allowed = new List<string>(SalesDefaultDocTypeCodes.Length);
        foreach (var code in SalesDefaultDocTypeCodes)
            if (await _docTypes.IsActiveDocTypeAsync(code, ct))
                allowed.Add(code);
        return allowed;
    }

    public async Task<bool> IsValidDocTypeCodeAsync(string code, CancellationToken ct = default) =>
        (await GetAllowedDocTypeCodesAsync(ct)).Contains(code, StringComparer.Ordinal);

    public Task<bool> IsValidSriPaymentMethodCodeAsync(
        string code,
        CancellationToken ct = default
    ) => _sriCatalog.PaymentMethodCodeExistsActiveAsync(code, ct);

    public async Task<bool> IsValidPaymentTermAsync(
        Guid tenantId,
        Guid paymentTermId,
        CancellationToken ct = default
    )
    {
        var term = await _paymentTerms.GetByIdAsync(tenantId, paymentTermId, ct);
        return term is not null && term.IsActive;
    }
}
