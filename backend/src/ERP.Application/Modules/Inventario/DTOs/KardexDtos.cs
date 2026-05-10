namespace ERP.Application.Inventario.DTOs;

public record KardexResponse(
    KardexProductoDto                  Producto,
    KardexBodegaDto                    Bodega,
    IReadOnlyList<MovimientoKardexDto> Movimientos,
    ResumenKardexDto                   Resumen);

public record KardexProductoDto(Guid Id, string Nombre, string Codigo);

public record KardexBodegaDto(Guid Id, string Nombre);

public record MovimientoKardexDto(
    DateTime Fecha,
    string   TipoMovimiento,
    string?  Referencia,
    decimal  EntradaCantidad,
    decimal  EntradaValor,
    decimal  SalidaCantidad,
    decimal  SalidaValor,
    decimal  SaldoCantidad,
    decimal  SaldoValor,
    decimal  CostoUnitarioPromedio);

public record ResumenKardexDto(
    decimal InventarioInicialCantidad,
    decimal InventarioInicialValor,
    decimal EntradasCantidad,
    decimal EntradasValor,
    decimal SalidasCantidad,
    decimal SalidasValor,
    decimal InventarioFinalCantidad,
    decimal InventarioFinalValor,
    decimal CostoPromedioFinal);
