using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.GetImportBatchStatus;

public sealed record GetImportBatchStatusQuery(Guid ImportBatchId)
    : IRequest<Result<ImportBatchDto>>,
        ICompanyScopedRequest;
