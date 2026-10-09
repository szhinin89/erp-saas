using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;

public sealed class CancelImportBatchHandler
    : IRequestHandler<CancelImportBatchCommand, Result<bool>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImportBatchRowRepository _rowRepo;
    private readonly IReadOnlyDictionary<ImportType, IImportProcessor> _processors;

    public CancelImportBatchHandler(
        IImportBatchRepository batchRepo,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork,
        IImportBatchRowRepository rowRepo,
        IReadOnlyDictionary<ImportType, IImportProcessor> processors
    )
    {
        _batchRepo = batchRepo;
        _ctx = ctx;
        _unitOfWork = unitOfWork;
        _rowRepo = rowRepo;
        _processors = processors;
    }

    public async Task<Result<bool>> Handle(
        CancelImportBatchCommand cmd,
        CancellationToken cancellationToken
    )
    {
        // IL-2C: mismo FOR UPDATE que Validate/Confirm. Sin él, un Cancel concurrente esperaba el
        // lock de una confirmación en curso y, al liberarse, sobrescribía Completed con Cancelled
        // (los datos ya importados quedaban bajo un lote "cancelado").
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var batch = await _batchRepo.GetByIdForUpdateAsync(
                cmd.ImportBatchId,
                _ctx.TenantId,
                _ctx.CompanyId,
                cancellationToken
            );
            if (batch is null)
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);
                return Result<bool>.NotFound("Lote de importación no encontrado.");
            }
            // IL-4C: alcance derivado del staging (p. ej. sucursal de Inventario Inicial).
            if (_processors.TryGetValue(batch.ImportType, out var processor)
                && processor is IImportBatchScopeGuard scopeGuard)
            {
                var staged = await _rowRepo.GetAllRowsAsync(batch, cancellationToken);
                var scopeError = await scopeGuard.CheckStagingScopeAsync(
                    staged.Where(r => r.ParsedData is not null).Select(r => r.ParsedData!).ToList(),
                    cancellationToken);
                if (scopeError is not null)
                    throw new DomainRuleViolationException(scopeError);
            }
            batch.Cancel(_ctx.UserId);
            await _batchRepo.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            try { await _unitOfWork.RollbackAsync(CancellationToken.None); }
            finally { _unitOfWork.ClearChangeTracker(); }
            throw;
        }
        return Result<bool>.Success(true);
    }
}
