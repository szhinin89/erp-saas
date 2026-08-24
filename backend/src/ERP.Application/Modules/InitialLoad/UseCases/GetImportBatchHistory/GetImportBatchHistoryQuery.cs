using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Modules.InitialLoad.Enums;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.UseCases.GetImportBatchHistory;

public sealed record GetImportBatchHistoryQuery(
    ImportType? ImportType = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<ImportBatchDto>>>, ICompanyScopedRequest;
