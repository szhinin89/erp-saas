namespace ERP.Domain.Modules.Sales.Enums;

/// <summary>
/// Forma de reembolso elegida explícitamente por el operador al autorizar una devolución
/// (P0-01, decisión de producto 2 — sin prorrateo automático entre formas de pago).
/// </summary>
public enum SalesReturnRefundMethod
{
    Cash = 1,
    ReceivableCredit = 2,
}
