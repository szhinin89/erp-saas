using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.GetTransferenciaById;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record GetTransferByIdQuery(Guid Id)
    : IRequest<Result<TransferDetailDto?>>, ICompanyScopedRequest;
