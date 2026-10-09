using ERP.Application.Common;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;

public sealed class CancelImportBatchHandler
    : IRequestHandler<CancelImportBatchCommand, Result<bool>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public CancelImportBatchHandler(
        IImportBatchRepository batchRepo,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    )
    {
        _batchRepo = batchRepo;
        _ctx = ctx;
        _unitOfWork = unitOfWork;
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
