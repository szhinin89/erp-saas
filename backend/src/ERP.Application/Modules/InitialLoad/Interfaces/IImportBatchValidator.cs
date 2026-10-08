using ERP.Application.Modules.InitialLoad.DTOs;

namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>Validaciones entre filas sobre el resultado completo, sin estado entre lotes.</summary>
public interface IImportBatchValidator
{
    IReadOnlyList<RowValidationResult> ValidateBatch(IReadOnlyList<RowValidationResult> rows);
}
