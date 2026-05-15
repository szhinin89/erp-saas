using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Application.Modules.Expenses.DTOs;

public sealed record GastoFacturaDto(
    Guid         Id,
    string?      ClaveAcceso,
    DateTime     FechaEmision,
    Guid?        ProveedorId,
    string?      NumeroFactura,
    string       Concepto,
    string       CategoriaGasto,
    decimal      Subtotal,
    decimal      Impuesto,
    decimal      Total,
    EstadoGasto Estado,
    string?      XmlPath,
    string?      Observaciones,
    Guid?        AsientoContableId,
    DateTime     CreatedAt);
