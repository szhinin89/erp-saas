using System.Globalization;

namespace ERP.Domain.Modules.Sales.Policies;

/// <summary>
/// Textos de los mensajes funcionales de la política fiscal de Consumidor Final. Único punto de
/// estos mensajes — los usan tanto la validación de autoridad en Ventas (autorización de
/// factura) como los DTOs de lectura (Fiscal/Tributario, SalesRuntimeContext), para que el
/// frontend nunca tenga que redactar ni duplicar el texto.
/// </summary>
public static class SalesFiscalPolicyMessages
{
    public const string CreditBlockedMessage =
        "Consumidor Final no puede registrar ventas a crédito. Seleccione un cliente identificado o cambie la condición de pago a contado.";

    public static string AmountExceededMessage(decimal maxAmount) =>
        $"El monto máximo para facturar a Consumidor Final es {maxAmount.ToString("0.00", CultureInfo.InvariantCulture)}. Seleccione un cliente identificado para continuar.";
}
