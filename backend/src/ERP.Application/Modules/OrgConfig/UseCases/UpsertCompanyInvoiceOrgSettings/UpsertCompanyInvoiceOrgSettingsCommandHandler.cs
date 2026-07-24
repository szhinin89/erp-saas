using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings;

public sealed class UpsertCompanyInvoiceOrgSettingsCommandHandler
    : IRequestHandler<UpsertCompanyInvoiceOrgSettingsCommand, Result<CompanyInvoiceOrgSettingsDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly ICurrentTenant         _currentTenant;
    private readonly ICurrentCompany        _currentCompany;
    private readonly ICurrentUser           _currentUser;

    public UpsertCompanyInvoiceOrgSettingsCommandHandler(
        IOrgSettingsRepository repo,
        ICurrentTenant         currentTenant,
        ICurrentCompany        currentCompany,
        ICurrentUser           currentUser)
    {
        _repo           = repo;
        _currentTenant  = currentTenant;
        _currentCompany = currentCompany;
        _currentUser    = currentUser;
    }

    public async Task<Result<CompanyInvoiceOrgSettingsDto>> Handle(
        UpsertCompanyInvoiceOrgSettingsCommand command, CancellationToken cancellationToken)
    {
        var tenantId  = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var userId    = _currentUser.UserId;

        await UpsertAsync(tenantId, companyId, OrgSettingKeys.Invoice.DefaultDocTypeCode,
            command.DefaultDocTypeCode, SettingDataType.String, userId, cancellationToken);

        await UpsertAsync(tenantId, companyId, OrgSettingKeys.Invoice.DefaultPaymentMethodCode,
            command.DefaultSriPaymentMethodCode, SettingDataType.String, userId, cancellationToken);

        await UpsertAsync(tenantId, companyId, OrgSettingKeys.Invoice.DefaultPaymentTermId,
            command.DefaultPaymentTermId?.ToString(), SettingDataType.Guid, userId, cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);

        return Result<CompanyInvoiceOrgSettingsDto>.Success(new CompanyInvoiceOrgSettingsDto(
            command.DefaultDocTypeCode,
            command.DefaultSriPaymentMethodCode,
            command.DefaultPaymentTermId
        ));
    }

    private async Task UpsertAsync(
        Guid tenantId, Guid companyId,
        string key, string? value, SettingDataType dataType,
        Guid userId, CancellationToken ct)
    {
        var setting = OrgSetting.Create(
            tenantId, companyId,
            OrgScope.Company, companyId,
            key, value, dataType, userId);

        await _repo.UpsertAsync(setting, ct);
    }
}
