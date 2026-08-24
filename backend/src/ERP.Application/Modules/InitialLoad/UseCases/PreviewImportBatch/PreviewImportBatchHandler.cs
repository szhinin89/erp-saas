using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.PreviewImportBatch;

/// <summary>
/// Lectura paginada pura — nunca toca la cabecera <c>ImportBatch</c>. Posible porque las filas
/// son un agregado propio (ver nota de diseño en <c>ImportBatch</c>), no una colección cargada
/// en memoria de la cabecera.
/// </summary>
public sealed class PreviewImportBatchHandler
    : IRequestHandler<PreviewImportBatchQuery, Result<PagedResult<ImportBatchRowPreviewDto>>>
{
    private readonly IImportBatchRowRepository _rowRepo;
    private readonly IImportBatchIssueRepository _issueRepo;
    private readonly IOperationalContext _ctx;

    public PreviewImportBatchHandler(
        IImportBatchRowRepository rowRepo,
        IImportBatchIssueRepository issueRepo,
        IOperationalContext ctx
    )
    {
        _rowRepo = rowRepo;
        _issueRepo = issueRepo;
        _ctx = ctx;
    }

    public async Task<Result<PagedResult<ImportBatchRowPreviewDto>>> Handle(
        PreviewImportBatchQuery query,
        CancellationToken cancellationToken
    )
    {
        var pageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize;
        var page = query.Page < 1 ? 1 : query.Page;

        var (rows, total) = await _rowRepo.GetPageAsync(
            query.ImportBatchId,
            _ctx.TenantId,
            _ctx.CompanyId,
            page,
            pageSize,
            query.OnlyWithBlockingIssue,
            cancellationToken
        );

        var issues = await _issueRepo.GetByRowIdsAsync(
            rows.Select(r => r.Id).ToList(),
            cancellationToken
        );
        var issuesByRow = issues.ToLookup(i => i.ImportBatchRowId);

        var items = rows
            .Select(r => new ImportBatchRowPreviewDto(
                r.Id,
                r.RowNumber,
                r.HasBlockingIssue,
                r.IsImported,
                r.CreatedBusinessPartnerId,
                JsonSerializer.Deserialize<Dictionary<string, string?>>(r.RawData) ?? new(),
                issuesByRow[r.Id].Select(ImportBatchIssueDto.From).ToList()
            ))
            .ToList();

        return Result<PagedResult<ImportBatchRowPreviewDto>>.Success(
            new PagedResult<ImportBatchRowPreviewDto>(items, page, pageSize, total)
        );
    }
}
