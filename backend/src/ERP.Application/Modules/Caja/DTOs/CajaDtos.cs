namespace ERP.Application.Modules.Caja.DTOs;

public sealed record CuentaBancariaDto(
    Guid Id,
    string Nombre,
    string NumeroCuenta,
    string TipoCuenta,
    string Moneda,
    decimal SaldoInicial,
    decimal SaldoActual,
    bool Activo,
    Guid? CuentaContableId);

public sealed record ExtractoBancarioDto(
    Guid Id,
    Guid CuentaBancariaId,
    DateTime PeriodoDesde,
    DateTime PeriodoHasta,
    decimal SaldoInicialExtracto,
    decimal SaldoFinalExtracto,
    DateTime FechaCarga,
    bool Conciliado,
    int CantidadMovimientos);

public sealed record MovimientoBancarioDto(
    Guid Id,
    Guid ExtractoBancarioId,
    DateTime Fecha,
    string Descripcion,
    decimal Monto,
    string Tipo,
    string Referencia,
    Guid? AsientoContableId,
    string Estado);

public sealed record SugerenciaConciliacionDto(
    Guid MovimientoBancarioId,
    Guid? AsientoContableSugeridoId,
    string? Motivo);

public sealed record FlujoEfectivoDiaDto(DateOnly Fecha, decimal Entradas, decimal Salidas, decimal Neto);

public sealed record GastoCajaChicaDto(
    Guid Id,
    Guid CajaChicaId,
    DateTime Fecha,
    string Concepto,
    decimal Monto,
    string TipoComprobante,
    string NumeroComprobante,
    Guid? AsientoContableId);

public sealed record ArqueoCajaDto(
    Guid Id,
    Guid CajaChicaId,
    DateTime FechaArqueo,
    decimal EfectivoFisico,
    decimal Diferencia,
    string Observaciones,
    bool Aprobado);

public sealed record CajaChicaDto(
    Guid Id,
    string Nombre,
    decimal SaldoAsignado,
    decimal SaldoActual,
    Guid? CuentaBancariaIdReposicion,
    Guid? CuentaContableCajaId,
    bool Activo);
