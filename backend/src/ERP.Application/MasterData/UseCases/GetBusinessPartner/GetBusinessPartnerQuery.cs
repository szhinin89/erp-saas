using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetBusinessPartner;

public sealed record GetBusinessPartnerQuery(Guid Id)
    : IRequest<Result<BusinessPartnerDto>>, ISubscriberOnlyRequest;
