using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.RechazarCompra;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record RechazarCompraCommand(Guid CompraFacturaId, string Motivo)
    : IRequest<Result<CompraFacturaDto>>;
