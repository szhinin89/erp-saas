using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;

public sealed record CancelImportBatchCommand(Guid ImportBatchId)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;
