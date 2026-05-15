using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.AprobarOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record AprobarOrdenCompraCommand(Guid OrdenId)
    : IRequest<Result<OrdenCompraDto>>;
