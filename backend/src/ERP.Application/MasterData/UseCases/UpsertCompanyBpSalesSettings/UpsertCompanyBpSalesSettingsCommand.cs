using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpSalesSettings;

public sealed record UpsertCompanyBpSalesSettingsCommand(Guid BusinessPartnerId, Guid? PaymentTermId)
    : IRequest<Result<CompanyBpSalesSettingsDto>>, ICompanyScopedRequest;
