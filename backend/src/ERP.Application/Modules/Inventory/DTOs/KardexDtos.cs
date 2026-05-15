namespace ERP.Application.Inventory.DTOs;

public record KardexResponse(
    KardexProductoDto                  Producto,
    KardexBodegaDto                    Warehouse,
    IReadOnlyList<MovimientoKardexDto> Rows,
    ResumenKardexDto                   Resumen);

public record KardexProductoDto(Guid Id, string  Name, string Codigo);

public record KardexBodegaDto(Guid Id, string  Name);

public record MovimientoKardexDto(
    DateTime Fecha,
    string   MovementType,
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

