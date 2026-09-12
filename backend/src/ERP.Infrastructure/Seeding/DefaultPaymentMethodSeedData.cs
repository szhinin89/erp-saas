using ERP.Domain.Modules.Sales.Enums;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// SALES-PAYMENT-METHOD-SRI-MAPPING-EFECTIVO-WRONG-CODE-01: única tabla de valores INICIALES de
/// seed/backfill para las 5 formas de cobro Tipo A — consumida tanto por
/// <see cref="Steps.SalesBootstrapStep"/> (empresas nuevas) como por
/// <see cref="PaymentMethodSriMappingBackfillService"/> (empresas ya existentes cuyo
/// PaymentMethod.SriPaymentMethodCode quedó null por no haber pasado por este bootstrap). Antes de
/// esta corrección existían dos fuentes potenciales del mismo dato — este archivo es la única.
///
/// No es lógica productiva: nunca se lee en el flujo de emisión (SalesInvoiceElectronicDocumentDataProvider
/// solo lee PaymentMethod.SriPaymentMethodCode desde BD). Es exclusivamente el valor con el que se
/// crea/backfillea una fila cuando aún no tiene mapeo configurado — el operador de cada empresa
/// puede cambiarlo en cualquier momento vía UpdatePaymentMethodCommand.
///
/// Códigos según catálogo SRI Ecuador: 01 Sin utilización del sistema financiero, 19 Tarjeta de
/// crédito, 20 Otros con utilización del sistema financiero. "CREDITO" (venta a plazo, no es un
/// instrumento de cobro real) queda sin mapeo — al momento del cobro real se registra con la forma
/// de pago efectiva.
/// </summary>
public static class DefaultPaymentMethodSeedData
{
    public static readonly (
        string Code,
        string Name,
        bool RequiresRef,
        bool CreditAllowed,
        int Sort,
        PaymentMethodDetailType DetailType,
        string? SriPaymentMethodCode
    )[] Entries =
    [
        ("EFECTIVO", "Efectivo", false, false, 1, PaymentMethodDetailType.None, "01"),
        ("TARJETA", "Tarjeta de Crédito", true, false, 2, PaymentMethodDetailType.Card, "19"),
        (
            "TRANSFERENCIA",
            "Transferencia Bancaria",
            true,
            false,
            3,
            PaymentMethodDetailType.Transfer,
            "20"
        ),
        ("CHEQUE", "Cheque", true, false, 4, PaymentMethodDetailType.Check, "20"),
        ("CREDITO", "Crédito", false, true, 5, PaymentMethodDetailType.None, null),
    ];

    /// <summary>Código SRI inicial sugerido para un Code de PaymentMethod conocido, o null si no hay entrada/mapeo.</summary>
    public static string? SriCodeFor(string code) =>
        Array.Find(Entries, e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase))
            .SriPaymentMethodCode;
}
