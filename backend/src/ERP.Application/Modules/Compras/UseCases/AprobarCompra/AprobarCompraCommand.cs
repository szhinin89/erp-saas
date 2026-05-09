using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.AprobarCompra;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record AprobarCompraCommand(Guid CompraFacturaId)
    : IRequest<Result<CompraFacturaDto>>;
