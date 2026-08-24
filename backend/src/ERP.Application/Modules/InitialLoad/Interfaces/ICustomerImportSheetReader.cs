using ERP.Application.Modules.InitialLoad.DTOs;

namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// Lectura/escritura del formato Excel de la plantilla de Clientes — implementado en
/// Infrastructure con ClosedXML (regla: mapeo de formato externo → dominio vive en
/// Infrastructure, nunca en Application/Domain). <c>CustomerImportProcessor</c> (Application)
/// solo conoce esta interfaz, nunca ClosedXML directamente.
/// </summary>
public interface ICustomerImportSheetReader
{
    Task<ImportReadResult> ReadAsync(Stream fileContent, CancellationToken ct);

    Task<byte[]> BuildTemplateAsync(CancellationToken ct);
}
