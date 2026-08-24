using ERP.Application.Common;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;

public sealed class CancelImportBatchHandler : IRequestHandler<CancelImportBatchCommand, Result<bool>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IOperationalContext _ctx;

    public CancelImportBatchHandler(IImportBatchRepository batchRepo, IOperationalContext ctx)
    {
        _batchRepo = batchRepo;
        _ctx = ctx;
    }

    public async Task<Result<bool>> Handle(
        CancelImportBatchCommand cmd,
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
            return Result<bool>.NotFound("Lote de importación no encontrado.");

        try
        {
            batch.Cancel(_ctx.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _batchRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
