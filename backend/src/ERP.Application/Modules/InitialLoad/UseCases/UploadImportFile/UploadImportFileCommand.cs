using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Application.Modules.InitialLoad.DTOs;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.UploadImportFile;

public sealed record UploadImportFileCommand(Guid ImportBatchId, MediaUploadContent Content)
    : IRequest<Result<ImportBatchDto>>,
        ICompanyScopedRequest;
