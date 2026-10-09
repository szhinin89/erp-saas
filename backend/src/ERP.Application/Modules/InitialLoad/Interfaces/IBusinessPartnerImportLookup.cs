using ERP.Domain.MasterData.Enums;

namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// Lectura de solo consulta del maestro existente para clasificar una fila de terceros (Clientes
/// IL-2A, Proveedores IL-3A). La identificación se compara sin distinguir mayúsculas — el índice
/// único de BD es exacto, así que una coincidencia exacta dejaría crear "AB123" junto a un "ab123"
/// manual. BusinessPartner y rol son tenant-wide; la condición de pago se lee solo de la Company
/// operativa (filtros globales fail-closed): ventas para Cliente, compras para Proveedor.
/// Un rol revocado se distingue de "sin rol": se reactiva con sus datos previos, nunca se recrea.
/// <c>RevokedRoleHasFiscalData</c> indica si el rol Proveedor revocado conserva su config fiscal.
/// </summary>
public interface IBusinessPartnerImportLookup
{
    Task<BusinessPartnerImportMatch?> FindByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        RoleType role,
        CancellationToken ct
    );
}

public sealed record BusinessPartnerImportMatch(
    Guid BusinessPartnerId,
    bool IsActive,
    string IdentificationNumber,
    string LegalName,
    bool HasActiveRole,
    bool HasCompanySettings,
    Guid? CompanyPaymentTermId,
    bool IsAmbiguous = false,
    bool HasRevokedRole = false,
    bool RevokedRoleHasFiscalData = false
);
