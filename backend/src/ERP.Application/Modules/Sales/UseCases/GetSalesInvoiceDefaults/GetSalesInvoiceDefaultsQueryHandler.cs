using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;

/// <summary>
/// CONFIG-FOUNDATION-P1-04: ya no lee IOrgSettingsRepository/OrgSettingKeys directamente —
/// delega toda la resolución en IInvoiceDefaultsResolver (Domain) y solo ensambla el DTO de
/// salida (agregando las constantes Fallback*, que no son org_settings, son constantes de
/// plataforma vía SriSettings).
/// </summary>
public sealed class GetSalesInvoiceDefaultsQueryHandler
    : IRequestHandler<GetSalesInvoiceDefaultsQuery, Result<SalesInvoiceDefaultsDto>>
{
    private readonly IInvoiceDefaultsResolver _invoiceDefaultsResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentBranch _currentBranch;

    public GetSalesInvoiceDefaultsQueryHandler(
        IInvoiceDefaultsResolver invoiceDefaultsResolver,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentBranch currentBranch
    )
    {
        _invoiceDefaultsResolver = invoiceDefaultsResolver;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentBranch = currentBranch;
    }

    public async Task<Result<SalesInvoiceDefaultsDto>> Handle(
        GetSalesInvoiceDefaultsQuery query,
        CancellationToken cancellationToken
    )
    {
        var branchId = _currentBranch.HasBranchContext ? _currentBranch.BranchId : (Guid?)null;

        var defaults = await _invoiceDefaultsResolver.GetAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            branchId,
            cancellationToken
        );

        return Result<SalesInvoiceDefaultsDto>.Success(
            new SalesInvoiceDefaultsDto(
                DefaultDocTypeCode: defaults.DefaultDocTypeCode,
                DefaultSriPaymentMethodCode: defaults.DefaultSriPaymentMethodCode,
                DefaultEmissionPointId: defaults.DefaultEmissionPointId,
                DefaultWarehouseId: defaults.DefaultWarehouseId,
                DefaultPaymentTermId: defaults.DefaultPaymentTermId,
                FallbackDocTypeCode: SriSettings.FallbackDocTypeCode,
                FallbackSriPaymentMethodCode: SriSettings.FallbackSriPaymentMethodCode,
                DefaultWarehouseSource: defaults.DefaultWarehouseSource,
                RequiresManualWarehouseSelection: defaults.RequiresManualWarehouseSelection,
                ConfigurationWarnings: defaults.ConfigurationWarnings
            )
        );
    }
}
