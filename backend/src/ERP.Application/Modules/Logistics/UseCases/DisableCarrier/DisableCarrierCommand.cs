using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.DisableCarrier;

[RequireFeature(SubscriptionFeatureCodes.Logistics)]
public record DisableCarrierCommand(Guid CarrierId) : IRequest<Result<CarrierDto>>, ICompanyScopedRequest;

[RequireFeature(SubscriptionFeatureCodes.Logistics)]
public record EnableCarrierCommand(Guid CarrierId)  : IRequest<Result<CarrierDto>>, ICompanyScopedRequest;
