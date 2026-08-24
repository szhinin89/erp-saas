using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.UploadImportFile;

public sealed class UploadImportFileHandler
    : IRequestHandler<UploadImportFileCommand, Result<ImportBatchDto>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IFileStorage _fileStorage;
    private readonly IOperationalContext _ctx;

    public UploadImportFileHandler(
        IImportBatchRepository batchRepo,
        IFileStorage fileStorage,
        IOperationalContext ctx
    )
    {
        _batchRepo = batchRepo;
        _fileStorage = fileStorage;
        _ctx = ctx;
    }

    public async Task<Result<ImportBatchDto>> Handle(
        UploadImportFileCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var batch = await _batchRepo.GetByIdAsync(
            cmd.ImportBatchId,
            _ctx.TenantId,
            _ctx.CompanyId,
            cancellationToken
        );
        if (batch is null)
            return Result<ImportBatchDto>.NotFound("Lote de importación no encontrado.");

        var relativePath = $"initial-load/{batch.TenantId}/{batch.Id}/{Guid.NewGuid()}.xlsx";

        string storedPath;
        try
        {
            storedPath = await _fileStorage.SaveAsync(
                relativePath,
                cmd.Content.Content,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return Result<ImportBatchDto>.ValidationFailure(
                $"No se pudo guardar el archivo: {ex.Message}"
            );
        }

        try
        {
            batch.AttachFile(storedPath, cmd.Content.FileName, cmd.Content.SizeBytes, _ctx.UserId);
            batch.MarkUploaded(_ctx.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ImportBatchDto>.ValidationFailure(ex.Message);
        }

        await _batchRepo.SaveChangesAsync(cancellationToken);
        return Result<ImportBatchDto>.Success(ImportBatchDto.From(batch));
    }
}
