using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.GetCarriers;

[RequireFeature(SubscriptionFeatureCodes.Logistics)]
public record GetCarriersQuery(string? Search = null, bool? IsActive = null)
    : IRequest<Result<List<CarrierDto>>>, ICompanyScopedRequest;
