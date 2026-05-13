namespace ERP.Domain.Modules.Caja;

/// <summary>Convierte un archivo de extracto bancario en movimientos normalizados.</summary>
public interface IExtractoParser
{
    Task<IReadOnlyList<MovimientoExtractoParseRow>> ParseAsync(Stream stream, CancellationToken ct = default);
}
