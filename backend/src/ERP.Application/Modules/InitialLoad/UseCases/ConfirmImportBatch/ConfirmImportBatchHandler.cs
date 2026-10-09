using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;

/// <summary>
/// Productos, Clientes y Proveedores se confirman como una única transacción. Otros tipos conservan su flujo existente.
/// </summary>
public sealed partial class ConfirmImportBatchHandler
    : IRequestHandler<ConfirmImportBatchCommand, Result<ImportBatchConfirmResultDto>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IImportBatchRowRepository _rowRepo;
    private readonly IImportBatchIssueRepository _issueRepo;
    private readonly IReadOnlyDictionary<ImportType, IImportProcessor> _processors;
    private readonly IOperationalContext _ctx;
    private readonly ILogger<ConfirmImportBatchHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmImportBatchHandler(
        IImportBatchRepository batchRepo,
        IImportBatchRowRepository rowRepo,
        IImportBatchIssueRepository issueRepo,
        IReadOnlyDictionary<ImportType, IImportProcessor> processors,
        IOperationalContext ctx,
        ILogger<ConfirmImportBatchHandler> logger,
        IUnitOfWork unitOfWork
    )
    {
        _batchRepo = batchRepo;
        _rowRepo = rowRepo;
        _issueRepo = issueRepo;
        _processors = processors;
        _ctx = ctx;
        _logger = logger;
        _unitOfWork = unitOfWork;
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
            return Result<ImportBatchConfirmResultDto>.NotFound(
                "Lote de importación no encontrado."
            );

        if (!_processors.TryGetValue(batch.ImportType, out var processor))
            return Result<ImportBatchConfirmResultDto>.ValidationFailure(
                "No hay un procesador disponible para este tipo de importación."
            );

        if (batch.ImportType == ImportType.Items)
            return await ConfirmItemsAsync(batch, processor, cancellationToken);

        // Terceros son todo-o-nada y se confirman en una única transacción (Clientes IL-2B,
        // Proveedores IL-3B); nunca pasan por el bucle genérico fila por fila de abajo.
        if (batch.ImportType is ImportType.Customers or ImportType.Suppliers)
            return await ConfirmPartnersAsync(batch, processor,
                batch.ImportType == ImportType.Customers ? "cliente" : "proveedor", cancellationToken);

        batch.BeginConfirming(_ctx.UserId);
        await _batchRepo.SaveChangesAsync(cancellationToken);

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
                    catch (DomainRuleViolationException ex)
                    {
                        // ZH-DOMAIN-RULE-ERROR-SSOT-01: regla de negocio → fila inválida con su
                        // mensaje público (mismo mecanismo semántico que HTTP).
                        errorMessage = ex.Message;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Error técnico: la fila falla igual, pero el detalle queda solo en el log.
                        LogRowFailed(ex, batch.Id, row.RowNumber);
                        errorMessage = RowInternalErrorMessage;
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
        catch (DomainRuleViolationException ex)
        {
            // Regla de negocio fuera del bucle por fila: el lote falla con el mensaje público.
            batch.Fail(ex.Message, _ctx.UserId);
            await _batchRepo.SaveChangesAsync(cancellationToken);
            return Result<ImportBatchConfirmResultDto>.FromDomainRule(ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fallo inesperado fuera del bucle por fila (p. ej. la propia paginación) — el lote no
            // puede quedar en Confirming sin salida: se marca Failed con el motivo y se reporta,
            // en vez de dejarlo bloqueado para siempre.
            LogBatchFailed(ex, batch.Id);
            batch.Fail(BatchInternalErrorMessage, _ctx.UserId);
            await _batchRepo.SaveChangesAsync(cancellationToken);
            return Result<ImportBatchConfirmResultDto>.Failure(
                $"La confirmación del lote falló de forma inesperada: {BatchInternalErrorMessage}"
            );
        }

        batch.CompleteConfirmation(importedRows, failedRows > 0, _ctx.UserId);
        await _batchRepo.SaveChangesAsync(cancellationToken);

        return Result<ImportBatchConfirmResultDto>.Success(
            new ImportBatchConfirmResultDto(batch.Id, batch.Status, importedRows, failedRows)
        );
    }

    private const string RowInternalErrorMessage =
        "Error interno al confirmar la fila. Revise el registro del sistema.";

    private const string BatchInternalErrorMessage = "Error interno del sistema.";

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Fila {RowNumber} del lote {BatchId} falló por un error interno"
    )]
    private partial void LogRowFailed(Exception ex, Guid batchId, int rowNumber);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Confirmación del lote {BatchId} falló por un error interno"
    )]
    private partial void LogBatchFailed(Exception ex, Guid batchId);
}
