using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetCompanyBpTradingSettings;

public sealed record GetCompanyBpTradingSettingsQuery(Guid BusinessPartnerId)
    : IRequest<Result<CompanyBpTradingSettingsDto>>,
        ICompanyScopedRequest;
