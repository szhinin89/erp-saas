using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.ConfirmTransfer;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record ConfirmTransferCommand(Guid TransferId)
    : IRequest<Result<TransferDto>>, ICompanyScopedRequest;
