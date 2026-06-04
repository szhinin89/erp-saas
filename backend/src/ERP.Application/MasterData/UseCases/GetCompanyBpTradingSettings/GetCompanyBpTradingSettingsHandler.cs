using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetCompanyBpTradingSettings;

public sealed class GetCompanyBpTradingSettingsHandler
    : IRequestHandler<GetCompanyBpTradingSettingsQuery, Result<CompanyBpTradingSettingsDto>>
{
    private readonly ICompanyBpTradingSettingsRepository _settingsRepo;

    public GetCompanyBpTradingSettingsHandler(ICompanyBpTradingSettingsRepository settingsRepo)
        => _settingsRepo = settingsRepo;

    public async Task<Result<CompanyBpTradingSettingsDto>> Handle(
        GetCompanyBpTradingSettingsQuery q, CancellationToken ct)
    {
        var settings = await _settingsRepo.GetByBusinessPartnerAsync(q.BusinessPartnerId, ct);
        return settings is null
            ? Result<CompanyBpTradingSettingsDto>.NotFound("No existe configuración comercial para este BP en la empresa activa.")
            : Result<CompanyBpTradingSettingsDto>.Success(CompanyBpTradingSettingsDto.From(settings));
    }
}
