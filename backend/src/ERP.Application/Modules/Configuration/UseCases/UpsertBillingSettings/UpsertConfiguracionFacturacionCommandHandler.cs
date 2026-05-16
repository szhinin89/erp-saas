using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Common;

namespace ERP.Application.Configuration.UseCases.UpsertBillingSettings;

public sealed class UpsertBillingSettingsCommandHandler
    : IRequestHandler<UpsertBillingSettingsCommand, Result<BillingSettingsDto>>
{
    private readonly IBillingSettingsRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public UpsertBillingSettingsCommandHandler(
        IBillingSettingsRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<BillingSettingsDto>> Handle(
        UpsertBillingSettingsCommand command,
        CancellationToken ct)
    {
        var config = await _repo.GetByTenantIdAsync(_currentTenant.TenantId, ct);
        if (config is null)
        {
            config = BillingSettings.Create(
                _currentTenant.TenantId,
                command.LegalName,
                command.TradeName,
                command.Ruc,
                command.MainAddress,
                command.Phone,
                command.Email,
                command.RequiresAccounting,
                command.SpecialTaxpayer,
                command.LogoBase64,
                command.FooterText,
                command.ReceiptWidth,
                _currentUser.UserId);

            await _repo.AddAsync(config, ct);
        }
        else
        {
            config.Update(
                command.LegalName,
                command.TradeName,
                command.Ruc,
                command.MainAddress,
                command.Phone,
                command.Email,
                command.RequiresAccounting,
                command.SpecialTaxpayer,
                command.LogoBase64,
                command.FooterText,
                command.ReceiptWidth,
                _currentUser.UserId);

            await _repo.UpdateAsync(config, ct);
        }

        await _repo.SaveChangesAsync(ct);

        return Result<BillingSettingsDto>.Success(new BillingSettingsDto(
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
