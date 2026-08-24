using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Modules.InitialLoad.Enums;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.CreateImportBatch;

public sealed record CreateImportBatchCommand(ImportType ImportType, string? Label = null)
    : IRequest<Result<ImportBatchDto>>,
        ICompanyScopedRequest;
