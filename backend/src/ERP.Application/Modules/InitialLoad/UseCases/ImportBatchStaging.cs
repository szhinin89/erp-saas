using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Interfaces;

namespace ERP.Application.Modules.InitialLoad.UseCases;

/// <summary>Lectura completa del staging de un lote (paginada), con el mismo scope Tenant+Company.</summary>
internal static class ImportBatchStaging
{
    private const int PageSize = 200;

    public static async Task<List<ImportBatchRow>> GetAllRowsAsync(
        this IImportBatchRowRepository rows, ImportBatch batch, CancellationToken ct)
    {
        var all = new List<ImportBatchRow>();
        for (var pageNumber = 1; ; pageNumber++)
        {
            var page = await rows.GetPageAsync(batch.Id, batch.TenantId, batch.CompanyId,
                pageNumber, PageSize, onlyWithBlockingIssue: null, ct);
            all.AddRange(page.Rows);
            if (page.Rows.Count < PageSize || all.Count >= page.TotalCount)
                return all;
        }
    }
}
