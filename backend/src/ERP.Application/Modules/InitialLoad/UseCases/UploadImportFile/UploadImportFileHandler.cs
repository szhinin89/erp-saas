using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Exceptions;
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
        catch (DomainRuleViolationException)
        {
            // ZH-DOMAIN-RULE-ERROR-SSOT-01B: el archivo ya se escribió en el almacenamiento antes de
            // que el lote rechazara el adjunto (estado no admite archivos) — se compensa para no dejar
            // un archivo huérfano y la regla sigue su camino (DomainRuleBehavior → 422).
            await DeleteOrphanAsync(storedPath);
            throw;
        }

        await _batchRepo.SaveChangesAsync(cancellationToken);
        return Result<ImportBatchDto>.Success(ImportBatchDto.From(batch));
    }

    /// <summary>
    /// Compensación best-effort: si el borrado también fallara, prevalece el rechazo de negocio
    /// (el usuario recibe el motivo real) y el archivo queda como huérfano, igual que antes.
    /// </summary>
    private async Task DeleteOrphanAsync(string storedPath)
    {
        try
        {
            await _fileStorage.DeleteAsync(storedPath, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El rechazo de negocio es el resultado relevante; nada más que hacer aquí.
        }
    }
}
