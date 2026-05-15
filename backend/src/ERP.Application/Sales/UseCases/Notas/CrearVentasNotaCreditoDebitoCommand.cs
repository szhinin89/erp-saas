using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.Notas;

public sealed record CrearVentasNotaItemDto(Guid    ProductId, decimal Quantity, decimal UnitPrice);

public sealed record CrearSalesNoteCommand(
    Guid   OriginalBillId,
    string NoteType,
    string Reason,
    IReadOnlyList<CrearVentasNotaItemDto> Items) : IRequest<Result<Guid>>;
