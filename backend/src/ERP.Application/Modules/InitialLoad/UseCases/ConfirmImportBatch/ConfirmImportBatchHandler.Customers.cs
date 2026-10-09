using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Entities;
using FluentValidation;

namespace ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;

/// <summary>
/// IL-2B — Clientes se confirma como una única transacción: BP + rol Cliente + contacto +
/// <c>CompanyBpSalesSettings</c> de todas las filas quedan juntos o no queda ninguno. Los comandos
/// MediatR anidados comparten el DbContext de la request, así que sus SaveChanges (y el Outbox de
/// sus eventos) participan de esta transacción. Concurrencia/retry/recovery: IL-2C.
/// </summary>
public sealed partial class ConfirmImportBatchHandler
{
    private async Task<Result<ImportBatchConfirmResultDto>> ConfirmCustomersAsync(
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
                    RowConfirmResult result;
                    try
                    {
                        result = await processor.ConfirmRowAsync(row.ParsedData, ct);
                    }
                    catch (ValidationException ex)
                    {
                        // Un comando MasterData anidado rechazó la fila (ValidationBehavior lanza):
                        // es un dato inválido, no un error interno — se reporta con su fila.
                        result = RowConfirmResult.Failed(
                            string.Join(" ", ex.Errors.Select(e => e.ErrorMessage).Distinct()));
                    }
                    if (!result.IsSuccess)
                        throw new DomainRuleViolationException(
                            $"Fila {row.RowNumber}: {result.Error ?? "No se pudo confirmar el cliente."} "
                                + "No se importó ningún cliente del lote.");
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
}
