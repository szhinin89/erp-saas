using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Gastos.DTOs;


namespace ERP.Application.Modules.Gastos.UseCases.CrearGasto;

public enum ModoCreacionGasto { Manual = 1, Xml = 2 }

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record CrearGastoCommand(
    ModoCreacionGasto Modo,
    byte[]?           XmlContent,
    string?           XmlNombreArchivo,
    Guid?             ProveedorId,
    DateTime?         FechaEmision,
    string?           Concepto,
    string?           CategoriaGasto,
    decimal?          Subtotal,
    decimal?          Impuesto,
    decimal?          Total,
    string?           Observaciones
) : IRequest<Result<GastoFacturaDto>>;
