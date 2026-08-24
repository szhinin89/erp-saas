using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;

public sealed record ConfirmImportBatchCommand(Guid ImportBatchId)
    : IRequest<Result<ImportBatchConfirmResultDto>>,
        ICompanyScopedRequest;
