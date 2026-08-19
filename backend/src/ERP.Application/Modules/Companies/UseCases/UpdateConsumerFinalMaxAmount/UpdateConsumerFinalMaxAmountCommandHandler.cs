using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;
using System.Globalization;

namespace ERP.Application.Modules.Companies.UseCases.UpdateConsumerFinalMaxAmount;

public sealed class UpdateConsumerFinalMaxAmountCommandHandler
    : IRequestHandler<UpdateConsumerFinalMaxAmountCommand, Result<SalesFiscalPolicyDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly ISalesFiscalPolicyResolver _resolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public UpdateConsumerFinalMaxAmountCommandHandler(
        IOrgSettingsRepository repo,
        ISalesFiscalPolicyResolver resolver,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _resolver = resolver;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<SalesFiscalPolicyDto>> Handle(
        UpdateConsumerFinalMaxAmountCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;

        var existing = await _repo.GetAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            OrgSettingKeys.Sales.ConsumerFinalMaxAmount,
            cancellationToken
        );

        var value = command.ConsumerFinalMaxAmount.ToString("0.00", CultureInfo.InvariantCulture);

        if (existing is not null)
        {
            existing.UpdateValue(value, _currentUser.UserId);
            await _repo.UpsertAsync(existing, cancellationToken);
        }
        else
        {
            var setting = OrgSetting.Create(
                tenantId,
                companyId,
                OrgScope.Company,
                companyId,
                OrgSettingKeys.Sales.ConsumerFinalMaxAmount,
                value,
                SettingDataType.Decimal,
                _currentUser.UserId
            );
            await _repo.UpsertAsync(setting, cancellationToken);
        }

        await _repo.SaveChangesAsync(cancellationToken);

        var policy = await _resolver.GetEffectivePolicyAsync(cancellationToken);
        return Result<SalesFiscalPolicyDto>.Success(SalesFiscalPolicyMapper.ToDto(policy));
    }
}
