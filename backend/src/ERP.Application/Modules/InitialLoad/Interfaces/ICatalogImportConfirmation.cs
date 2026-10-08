using ERP.Application.Modules.InitialLoad.DTOs;

namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>La opción de creación pertenece al lote, nunca al JSON de la fila.</summary>
public interface ICatalogImportConfirmation
{
    Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, bool autoCreateCatalogValues, CancellationToken ct);
}
