using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetCompanyBpSalesSettings;

public sealed record GetCompanyBpSalesSettingsQuery(Guid BusinessPartnerId)
    : IRequest<Result<CompanyBpSalesSettingsDto>>,
        ICompanyScopedRequest;
