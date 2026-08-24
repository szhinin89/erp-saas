using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.PreviewImportBatch;

public sealed record PreviewImportBatchQuery(
    Guid ImportBatchId,
    int Page = 1,
    int PageSize = 50,
    bool? OnlyWithBlockingIssue = null
) : IRequest<Result<PagedResult<ImportBatchRowPreviewDto>>>, ICompanyScopedRequest;
