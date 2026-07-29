using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.GetBranchInvoiceOrgSettings;

public sealed class GetBranchInvoiceOrgSettingsQueryHandler
    : IRequestHandler<GetBranchInvoiceOrgSettingsQuery, Result<BranchInvoiceOrgSettingsDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetBranchInvoiceOrgSettingsQueryHandler(
        IOrgSettingsRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<BranchInvoiceOrgSettingsDto>> Handle(
        GetBranchInvoiceOrgSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;

        var all = await _repo.GetAllForScopeAsync(
            tenantId, companyId, OrgScope.Branch, request.BranchId, cancellationToken);

        var lookup = all.ToDictionary(s => s.Key, s => s.Value);

        Guid? TryParseGuid(string? value)
            => Guid.TryParse(value, out var g) ? g : null;

        return Result<BranchInvoiceOrgSettingsDto>.Success(new BranchInvoiceOrgSettingsDto(
            DefaultWarehouseId: TryParseGuid(lookup.GetValueOrDefault(OrgSettingKeys.Invoice.DefaultWarehouseId))
        ));
    }
}
