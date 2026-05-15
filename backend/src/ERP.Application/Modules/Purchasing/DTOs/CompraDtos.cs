using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Application.Modules.Purchasing.DTOs;

public record CompraDetalleDto(
    Guid     Id,
    Guid?    ProductoId,
    string   Descripcion,
    string?  CodigoPrincipalProveedor,
    decimal  Cantidad,
    decimal  PrecioUnitario,
    decimal  DescuentoPorcentaje,
    decimal  Subtotal,
    decimal  IvaPorcentaje,
    decimal  IvaValor,
    decimal  Total);

public record CompraFacturaDto(
    Guid          Id,
    Guid          ProveedorId,
    string        NumeroFactura,
    string?       ClaveAcceso,
    string?       XmlPath,
    DateTime      FechaFactura,
    DateTime?     FechaVencimiento,
    EstadoCompra  Estado,
    string        CondicionPago,
    decimal       Subtotal,
    decimal       IvaTotal,
    decimal       Total,
    string?       Observaciones,
    Guid?         AsientoContableId,
    DateTime      CreatedAt);

public record CompraFacturaDetailDto(
    Guid          Id,
    Guid          ProveedorId,
    string        NumeroFactura,
    string?       ClaveAcceso,
    string?       XmlPath,
    DateTime      FechaFactura,
    DateTime?     FechaVencimiento,
    EstadoCompra  Estado,
    string        CondicionPago,
    decimal       Subtotal,
    decimal       IvaTotal,
    decimal       Total,
    string?       Observaciones,
    Guid?         ValidadoPor,
    DateTime?     ValidadoEn,
    Guid?         AprobadoPor,
    DateTime?     AprobadoEn,
    Guid?         RechazadoPor,
    DateTime?     RechazadoEn,
    string?       MotivoRechazo,
    Guid?         AsientoContableId,
    DateTime      CreatedAt,
    IReadOnlyList<CompraDetalleDto> Detalles);

public record CompraNotaProveedorDto(
    Guid      Id,
    Guid      ProveedorId,
    Guid?     CompraFacturaId,
    Guid?     GastoFacturaId,
    string    TipoNota,
    string    Motivo,
    string    ClaveAcceso,
    DateTime  FechaEmision,
    string    Establecimiento,
    string    PuntoEmision,
    string    Secuencial,
    decimal   Subtotal,
    decimal   Impuesto,
    decimal   Total,
    string    Estado,
    string?   XmlPath,
    Guid?     AsientoContableId,
    DateTime  CreatedAt);
