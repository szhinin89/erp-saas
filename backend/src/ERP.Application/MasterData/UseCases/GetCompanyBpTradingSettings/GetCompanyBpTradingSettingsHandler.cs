using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetCompanyBpTradingSettings;

public sealed class GetCompanyBpTradingSettingsHandler
    : IRequestHandler<GetCompanyBpTradingSettingsQuery, Result<CompanyBpTradingSettingsDto>>
{
    private readonly ICompanyBpTradingSettingsRepository _settingsRepo;

    public GetCompanyBpTradingSettingsHandler(ICompanyBpTradingSettingsRepository settingsRepo) =>
        _settingsRepo = settingsRepo;

    public async Task<Result<CompanyBpTradingSettingsDto>> Handle(
        GetCompanyBpTradingSettingsQuery q,
        CancellationToken cancellationToken
    )
    {
        var settings = await _settingsRepo.GetByBusinessPartnerAsync(
            q.BusinessPartnerId,
            cancellationToken
        );
        return Result<CompanyBpTradingSettingsDto>.Success(
            settings is not null
                ? CompanyBpTradingSettingsDto.From(settings)
                : CompanyBpTradingSettingsDto.Defaults(q.BusinessPartnerId)
        );
    }
}
