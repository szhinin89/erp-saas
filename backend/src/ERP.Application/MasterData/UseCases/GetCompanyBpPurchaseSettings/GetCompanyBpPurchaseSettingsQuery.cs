using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetCompanyBpPurchaseSettings;

public sealed record GetCompanyBpPurchaseSettingsQuery(Guid BusinessPartnerId)
    : IRequest<Result<CompanyBpPurchaseSettingsDto>>,
        ICompanyScopedRequest;
