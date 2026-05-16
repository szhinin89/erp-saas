using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.GetBillingSettings;

public sealed class GetBillingSettingsQueryHandler
    : IRequestHandler<GetBillingSettingsQuery, Result<BillingSettingsDto?>>
{
    private readonly IBillingSettingsRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetBillingSettingsQueryHandler(
        IBillingSettingsRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<BillingSettingsDto?>> Handle(
        GetBillingSettingsQuery query, CancellationToken ct)
    {
        var config = await _repo.GetByTenantIdAsync(_currentTenant.TenantId, ct);
        if (config is null)
            return Result<BillingSettingsDto?>.Success(null);

        return Result<BillingSettingsDto?>.Success(new BillingSettingsDto(
            config.Id,
            config.TenantId,
            config.LegalName,
            config.TradeName,
            config.Ruc,
            config.MainAddress,
            config.Phone,
            config.Email,
            config.RequiresAccounting,
            config.SpecialTaxpayer,
            config.LogoBase64,
            config.FooterText,
            config.ReceiptWidth));
    }
}
