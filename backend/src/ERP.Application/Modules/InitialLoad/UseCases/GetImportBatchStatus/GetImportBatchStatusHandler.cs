using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.GetImportBatchStatus;

public sealed class GetImportBatchStatusHandler
    : IRequestHandler<GetImportBatchStatusQuery, Result<ImportBatchDto>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IOperationalContext _ctx;

    public GetImportBatchStatusHandler(IImportBatchRepository batchRepo, IOperationalContext ctx)
    {
        _batchRepo = batchRepo;
        _ctx = ctx;
    }

    public async Task<Result<ImportBatchDto>> Handle(
        GetImportBatchStatusQuery query,
        CancellationToken cancellationToken
    )
    {
        var batch = await _batchRepo.GetByIdAsync(
            query.ImportBatchId,
            _ctx.TenantId,
            _ctx.CompanyId,
            cancellationToken
        );
        return batch is null
            ? Result<ImportBatchDto>.NotFound("Lote de importación no encontrado.")
            : Result<ImportBatchDto>.Success(ImportBatchDto.From(batch));
    }
}
