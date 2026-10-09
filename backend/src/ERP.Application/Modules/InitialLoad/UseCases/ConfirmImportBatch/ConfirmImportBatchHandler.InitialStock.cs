using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Entities;
using FluentValidation;

namespace ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;

/// <summary>
/// IL-4B — Inventario Inicial se confirma como UNA transacción: todos los documentos de apertura
/// (uno por bodega), sus líneas, CurrentStock, Kardex y la numeración quedan juntos o no queda
/// nada. Nunca pasa por el bucle genérico fila por fila. Bloqueo/idempotencia del lote: IL-4C.
/// </summary>
public sealed partial class ConfirmImportBatchHandler
{
    private async Task<Result<ImportBatchConfirmResultDto>> ConfirmInitialStockAsync(
        ImportBatch batch, IImportProcessor processor, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var errors = await _rowRepo.GetPageAsync(batch.Id, batch.TenantId, batch.CompanyId,
                pageNumber: 1, pageSize: 1, onlyWithBlockingIssue: true, ct);
            if (batch.IssueRows > 0 || errors.TotalCount > 0 || batch.ValidRows != batch.TotalRows)
                throw new DomainRuleViolationException(
                    "El lote tiene errores. Debe corregir todas las filas antes de confirmar.");
            if (processor is not IBatchImportConfirmation batchConfirmation)
                throw new DomainRuleViolationException("El procesador no soporta confirmación por lote.");

            batch.BeginConfirming(_ctx.UserId);
            await _batchRepo.SaveChangesAsync(ct);

            var rows = new List<ImportBatchRow>();
            const int pageSize = 200;
            for (var pageNumber = 1; ; pageNumber++)
            {
                var page = await _rowRepo.GetPageAsync(batch.Id, batch.TenantId, batch.CompanyId,
                    pageNumber, pageSize, onlyWithBlockingIssue: false, ct);
                rows.AddRange(page.Rows);
                if (page.Rows.Count < pageSize || rows.Count >= page.TotalCount)
                    break;
            }
            if (rows.Count != batch.TotalRows || rows.Any(r => r.ParsedData is null || r.IsImported))
                throw new DomainRuleViolationException("El lote no tiene filas validadas pendientes de confirmar.");

            BatchConfirmResult result;
            try
            {
                result = await batchConfirmation.ConfirmBatchAsync(
                    rows.Select(r => (r.RowNumber, r.ParsedData!)).ToList(), ct);
            }
            catch (ValidationException ex)
            {
                result = BatchConfirmResult.Failed(string.Join(" ", ex.Errors.Select(e => e.ErrorMessage).Distinct()));
            }
            if (!result.IsSuccess)
                throw new DomainRuleViolationException(
                    $"{result.Error} No se registró ningún saldo inicial del lote.");

            foreach (var row in rows)
                row.MarkImported(result.CreatedIdsByRow![row.RowNumber], _ctx.UserId);
            await _rowRepo.SaveChangesAsync(ct);
            batch.CompleteConfirmation(rows.Count, anyRowsFailed: false, _ctx.UserId);
            await _batchRepo.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<ImportBatchConfirmResultDto>.Success(
                new(batch.Id, batch.Status, rows.Count, FailedRows: 0));
        }
        catch (DomainRuleViolationException ex)
        {
            await RollbackItemsAsync();
            return Result<ImportBatchConfirmResultDto>.FromDomainRule(ex);
        }
        catch (OperationCanceledException)
        {
            await RollbackItemsAsync();
            throw;
        }
        catch (Exception ex)
        {
            await RollbackItemsAsync();
            LogBatchFailed(ex, batch.Id);
            return Result<ImportBatchConfirmResultDto>.Failure(BatchInternalErrorMessage);
        }
    }
}
