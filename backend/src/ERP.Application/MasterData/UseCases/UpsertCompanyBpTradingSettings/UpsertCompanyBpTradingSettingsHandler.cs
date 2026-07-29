using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpTradingSettings;

public sealed class UpsertCompanyBpTradingSettingsHandler
    : IRequestHandler<UpsertCompanyBpTradingSettingsCommand, Result<CompanyBpTradingSettingsDto>>
{
    private readonly ICompanyBpTradingSettingsRepository _settingsRepo;
    private readonly IOperationalContext _ctx;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public UpsertCompanyBpTradingSettingsHandler(
        ICompanyBpTradingSettingsRepository settingsRepo,
        IOperationalContext ctx,
        IDatabaseExceptionTranslator dbEx
    ) => (_settingsRepo, _ctx, _dbEx) = (settingsRepo, ctx, dbEx);

    public async Task<Result<CompanyBpTradingSettingsDto>> Handle(
        UpsertCompanyBpTradingSettingsCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (!_ctx.HasCompany)
            return Result<CompanyBpTradingSettingsDto>.Failure(
                "Contexto de empresa no establecido."
            );

        var existing = await _settingsRepo.GetByBusinessPartnerAsync(
            cmd.BusinessPartnerId,
            cancellationToken
        );

        CompanyBpTradingSettings settings;

        if (existing is not null)
        {
            try
            {
                existing.Update(
                    cmd.CreditLimit,
                    cmd.PaymentDays,
                    _ctx.UserId,
                    cmd.CreditCurrencyCode
                );
            }
            catch (ArgumentException ex)
            {
                return Result<CompanyBpTradingSettingsDto>.ValidationFailure(ex.Message);
            }
            settings = existing;
        }
        else
        {
            try
            {
                settings = CompanyBpTradingSettings.Create(
                    _ctx.TenantId,
                    _ctx.CompanyId,
                    cmd.BusinessPartnerId,
                    cmd.CreditLimit,
                    cmd.PaymentDays,
                    _ctx.UserId,
                    cmd.CreditCurrencyCode
                );
            }
            catch (ArgumentException ex)
            {
                return Result<CompanyBpTradingSettingsDto>.ValidationFailure(ex.Message);
            }
            await _settingsRepo.AddAsync(settings, cancellationToken);
        }

        try
        {
            await _settingsRepo.SaveChangesAsync(cancellationToken);
            return Result<CompanyBpTradingSettingsDto>.Success(
                CompanyBpTradingSettingsDto.From(settings)
            );
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<CompanyBpTradingSettingsDto>.Conflict(
                "Ya existe una configuración para este BusinessPartner en esta empresa (race condition)."
            );
        }
    }
}
