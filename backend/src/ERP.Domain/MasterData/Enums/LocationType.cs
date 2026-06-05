namespace ERP.Domain.MasterData.Enums;

/// <summary>
/// Tipo físico de la ubicación del BusinessPartner.
///
/// note: BillingAddr fue eliminado deliberadamente. El propósito de facturación
/// es ortogonal al tipo físico — se modela con LocationPurpose (flags).
/// Una Matriz puede ser simultáneamente punto de entrega Y dirección de facturación.
/// </summary>
public enum LocationType : short
{
    Matrix        = 1,
    Branch        = 2,
    Office        = 3,
    Warehouse     = 4,
    DeliveryPoint = 5,
    Other         = 99,
}
