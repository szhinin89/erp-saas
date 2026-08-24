using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;

/// <summary>
/// Parcial-seguro por construcción: solo procesa filas sin error bloqueante
/// (<see cref="IImportBatchRowRepository.StreamValidRowsAsync"/>) — las filas bloqueadas nunca
/// llegan a <c>ConfirmRowAsync</c>. Una excepción/fallo en una fila no aborta el lote: se
/// registra como <see cref="ImportBatchIssue"/> (código <c>CONFIRM_FAILED</c>) y continúa con la
/// siguiente.
/// </summary>
public sealed class ConfirmImportBatchHandler
    : IRequestHandler<ConfirmImportBatchCommand, Result<ImportBatchConfirmResultDto>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IImportBatchRowRepository _rowRepo;
    private readonly IImportBatchIssueRepository _issueRepo;
    private readonly IReadOnlyDictionary<ImportType, IImportProcessor> _processors;
    private readonly IOperationalContext _ctx;

    public ConfirmImportBatchHandler(
        IImportBatchRepository batchRepo,
        IImportBatchRowRepository rowRepo,
        IImportBatchIssueRepository issueRepo,
        IReadOnlyDictionary<ImportType, IImportProcessor> processors,
        IOperationalContext ctx
    )
    {
        _batchRepo = batchRepo;
        _rowRepo = rowRepo;
        _issueRepo = issueRepo;
        _processors = processors;
        _ctx = ctx;
    }

    public async Task<Result<ImportBatchConfirmResultDto>> Handle(
        ConfirmImportBatchCommand cmd,
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
            return Result<ImportBatchConfirmResultDto>.NotFound("Lote de importación no encontrado.");

        if (!_processors.TryGetValue(batch.ImportType, out var processor))
            return Result<ImportBatchConfirmResultDto>.ValidationFailure(
                "No hay un procesador disponible para este tipo de importación."
            );

        try
        {
            batch.BeginConfirming(_ctx.UserId);
            await _batchRepo.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ImportBatchConfirmResultDto>.ValidationFailure(ex.Message);
        }

        var importedRows = 0;
        var failedRows = 0;
        const int pageSize = 200;

        try
        {
            // Paginado, no streaming: confirmar cada fila envía comandos MediatR que abren su
            // propio SaveChangesAsync sobre el mismo DbContext — un IAsyncEnumerable con reader
            // abierto entraría en conflicto con esas escrituras anidadas en Npgsql. Cada página se
            // vuelve a pedir tras persistir (las filas ya procesadas dejan de calificar como
            // "válidas y no importadas"), así que nunca se reprocesa una fila.
            while (true)
            {
                var page = await _rowRepo.GetValidRowsPageAsync(
                    batch.Id,
                    batch.TenantId,
                    batch.CompanyId,
                    pageSize,
                    cancellationToken
                );
                if (page.Count == 0)
                    break;

                foreach (var row in page)
                {
                    if (row.ParsedData is null)
                        continue;

                    // Una fila nunca debe poder tumbar el lote completo: además de los fallos
                    // "esperados" (Result.IsSuccess == false), una excepción no controlada de un
                    // comando anidado (p. ej. un pipeline behavior de alcance de sucursal/tenant)
                    // también se captura aquí y se registra como CONFIRM_FAILED — de lo contrario
                    // el lote queda atascado en Confirming para siempre, porque Cancel() solo
                    // permite Draft|Uploaded|Validated y no hay forma de reintentar Confirm.
                    string? errorMessage;
                    var confirmed = false;
                    Guid? businessPartnerId = null;
                    try
                    {
                        var confirmResult = await processor.ConfirmRowAsync(
                            row.ParsedData,
                            cancellationToken
                        );
                        confirmed = confirmResult.IsSuccess;
                        businessPartnerId = confirmResult.BusinessPartnerId;
                        errorMessage = confirmResult.Error;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errorMessage = ex.Message;
                    }

                    if (confirmed)
                    {
                        row.MarkImported(businessPartnerId!.Value, _ctx.UserId);
                        importedRows++;
                    }
                    else
                    {
                        failedRows++;
                        await _issueRepo.AddAsync(
                            ImportBatchIssue.Create(
                                batch.TenantId,
                                batch.CompanyId,
                                batch.Id,
                                row.Id,
                                row.RowNumber,
                                ImportSeverity.Error,
                                "CONFIRM_FAILED",
                                errorMessage ?? "No se pudo confirmar la fila.",
                                _ctx.UserId
                            ),
                            cancellationToken
                        );
                    }
                }

                await _rowRepo.SaveChangesAsync(cancellationToken);
                await _issueRepo.SaveChangesAsync(cancellationToken);

                if (page.Count < pageSize)
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fallo inesperado fuera del bucle por fila (p. ej. la propia paginación) — el lote no
            // puede quedar en Confirming sin salida: se marca Failed con el motivo y se reporta,
            // en vez de dejarlo bloqueado para siempre.
            batch.Fail(ex.Message, _ctx.UserId);
            await _batchRepo.SaveChangesAsync(cancellationToken);
            return Result<ImportBatchConfirmResultDto>.Failure(
                $"La confirmación del lote falló de forma inesperada: {ex.Message}"
            );
        }

        batch.CompleteConfirmation(importedRows, failedRows > 0, _ctx.UserId);
        await _batchRepo.SaveChangesAsync(cancellationToken);

        return Result<ImportBatchConfirmResultDto>.Success(
            new ImportBatchConfirmResultDto(batch.Id, batch.Status, importedRows, failedRows)
        );
    }
}
