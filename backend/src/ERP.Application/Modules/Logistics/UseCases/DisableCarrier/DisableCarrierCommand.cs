using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.DisableCarrier;

public record DisableCarrierCommand(Guid CarrierId) : IRequest<Result<CarrierDto>>;
public record EnableCarrierCommand(Guid CarrierId)  : IRequest<Result<CarrierDto>>;
