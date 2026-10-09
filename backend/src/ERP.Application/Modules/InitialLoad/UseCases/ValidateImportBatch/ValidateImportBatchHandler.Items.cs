using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;

public sealed partial class ValidateImportBatchHandler
{
    private async Task<Result<ImportBatchDto>> ValidateReplacingStagingAsync(
        ImportBatch batch, IImportProcessor processor, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            batch = await _batchRepo.GetByIdForUpdateAsync(batch.Id, _ctx.TenantId, _ctx.CompanyId, ct)
                ?? throw new DomainRuleViolationException("Lote de importación no encontrado.");
            // Reject terminal/busy states before touching previously validated staging.
            if (batch.Status is not (ImportStatus.Uploaded or ImportStatus.Validated))
                throw new DomainRuleViolationException($"No se puede validar un lote en estado '{batch.Status}'.");
            // IL-4C: alcance derivado del staging (sucursal), comprobado antes de reemplazarlo.
            if (processor is IImportBatchScopeGuard scopeGuard)
            {
                var staged = await _rowRepo.GetAllRowsAsync(batch, ct);
                var scopeError = await scopeGuard.CheckStagingScopeAsync(
                    staged.Where(r => r.ParsedData is not null).Select(r => r.ParsedData!).ToList(), ct);
                if (scopeError is not null)
                    throw new DomainRuleViolationException(scopeError);
            }
            await _issueRepo.DeleteByBatchAsync(batch.Id, batch.TenantId, batch.CompanyId, ct);
            await _rowRepo.DeleteByBatchAsync(batch.Id, batch.TenantId, batch.CompanyId, ct);
            var result = await ValidateRowsAsync(batch, processor, ct);
            if (!result.IsSuccess)
            {
                await RollbackValidationAsync();
                return result;
            }
            await _unitOfWork.CommitAsync(ct);
            return result;
        }
        catch (DomainRuleViolationException ex)
        {
            await RollbackValidationAsync();
            return Result<ImportBatchDto>.FromDomainRule(ex);
        }
        catch (OperationCanceledException)
        {
            await RollbackValidationAsync();
            throw;
        }
        catch (Exception ex)
        {
            await RollbackValidationAsync();
            LogReadFailed(ex, batch.Id);
            return Result<ImportBatchDto>.Failure("No se pudo validar el archivo del lote.");
        }
    }

    private async Task RollbackValidationAsync()
    {
        try { await _unitOfWork.RollbackAsync(CancellationToken.None); }
        finally { _unitOfWork.ClearChangeTracker(); }
    }
}
