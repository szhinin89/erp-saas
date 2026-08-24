using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;

public sealed record ValidateImportBatchCommand(Guid ImportBatchId)
    : IRequest<Result<ImportBatchDto>>,
        ICompanyScopedRequest;
