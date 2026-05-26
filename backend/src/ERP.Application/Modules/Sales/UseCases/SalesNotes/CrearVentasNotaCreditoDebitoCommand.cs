using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.SalesNotes;

public sealed record CrearSalesNoteItemDto(Guid    ProductId, decimal Quantity, decimal UnitPrice);

public sealed record CreateSalesNoteCommand(
    Guid   OriginalBillId,
    string NoteType,
    string Reason,
    IReadOnlyList<CrearSalesNoteItemDto> Items) : IRequest<Result<Guid>>, ICompanyScopedRequest;
