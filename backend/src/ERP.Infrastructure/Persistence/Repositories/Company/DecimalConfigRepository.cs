using ERP.Application.Common;
using ERP.Application.Modules.Companies.UseCases.DecimalConfig;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Persistence.Repositories.CompanyConfig;

/// <summary>
/// CONFIG-FOUNDATION-P1-01: decimales de PRESENTACIÓN (cómo se muestran/almacenan cantidades,
/// precios, costos, porcentajes y totales en pantalla) — respaldados por <c>org_settings</c>,
/// scope Company, namespace <see cref="OrgSettingKeys.Presentation"/>. Reemplaza el mecanismo
/// paralelo <c>GeneralParameter</c> (tabla <c>general_parameter</c>, eliminada en esta entrega
/// junto con su único consumidor real).
///
/// NUNCA usar para redondeo fiscal/tributario/legal — eso es
/// <see cref="ERP.Domain.Common.FiscalPrecision"/>, constante System no configurable, sin
/// relación con esta clase.
///
/// Fail-safe de lectura: un valor corrupto o fuera de rango en un OrgSetting existente cae al
/// default seguro con warning en log — nunca tumba la operación (esto es presentación de UI, no
/// un dato crítico de documento/fiscal/inventario). La escritura, en cambio, se valida antes de
/// llegar aquí (<c>UpdateDecimalConfigCommandValidator</c>) — este repositorio no reintroduce el
/// clamp silencioso que tenía la implementación anterior sobre GeneralParameter.
/// </summary>
public sealed class DecimalConfigRepository : IDecimalConfigRepository
{
    private const int MinDecimals = 0;
    private const int MaxDecimals = 6;

    private const int DefaultSalesUnitPrice = 2;
    private const int DefaultPurchaseUnitPrice = 4;
    private const int DefaultQuantity = 4;
    private const int DefaultPercentage = 2;
    private const int DefaultTotalAmount = 2;

    private readonly IOrgSettingsRepository _orgRepo;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<DecimalConfigRepository> _logger;

    public DecimalConfigRepository(
        IOrgSettingsRepository orgRepo,
        ICurrentUser currentUser,
        ILogger<DecimalConfigRepository> logger
    )
    {
        _orgRepo = orgRepo;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<DecimalConfigDto> GetAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    )
    {
        var settings = await _orgRepo.GetAllForScopeAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            ct
        );
        var lookup = settings.ToDictionary(s => s.Key, s => s.Value);

        return new DecimalConfigDto(
            GetInt(lookup, OrgSettingKeys.Presentation.DecimalSalesUnitPrice, DefaultSalesUnitPrice),
            GetInt(
                lookup,
                OrgSettingKeys.Presentation.DecimalPurchaseUnitPrice,
                DefaultPurchaseUnitPrice
            ),
            GetInt(lookup, OrgSettingKeys.Presentation.DecimalQuantity, DefaultQuantity),
            GetInt(lookup, OrgSettingKeys.Presentation.DecimalPercentage, DefaultPercentage),
            GetInt(lookup, OrgSettingKeys.Presentation.DecimalTotalAmount, DefaultTotalAmount)
        );
    }

    public async Task SaveAsync(
        Guid tenantId,
        Guid companyId,
        int salesUnitPrice,
        int purchaseUnitPrice,
        int quantity,
        int percentage,
        int totalAmount,
        CancellationToken ct = default
    )
    {
        var userId = _currentUser.UserId;

        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Presentation.DecimalSalesUnitPrice,
            salesUnitPrice,
            userId,
            ct
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Presentation.DecimalPurchaseUnitPrice,
            purchaseUnitPrice,
            userId,
            ct
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Presentation.DecimalQuantity,
            quantity,
            userId,
            ct
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Presentation.DecimalPercentage,
            percentage,
            userId,
            ct
        );
        await UpsertAsync(
            tenantId,
            companyId,
            OrgSettingKeys.Presentation.DecimalTotalAmount,
            totalAmount,
            userId,
            ct
        );

        await _orgRepo.SaveChangesAsync(ct);
    }

    private async Task UpsertAsync(
        Guid tenantId,
        Guid companyId,
        string key,
        int value,
        Guid updatedBy,
        CancellationToken ct
    )
    {
        var setting = OrgSetting.Create(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            key,
            value.ToString(),
            SettingDataType.Int,
            updatedBy
        );
        await _orgRepo.UpsertAsync(setting, ct);
    }

    private int GetInt(Dictionary<string, string?> lookup, string key, int defaultVal)
    {
        if (!lookup.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultVal;

        if (!int.TryParse(raw, out var v) || v < MinDecimals || v > MaxDecimals)
        {
            _logger.LogWarning(
                "OrgSetting {Key} tiene un valor de presentación corrupto o fuera de rango ({RawValue}) — usando default {DefaultValue}.",
                key,
                raw,
                defaultVal
            );
            return defaultVal;
        }

        return v;
    }
}
