using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.RechazarCompra;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record RechazarCompraCommand(Guid CompraFacturaId, string Motivo)
    : IRequest<Result<CompraFacturaDto>>;
