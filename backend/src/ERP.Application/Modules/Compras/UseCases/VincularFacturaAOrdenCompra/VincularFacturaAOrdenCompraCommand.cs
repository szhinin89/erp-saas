using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.VincularFacturaAOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record VincularFacturaAOrdenCompraCommand(
    Guid OrdenCompraId,
    Guid CompraFacturaId
) : IRequest<Result<OrdenCompraDto>>;
