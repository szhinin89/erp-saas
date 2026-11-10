using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.GetCompanyInvoiceOrgSettings;

/// <summary>
/// Lectura administrativa de Configuración → Ventas. Devuelve los valores guardados tal cual
/// (vacío = null) y, si alguno ya no es válido según <see cref="IInvoiceDefaultsCatalogValidator"/>,
/// una advertencia explícita — nunca lo borra ni lo corrige en silencio.
/// </summary>
public sealed class GetCompanyInvoiceOrgSettingsQueryHandler
    : IRequestHandler<GetCompanyInvoiceOrgSettingsQuery, Result<CompanyInvoiceOrgSettingsDto>>
{
    private const string InactiveDefaultPriceListWarning =
        "La lista de precios predeterminada está deshabilitada; se aplica el PVP hasta que se asigne otra lista o se quite la predeterminada.";

    private readonly IOrgSettingsRepository _repo;
    private readonly IPriceListRepository _priceLists;
    private readonly IInvoiceDefaultsCatalogValidator _catalog;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetCompanyInvoiceOrgSettingsQueryHandler(
        IOrgSettingsRepository repo,
        IPriceListRepository priceLists,
        IInvoiceDefaultsCatalogValidator catalog,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repo = repo;
        _priceLists = priceLists;
        _catalog = catalog;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<CompanyInvoiceOrgSettingsDto>> Handle(
        GetCompanyInvoiceOrgSettingsQuery request,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;

        var all = await _repo.GetAllForScopeAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            cancellationToken
        );

        var lookup = all.ToDictionary(s => s.Key, s => s.Value);
        var docTypeCode = Normalize(lookup.GetValueOrDefault(OrgSettingKeys.Invoice.DefaultDocTypeCode));
        var paymentMethodCode = Normalize(
            lookup.GetValueOrDefault(OrgSettingKeys.Invoice.DefaultPaymentMethodCode)
        );
        var paymentTermId = TryParseGuid(
            lookup.GetValueOrDefault(OrgSettingKeys.Invoice.DefaultPaymentTermId)
        );

        var warnings = new List<string>();
        if (
            docTypeCode is not null
            && !await _catalog.IsValidDocTypeCodeAsync(docTypeCode, cancellationToken)
        )
            warnings.Add(InvoiceDefaultsCatalogMessages.InvalidDocType);
        if (
            paymentMethodCode is not null
            && !await _catalog.IsValidSriPaymentMethodCodeAsync(paymentMethodCode, cancellationToken)
        )
            warnings.Add(InvoiceDefaultsCatalogMessages.InvalidSriPaymentMethod);
        if (
            paymentTermId is { } termId
            && !await _catalog.IsValidPaymentTermAsync(tenantId, termId, cancellationToken)
        )
            warnings.Add(InvoiceDefaultsCatalogMessages.InvalidPaymentTerm);

        var holders = await _priceLists.GetDefaultFlagHoldersAsync(tenantId, cancellationToken);
        if (holders.Any(h => !h.IsActive))
            warnings.Add(InactiveDefaultPriceListWarning);

        return Result<CompanyInvoiceOrgSettingsDto>.Success(
            new CompanyInvoiceOrgSettingsDto(
                DefaultDocTypeCode: docTypeCode,
                DefaultSriPaymentMethodCode: paymentMethodCode,
                DefaultPaymentTermId: paymentTermId,
                DefaultPriceListId: holders.FirstOrDefault(h => h.IsActive)?.Id,
                ConfigurationWarnings: warnings,
                AllowedDocTypeCodes: await _catalog.GetAllowedDocTypeCodesAsync(cancellationToken)
            )
        );
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? TryParseGuid(string? value) => Guid.TryParse(value, out var g) ? g : null;
}
