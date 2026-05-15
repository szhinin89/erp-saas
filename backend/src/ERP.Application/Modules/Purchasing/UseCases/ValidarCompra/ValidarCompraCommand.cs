using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.ValidarCompra;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record ValidarCompraCommand(Guid CompraFacturaId)
    : IRequest<Result<CompraFacturaDto>>;
