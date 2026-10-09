namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// Lectura de solo consulta del maestro existente para clasificar una fila de Clientes (IL-2A).
/// La identificación se compara sin distinguir mayúsculas — el índice único de BD es exacto, así
/// que una coincidencia exacta dejaría crear "AB123" junto a un "ab123" manual. BusinessPartner y
/// rol son tenant-wide; las condiciones de venta se leen solo de la Company operativa (filtros
/// globales fail-closed).
/// </summary>
public interface ICustomerImportLookup
{
    Task<CustomerImportMatch?> FindByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        CancellationToken ct
    );
}

public sealed record CustomerImportMatch(
    Guid BusinessPartnerId,
    bool IsActive,
    string IdentificationNumber,
    string LegalName,
    bool HasActiveCustomerRole,
    bool HasCompanySalesSettings,
    Guid? CompanyPaymentTermId,
    bool IsAmbiguous = false
);
