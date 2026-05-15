using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.VincularFacturaAOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record VincularFacturaAOrdenCompraCommand(
    Guid OrdenCompraId,
    Guid CompraFacturaId
) : IRequest<Result<OrdenCompraDto>>;
