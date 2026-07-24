using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;

public sealed class GetSalesInvoiceDefaultsQueryHandler
    : IRequestHandler<GetSalesInvoiceDefaultsQuery, Result<SalesInvoiceDefaultsDto>>
{
    private readonly IOrgSettingsRepository   _orgRepo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly ICurrentTenant           _currentTenant;
    private readonly ICurrentCompany          _currentCompany;

    public GetSalesInvoiceDefaultsQueryHandler(
        IOrgSettingsRepository   orgRepo,
        IEmissionPointRepository epRepo,
        ICurrentTenant           currentTenant,
        ICurrentCompany          currentCompany)
    {
        _orgRepo        = orgRepo;
        _epRepo         = epRepo;
        _currentTenant  = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<SalesInvoiceDefaultsDto>> Handle(
        GetSalesInvoiceDefaultsQuery query, CancellationToken cancellationToken)
    {
        var tenantId  = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;

        // Leer configuración de empresa: DocTypeCode, PaymentMethodCode, PaymentTermId
        var orgSettings = await _orgRepo.GetAllForScopeAsync(
            tenantId, companyId, OrgScope.Company, companyId, cancellationToken);

        var orgLookup = orgSettings.ToDictionary(s => s.Key, s => s.Value);

        string? Resolve(string key)
            => orgLookup.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        Guid? ResolveGuid(string key)
        {
            if (orgLookup.TryGetValue(key, out var v) && Guid.TryParse(v, out var g)) return g;
            return null;
        }

        var docTypeCode   = Resolve(OrgSettingKeys.Invoice.DefaultDocTypeCode);
        var payMethodCode = Resolve(OrgSettingKeys.Invoice.DefaultPaymentMethodCode);
        var paymentTermId = ResolveGuid(OrgSettingKeys.Invoice.DefaultPaymentTermId);

        // DefaultEmissionPointId: resuelto siempre desde EmissionPoint.IsDefault (única fuente).
        var defaultEp = await _epRepo.GetDefaultForCompanyAsync(tenantId, companyId, cancellationToken);
        var emissionPointId = defaultEp?.Id;

        // DefaultWarehouseId: propietario Sucursal (OrgScope.Branch).
        // No hay contexto de sucursal en esta query → null; el frontend usa whsData[0] como fallback.

        return Result<SalesInvoiceDefaultsDto>.Success(new SalesInvoiceDefaultsDto(
            DefaultDocTypeCode:          docTypeCode,
            DefaultSriPaymentMethodCode: payMethodCode,
            DefaultEmissionPointId:      emissionPointId,
            DefaultWarehouseId:          null,
            DefaultPaymentTermId:        paymentTermId,
            FallbackDocTypeCode:          SriSettings.FallbackDocTypeCode,
            FallbackSriPaymentMethodCode: SriSettings.FallbackSriPaymentMethodCode));
    }
}
