namespace ERP.Domain.Modules.Sales.Enums;

/// <summary>
/// Metadata de negocio de <see cref="Entities.PaymentMethod"/>: qué esquema de detalle (tarjeta,
/// transferencia, cheque) debe capturar la UI al registrar un pago con este método. Reemplaza la
/// comparación por <c>Code</c>/<c>Name</c> que antes vivía hardcodeada en el frontend — el frontend
/// nunca decide el esquema de captura, solo lo consume desde aquí.
/// </summary>
public enum PaymentMethodDetailType
{
    None = 0,
    Card = 1,
    Transfer = 2,
    Check = 3,
}
