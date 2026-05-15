namespace ERP.Domain.Modules.Cash;

/// <summary>Fila normalizada tras parsear un extracto (CSV/Excel).</summary>
public sealed record MovimientoExtractoParseRow(
    DateTime Fecha,
    string Descripcion,
    decimal Monto,
    string Tipo,
    string? Referencia);
