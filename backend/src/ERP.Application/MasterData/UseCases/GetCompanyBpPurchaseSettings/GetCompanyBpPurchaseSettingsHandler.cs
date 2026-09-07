using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetCompanyBpPurchaseSettings;

public sealed class GetCompanyBpPurchaseSettingsHandler
    : IRequestHandler<GetCompanyBpPurchaseSettingsQuery, Result<CompanyBpPurchaseSettingsDto>>
{
    private readonly ICompanyBpPurchaseSettingsRepository _settingsRepo;

    public GetCompanyBpPurchaseSettingsHandler(ICompanyBpPurchaseSettingsRepository settingsRepo) =>
        _settingsRepo = settingsRepo;

    public async Task<Result<CompanyBpPurchaseSettingsDto>> Handle(
        GetCompanyBpPurchaseSettingsQuery q,
        CancellationToken cancellationToken
    )
    {
        var settings = await _settingsRepo.GetByBusinessPartnerAsync(
            q.BusinessPartnerId,
            cancellationToken
        );
        return Result<CompanyBpPurchaseSettingsDto>.Success(
            settings is not null
                ? CompanyBpPurchaseSettingsDto.From(settings)
                : CompanyBpPurchaseSettingsDto.Defaults(q.BusinessPartnerId)
        );
    }
}
