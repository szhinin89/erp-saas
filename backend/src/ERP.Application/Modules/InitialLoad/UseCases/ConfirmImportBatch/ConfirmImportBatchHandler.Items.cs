using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;

public sealed partial class ConfirmImportBatchHandler
{
    private async Task<Result<ImportBatchConfirmResultDto>> ConfirmItemsAsync(
        ImportBatch batch, IImportProcessor processor, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            batch = await _batchRepo.GetByIdForUpdateAsync(batch.Id, _ctx.TenantId, _ctx.CompanyId, ct)
                ?? throw new DomainRuleViolationException("Lote de importación no encontrado.");
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
            if (processor is not ICatalogImportConfirmation catalogConfirmation)
                throw new DomainRuleViolationException("El procesador no soporta confirmación de catálogos.");

            batch.BeginConfirming(_ctx.UserId);
            await _batchRepo.SaveChangesAsync(ct);
            var importedRows = 0;
            const int pageSize = 200;
            while (true)
            {
                var page = await _rowRepo.GetValidRowsPageAsync(batch.Id, batch.TenantId, batch.CompanyId, pageSize, ct);
                if (page.Count == 0)
                    break;
                foreach (var row in page)
                {
                    if (row.ParsedData is null)
                        throw new DomainRuleViolationException($"La fila {row.RowNumber} no tiene datos validados.");
                    var result = await catalogConfirmation.ConfirmRowAsync(row.ParsedData, batch.AutoCreateCatalogValues, ct);
                    if (!result.IsSuccess)
                        throw new DomainRuleViolationException($"Fila {row.RowNumber}: {result.Error ?? "No se pudo crear el ítem."}");
                    row.MarkImported(result.BusinessPartnerId!.Value, _ctx.UserId);
                    importedRows++;
                }
                await _rowRepo.SaveChangesAsync(ct);
                if (page.Count < pageSize)
                    break;
            }
            if (importedRows != batch.TotalRows)
                throw new DomainRuleViolationException("El lote no pudo confirmarse completo.");
            batch.CompleteConfirmation(importedRows, anyRowsFailed: false, _ctx.UserId);
            await _batchRepo.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<ImportBatchConfirmResultDto>.Success(
                new(batch.Id, batch.Status, importedRows, FailedRows: 0));
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

    private async Task RollbackItemsAsync()
    {
        // Incluso si la request fue cancelada, el rollback debe completar su limpieza.
        try { await _unitOfWork.RollbackAsync(CancellationToken.None); }
        finally { _unitOfWork.ClearChangeTracker(); }
    }
}
