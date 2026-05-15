using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.Notas;

public sealed record CrearSalesNoteItemDto(Guid    ProductId, decimal Quantity, decimal UnitPrice);

public sealed record CrearSalesNoteCommand(
    Guid   OriginalBillId,
    string NoteType,
    string Reason,
    IReadOnlyList<CrearSalesNoteItemDto> Items) : IRequest<Result<Guid>>;
