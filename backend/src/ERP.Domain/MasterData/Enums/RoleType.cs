namespace ERP.Domain.MasterData.Enums;

/// <summary>
/// Tipo de rol que un BusinessPartner puede tener dentro del tenant.
///
/// REGLA: Agregar un nuevo rol = nuevo valor aquí + (opcionalmente) nueva tabla de config.
/// PROHIBIDO: agregar columnas IsCustomer/IsSupplier/IsXxx en ninguna entidad.
/// PROHIBIDO: switch(RoleType) fuera de BusinessPartnerRole.Create() y UpdateConfig().
/// </summary>
public enum RoleType : short
{
    Customer    = 1,
    Supplier    = 2,
    Employee    = 3,
    Carrier     = 4,
    Broker      = 5,
    Agent       = 6,
    Distributor = 7,
    Contractor  = 8,
}
