using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.GetImportBatchHistory;

public sealed class GetImportBatchHistoryHandler
    : IRequestHandler<GetImportBatchHistoryQuery, Result<PagedResult<ImportBatchDto>>>
{
    private readonly IImportBatchRepository _batchRepo;
    private readonly IOperationalContext _ctx;

    public GetImportBatchHistoryHandler(IImportBatchRepository batchRepo, IOperationalContext ctx)
    {
        _batchRepo = batchRepo;
        _ctx = ctx;
    }

    public async Task<Result<PagedResult<ImportBatchDto>>> Handle(
        GetImportBatchHistoryQuery query,
        CancellationToken cancellationToken
    )
    {
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;
        var page = query.Page < 1 ? 1 : query.Page;

        var (batches, total) = await _batchRepo.GetPageAsync(
            _ctx.TenantId,
            _ctx.CompanyId,
            query.ImportType,
            page,
            pageSize,
            cancellationToken
        );

        return Result<PagedResult<ImportBatchDto>>.Success(
            new PagedResult<ImportBatchDto>(
                batches.Select(ImportBatchDto.From).ToList(),
                page,
                pageSize,
                total
            )
        );
    }
}
