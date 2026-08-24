using ERP.Application.Modules.InitialLoad.DTOs;

namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// Lectura/escritura del .xlsx de la plantilla de Proveedores vía ClosedXML — implementado en
/// Infrastructure. Mismo rol que <see cref="ICustomerImportSheetReader"/> para Clientes.
/// </summary>
public interface ISupplierImportSheetReader
{
    Task<ImportReadResult> ReadAsync(Stream fileContent, CancellationToken ct);

    Task<byte[]> BuildTemplateAsync(CancellationToken ct);
}
