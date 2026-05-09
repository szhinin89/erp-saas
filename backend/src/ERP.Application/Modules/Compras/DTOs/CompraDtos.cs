using ERP.Domain.Compras.Enums;

namespace ERP.Application.Modules.Compras.DTOs;

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
