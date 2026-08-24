using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;

/// <summary>
/// Ejecuta el camino síncrono siempre en esta entrega — el seam de Hangfire para lotes grandes
/// (<c>ImportBatchConstants.AsyncThresholdRows</c>) queda construido pero sin carga real de
/// prueba (ver INITIAL-LOAD-ARCH-01, la plantilla de Clientes no se espera que cruce el umbral).
/// </summary>
public sealed class ValidateImportBatchHandler
    : IRequestHandler<ValidateImportBatchCommand, Result<ImportBatchDto>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IImportBatchRowRepository _rowRepo;
    private readonly IImportBatchIssueRepository _issueRepo;
    private readonly IFileStorage _fileStorage;
    private readonly IReadOnlyDictionary<ImportType, IImportProcessor> _processors;
    private readonly IOperationalContext _ctx;

    public ValidateImportBatchHandler(
        IImportBatchRepository batchRepo,
        IImportBatchRowRepository rowRepo,
        IImportBatchIssueRepository issueRepo,
        IFileStorage fileStorage,
        IReadOnlyDictionary<ImportType, IImportProcessor> processors,
        IOperationalContext ctx
    )
    {
        _batchRepo = batchRepo;
        _rowRepo = rowRepo;
        _issueRepo = issueRepo;
        _fileStorage = fileStorage;
        _processors = processors;
        _ctx = ctx;
    }

    public async Task<Result<ImportBatchDto>> Handle(
        ValidateImportBatchCommand cmd,
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

        if (!_processors.TryGetValue(batch.ImportType, out var processor))
            return Result<ImportBatchDto>.ValidationFailure(
                "No hay un procesador disponible para este tipo de importación."
            );

        var file = batch.Files.OrderByDescending(f => f.UploadedAt).FirstOrDefault();
        if (file is null)
            return Result<ImportBatchDto>.ValidationFailure("El lote no tiene ningún archivo adjunto.");

        try
        {
            batch.BeginValidating(_ctx.UserId);
            await _batchRepo.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ImportBatchDto>.ValidationFailure(ex.Message);
        }

        await using var stream = await _fileStorage.GetAsync(file.StoredPath, cancellationToken);
        if (stream is null)
            return Result<ImportBatchDto>.ValidationFailure("El archivo del lote ya no está disponible.");

        ImportReadResult readResult;
        try
        {
            readResult = await processor.ReadAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            batch.Fail($"No se pudo leer el archivo: {ex.Message}", _ctx.UserId);
            await _batchRepo.SaveChangesAsync(cancellationToken);
            return Result<ImportBatchDto>.ValidationFailure(
                $"No se pudo leer el archivo: {ex.Message}"
            );
        }

        var rows = new List<ImportBatchRow>();
        var rowNumber = 0;
        foreach (var rawRow in readResult.Rows)
        {
            rowNumber++;
            rows.Add(
                ImportBatchRow.Create(
                    batch.TenantId,
                    batch.CompanyId,
                    batch.Id,
                    rowNumber,
                    System.Text.Json.JsonSerializer.Serialize(rawRow),
                    _ctx.UserId
                )
            );
        }
        await _rowRepo.AddRangeAsync(rows, cancellationToken);
        await _rowRepo.SaveChangesAsync(cancellationToken);

        var validCount = 0;
        var issueRowCount = 0;
        var warningRowCount = 0;
        var newIssues = new List<Domain.Modules.InitialLoad.Entities.ImportBatchIssue>();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var validation = await processor.ValidateRowAsync(
                row.RowNumber,
                readResult.Rows[i],
                batch.AutoCreateCatalogValues,
                cancellationToken
            );

            row.SetParsedData(validation.ParsedDataJson, validation.HasBlockingIssue, _ctx.UserId);

            foreach (var issue in validation.Issues)
                newIssues.Add(
                    Domain.Modules.InitialLoad.Entities.ImportBatchIssue.Create(
                        batch.TenantId,
                        batch.CompanyId,
                        batch.Id,
                        row.Id,
                        row.RowNumber,
                        issue.Severity,
                        issue.Code,
                        issue.Message,
                        _ctx.UserId,
                        issue.FieldName
                    )
                );

            if (validation.HasBlockingIssue)
                issueRowCount++;
            else
            {
                validCount++;
                if (validation.Issues.Count > 0)
                    warningRowCount++;
            }
        }

        if (newIssues.Count > 0)
            await _issueRepo.AddRangeAsync(newIssues, cancellationToken);
        await _rowRepo.SaveChangesAsync(cancellationToken);
        await _issueRepo.SaveChangesAsync(cancellationToken);

        batch.CompleteValidation(rows.Count, validCount, issueRowCount, warningRowCount, _ctx.UserId);
        await _batchRepo.SaveChangesAsync(cancellationToken);

        return Result<ImportBatchDto>.Success(ImportBatchDto.From(batch));
    }
}
