using ERP.Application.Modules.InitialLoad.DTOs;

namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// Lectura/escritura del .xlsx de la plantilla de Stock Inicial vía ClosedXML — implementado en
/// Infrastructure. Mismo rol que los demás readers de INITIAL-LOAD-ARCH-01.
/// </summary>
public interface IInitialStockImportSheetReader
{
    Task<ImportReadResult> ReadAsync(Stream fileContent, CancellationToken ct);

    Task<byte[]> BuildTemplateAsync(CancellationToken ct);
}
