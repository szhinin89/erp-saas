using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.SalesNotes;

public sealed record SalesNoteItemDto(Guid    ProductId, decimal Quantity, decimal UnitPrice);

public sealed record CreateSalesNoteCommand(
    Guid   OriginalBillId,
    string NoteType,
    string Reason,
    IReadOnlyList<SalesNoteItemDto> Items) : IRequest<Result<Guid>>, ICompanyScopedRequest;
