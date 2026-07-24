using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Modules.OrgConfig.UseCases.GetCompanyInvoiceOrgSettings;

public sealed class GetCompanyInvoiceOrgSettingsQueryHandler
    : IRequestHandler<GetCompanyInvoiceOrgSettingsQuery, Result<CompanyInvoiceOrgSettingsDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly ICurrentTenant         _currentTenant;
    private readonly ICurrentCompany        _currentCompany;

    public GetCompanyInvoiceOrgSettingsQueryHandler(
        IOrgSettingsRepository repo,
        ICurrentTenant         currentTenant,
        ICurrentCompany        currentCompany)
    {
        _repo           = repo;
        _currentTenant  = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<CompanyInvoiceOrgSettingsDto>> Handle(
        GetCompanyInvoiceOrgSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId  = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;

        var all = await _repo.GetAllForScopeAsync(
            tenantId, companyId, OrgScope.Company, companyId, cancellationToken);

        var lookup = all.ToDictionary(s => s.Key, s => s.Value);

        return Result<CompanyInvoiceOrgSettingsDto>.Success(new CompanyInvoiceOrgSettingsDto(
            DefaultDocTypeCode:          lookup.GetValueOrDefault(OrgSettingKeys.Invoice.DefaultDocTypeCode),
            DefaultSriPaymentMethodCode: lookup.GetValueOrDefault(OrgSettingKeys.Invoice.DefaultPaymentMethodCode),
            DefaultPaymentTermId:        TryParseGuid(lookup.GetValueOrDefault(OrgSettingKeys.Invoice.DefaultPaymentTermId))
        ));
    }

    private static Guid? TryParseGuid(string? value)
        => Guid.TryParse(value, out var g) ? g : null;
}
