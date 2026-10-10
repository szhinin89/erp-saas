using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using FluentValidation;

namespace ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;

/// <summary>
/// IL-4B — Inventario Inicial se confirma como UNA transacción: todos los documentos de apertura
/// (uno por bodega), sus líneas, CurrentStock, Kardex y la numeración quedan juntos o no queda
/// nada. Nunca pasa por el bucle genérico fila por fila.
/// IL-4C — FOR UPDATE por lote + Tenant + Company serializa confirmaciones/revalidaciones/
/// cancelaciones; un lote Completed devuelve su resultado sin re-ejecutar; el staging debe ser de la
/// sucursal activa.
/// </summary>
public sealed partial class ConfirmImportBatchHandler
{
    private async Task<Result<ImportBatchConfirmResultDto>> ConfirmInitialStockAsync(
        ImportBatch batch, IImportProcessor processor, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // IL-8E — orden fijo empresa → lote (el de la apertura): serializa con el cierre definitivo
            // de la Carga Inicial; el estado de cierre se lee después de ambos bloqueos.
            await _batchRepo.LockCompanyAsync(_ctx.TenantId, _ctx.CompanyId, ct);
            batch = await _batchRepo.GetByIdForUpdateAsync(batch.Id, _ctx.TenantId, _ctx.CompanyId, ct)
                ?? throw new DomainRuleViolationException("Lote de importación no encontrado.");
            if (processor is IOpeningBalanceImport opening
                && await opening.CheckInitialLoadOpenAsync(ct) is { } closed)
            {
                await RollbackItemsAsync();
                return Result<ImportBatchConfirmResultDto>.ValidationFailure(closed, InitialLoadClosedGuard.Code);
            }
            var staged = await _rowRepo.GetAllRowsAsync(batch, ct);
            if (processor is IImportBatchScopeGuard scopeGuard)
            {
                var scopeError = await scopeGuard.CheckStagingScopeAsync(
                    staged.Where(r => r.ParsedData is not null).Select(r => r.ParsedData!).ToList(), ct);
                if (scopeError is not null)
                    throw new DomainRuleViolationException(scopeError);
            }
            if (batch.Status == ImportStatus.Completed)
            {
                await _unitOfWork.CommitAsync(ct);
                return Result<ImportBatchConfirmResultDto>.Success(
                    new(batch.Id, batch.Status, batch.ImportedRows, FailedRows: 0));
            }

            var errors = await _rowRepo.GetPageAsync(batch.Id, batch.TenantId, batch.CompanyId,
                pageNumber: 1, pageSize: 1, onlyWithBlockingIssue: true, ct);
            if (batch.IssueRows > 0 || errors.TotalCount > 0 || batch.ValidRows != batch.TotalRows)
                throw new DomainRuleViolationException(
                    "El lote tiene errores. Debe corregir todas las filas antes de confirmar.");
            if (processor is not IBatchImportConfirmation batchConfirmation)
                throw new DomainRuleViolationException("El procesador no soporta confirmación por lote.");

            batch.BeginConfirming(_ctx.UserId);
            await _batchRepo.SaveChangesAsync(ct);

            var rows = staged;
            if (rows.Count != batch.TotalRows || rows.Any(r => r.ParsedData is null || r.IsImported))
                throw new DomainRuleViolationException("El lote no tiene filas validadas pendientes de confirmar.");

            BatchConfirmResult result;
            try
            {
                result = await batchConfirmation.ConfirmBatchAsync(
                    batch.Id, rows.Select(r => (r.Id, r.RowNumber, r.ParsedData!)).ToList(), ct);
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
