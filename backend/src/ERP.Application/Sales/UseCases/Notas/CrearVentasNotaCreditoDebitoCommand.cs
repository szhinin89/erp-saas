using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.Notas;

public sealed record CrearVentasNotaItemDto(Guid ProductoId, decimal Cantidad, decimal PrecioUnitario);

public sealed record CrearVentasNotaCreditoDebitoCommand(
    Guid FacturaOriginalId,
    string TipoNota,
    string Motivo,
    IReadOnlyList<CrearVentasNotaItemDto> Items) : IRequest<Result<Guid>>;
