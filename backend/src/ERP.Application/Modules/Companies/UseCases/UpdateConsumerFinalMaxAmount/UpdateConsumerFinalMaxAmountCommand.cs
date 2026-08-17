using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateConsumerFinalMaxAmount;

/// <summary>
/// Único campo editable de la política fiscal de Consumidor Final. 0.00 es un valor válido:
/// bloquea toda venta a Consumidor Final. BlockConsumerFinalCredit nunca viaja aquí — es fijo,
/// no editable.
/// </summary>
public sealed record UpdateConsumerFinalMaxAmountCommand(decimal ConsumerFinalMaxAmount)
    : IRequest<Result<SalesFiscalPolicyDto>>;
