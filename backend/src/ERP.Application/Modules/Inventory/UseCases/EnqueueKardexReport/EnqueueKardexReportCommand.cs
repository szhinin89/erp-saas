using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Inventory.UseCases.EnqueueKardexReport;

public sealed record EnqueueKardexReportCommand(
    Guid ProductId,
    Guid WarehouseId,
    DateTime? StartDate,
    DateTime? EndDate) : IRequest<Result<EnqueueKardexReportResult>>;

public sealed record EnqueueKardexReportResult(
    Guid JobId,
    string Status,
    string Message);
