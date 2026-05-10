using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.AprobarOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record AprobarOrdenCompraCommand(Guid OrdenId)
    : IRequest<Result<OrdenCompraDto>>;
