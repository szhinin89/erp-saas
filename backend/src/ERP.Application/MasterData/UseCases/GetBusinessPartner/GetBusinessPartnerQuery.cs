using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetBusinessPartner;

/// <summary>Devuelve detalle completo del BP incluyendo todos sus roles con configs.</summary>
public sealed record GetBusinessPartnerQuery(Guid Id)
    : IRequest<Result<BusinessPartnerDetailDto>>, ISubscriberScopedRequest;
