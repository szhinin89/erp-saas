using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.CancelTransfer;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record CancelTransferCommand(Guid TransferId)
    : IRequest<Result<TransferDto>>, ICompanyScopedRequest;
