using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.ValidarCompra;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record ValidarCompraCommand(Guid CompraFacturaId)
    : IRequest<Result<CompraFacturaDto>>;
