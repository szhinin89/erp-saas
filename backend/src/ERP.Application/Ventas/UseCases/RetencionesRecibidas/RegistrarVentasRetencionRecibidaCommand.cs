using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Ventas.UseCases.RetencionesRecibidas;

public sealed record RegistrarVentasRetencionRecibidaCommand(
    Guid VentasFacturaId,
    string XmlContent) : IRequest<Result<Guid>>;
