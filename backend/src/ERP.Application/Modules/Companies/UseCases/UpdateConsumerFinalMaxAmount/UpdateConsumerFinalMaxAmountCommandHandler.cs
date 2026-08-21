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

        var value = command.ConsumerFinalMaxAmount.ToString("0.00", CultureInfo.InvariantCulture);

        // CONFIG-FOUNDATION-P2-01: nunca pre-cargar y mutar la entidad existente aquí — UpsertAsync
        // resuelve internamente insert-vs-update y calcula OldValue/NewValue para el audit log
        // consultando el valor persistido. Si el caller mutara la instancia trackeada antes de
        // llamar a UpsertAsync, el identity map de EF devolvería esa misma instancia ya mutada al
        // recalcular OldValue dentro del repositorio, con lo que OldValue == NewValue siempre y el
        // ConfigurationChangeLog dejaría de generarse silenciosamente. Se construye siempre una
        // instancia nueva no trackeada, igual que el resto de escrituras de org_settings.
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

        await _repo.SaveChangesAsync(cancellationToken);

        var policy = await _resolver.GetEffectivePolicyAsync(cancellationToken);
        return Result<SalesFiscalPolicyDto>.Success(SalesFiscalPolicyMapper.ToDto(policy));
    }
}
