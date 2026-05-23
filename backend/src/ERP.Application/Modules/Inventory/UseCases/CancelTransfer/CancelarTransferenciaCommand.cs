using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.CancelarTransferencia;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record CancelTransferCommand(Guid TransferId)
    : IRequest<Result<TransferDto>>, ICompanyScopedRequest;
