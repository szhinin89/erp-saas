using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// Seam de extensibilidad del motor de Carga Inicial (INITIAL-LOAD-ARCH-01). Un <c>IImportProcessor</c>
/// por <see cref="ImportType"/>, resuelto en DI vía <c>IReadOnlyDictionary&lt;ImportType, IImportProcessor&gt;</c>
/// construido desde todas las implementaciones registradas. Agregar un tipo de importación nuevo
/// (Suppliers/Items/Prices/InitialStock) es agregar una implementación — el motor genérico
/// (entidades, casos de uso, controller) no cambia.
/// </summary>
public interface IImportProcessor
{
    ImportType ImportType { get; }

    string TemplateFileName { get; }

    Task<ImportTemplateFileDto> BuildTemplateAsync(CancellationToken ct);

    Task<ImportReadResult> ReadAsync(Stream fileContent, CancellationToken ct);

    Task<RowValidationResult> ValidateRowAsync(
        int rowNumber,
        IReadOnlyDictionary<string, string?> rawRow,
        CancellationToken ct
    );

    Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct);
}
