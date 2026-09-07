using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Application.MasterData.DTOs;

/// <summary>
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01. Los campos de catálogo (TaxType/Code/CodeName/
/// Percentage) son un snapshot de lectura del catálogo SRI en el momento de la consulta — nunca
/// se persisten en SupplierRetentionDefault (SSOT dinámico). <see cref="IsCatalogCodeActive"/>
/// distingue una fila cuyo código de catálogo ya no está activo (huérfano) — se sigue mostrando,
/// nunca se oculta, para que el operador pueda desactivarla explícitamente.
/// </summary>
public sealed record SupplierRetentionDefaultDto(
    Guid Id,
    Guid BusinessPartnerId,
    Guid SriRetentionCodeId,
    string TaxType,
    string Code,
    string CodeName,
    decimal Percentage,
    bool IsCatalogCodeActive,
    bool IsActive,
    int DisplayOrder
)
{
    public static SupplierRetentionDefaultDto From(
        SupplierRetentionDefault entry,
        SriRetentionCode? catalogCode
    ) =>
        new(
            entry.Id,
            entry.BusinessPartnerId,
            entry.SriRetentionCodeId,
            catalogCode?.TaxType ?? "?",
            catalogCode?.Code ?? "?",
            catalogCode?.Name ?? "Código de retención no encontrado en catálogo",
            catalogCode?.Percentage ?? 0,
            catalogCode?.IsActive ?? false,
            entry.IsActive,
            entry.DisplayOrder
        );
}
