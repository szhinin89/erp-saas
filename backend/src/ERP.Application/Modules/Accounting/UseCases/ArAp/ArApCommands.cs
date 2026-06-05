using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Accounting.UseCases.ArAp;

// ── AR ────────────────────────────────────────────────────────────────────────

public sealed record CreateArEntryCommand(
    Guid     BusinessPartnerId,
    string   Reference,
    DateTime IssueDate,
    DateTime DueDate,
    decimal  Amount,
    Guid?    SalesBillId = null,
    string   Currency    = "USD")
    : IRequest<Result<Guid>>, ICompanyScopedRequest;

public sealed record ApplyArPaymentCommand(
    Guid     ArEntryId,
    decimal  Amount,
    DateTime PaymentDate,
    string?  PaymentReference = null,
    string?  Notes            = null)
    : IRequest<Result<bool>>, ISubscriberScopedRequest;

public sealed record GetArAgingReportQuery(DateTime AsOf)
    : IRequest<Result<ArAgingReportDto>>, ICompanyScopedRequest;

// ── AP ────────────────────────────────────────────────────────────────────────

public sealed record CreateApEntryCommand(
    Guid     BusinessPartnerId,
    string   Reference,
    DateTime IssueDate,
    DateTime DueDate,
    decimal  Amount,
    Guid?    PurchBillId = null,
    string   Currency    = "USD")
    : IRequest<Result<Guid>>, ICompanyScopedRequest;

public sealed record ApplyApPaymentCommand(
    Guid     ApEntryId,
    decimal  Amount,
    DateTime PaymentDate,
    string?  PaymentReference = null,
    string?  Notes            = null)
    : IRequest<Result<bool>>, ISubscriberScopedRequest;

public sealed record GetApAgingReportQuery(DateTime AsOf)
    : IRequest<Result<ApAgingReportDto>>, ICompanyScopedRequest;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record ArAgingReportDto(
    IReadOnlyList<ArAgingLineDto> Lines,
    decimal TotalCurrent,
    decimal Total1To30,
    decimal Total31To60,
    decimal Total61To90,
    decimal TotalOver90,
    decimal GrandTotal,
    DateTime AsOf);

public sealed record ArAgingLineDto(
    Guid     ArEntryId,
    Guid     BusinessPartnerId,
    string   Reference,
    DateTime DueDate,
    decimal  Current,
    decimal  Days1To30,
    decimal  Days31To60,
    decimal  Days61To90,
    decimal  Over90,
    decimal  Total);

public sealed record ApAgingReportDto(
    IReadOnlyList<ApAgingLineDto> Lines,
    decimal TotalCurrent,
    decimal Total1To30,
    decimal Total31To60,
    decimal Total61To90,
    decimal TotalOver90,
    decimal GrandTotal,
    DateTime AsOf);

public sealed record ApAgingLineDto(
    Guid     ApEntryId,
    Guid     BusinessPartnerId,
    string   Reference,
    DateTime DueDate,
    decimal  Current,
    decimal  Days1To30,
    decimal  Days31To60,
    decimal  Days61To90,
    decimal  Over90,
    decimal  Total);
