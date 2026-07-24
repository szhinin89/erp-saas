namespace ERP.Application.Modules.ElectronicInvoicing.Enums;

/// <summary>
/// Disponibilidad observada del webservice SRI. <see cref="Unknown"/> cuando no se llegó a
/// comprobar (p. ej. porque la configuración o el certificado ya son inválidos y comprobar
/// conectividad sería una llamada de red desperdiciada — ver
/// <see cref="UseCases.GetElectronicInvoicingStatus.GetElectronicInvoicingStatusQueryHandler"/>).
/// </summary>
public enum SriAvailability
{
    Unknown = 0,
    Available = 1,
    Unavailable = 2,
}
