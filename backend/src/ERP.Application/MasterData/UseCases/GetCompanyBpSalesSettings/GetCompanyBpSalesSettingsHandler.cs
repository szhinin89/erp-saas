using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetCompanyBpSalesSettings;

public sealed class GetCompanyBpSalesSettingsHandler
    : IRequestHandler<GetCompanyBpSalesSettingsQuery, Result<CompanyBpSalesSettingsDto>>
{
    private readonly ICompanyBpSalesSettingsRepository _settingsRepo;

    public GetCompanyBpSalesSettingsHandler(ICompanyBpSalesSettingsRepository settingsRepo) =>
        _settingsRepo = settingsRepo;

    public async Task<Result<CompanyBpSalesSettingsDto>> Handle(
        GetCompanyBpSalesSettingsQuery q,
        CancellationToken cancellationToken
    )
    {
        var settings = await _settingsRepo.GetByBusinessPartnerAsync(
            q.BusinessPartnerId,
            cancellationToken
        );
        return Result<CompanyBpSalesSettingsDto>.Success(
            settings is not null
                ? CompanyBpSalesSettingsDto.From(settings)
                : CompanyBpSalesSettingsDto.Defaults(q.BusinessPartnerId)
        );
    }
}
