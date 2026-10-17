using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

public sealed class SalesInvoiceAuthorizedEvent : BaseDomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public decimal GrandTotal { get; }
    public Guid UserId { get; }

    /// <summary>
    /// Caja que originó la venta (<c>SalesInvoice.CashSessionId</c>) — permite registrar el
    /// movimiento de caja de forma determinística, sin buscar la sesión abierta del usuario.
    /// </summary>
    public Guid CashSessionId { get; }

    public Guid CompanyId { get; }
    public DateOnly IssueDate { get; }

    /// <summary>
    /// Montos ya resueltos por Sales (Configuración Tributaria, infraestructura CLOSED) — ADR-026
    /// §4. Accounting los consume tal cual, nunca los recalcula.
    /// </summary>
    public decimal Subtotal { get; }
    public decimal TotalVat { get; }
    public decimal TotalIce { get; }
    public decimal TotalDiscount { get; }

    /// <summary>TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E — ya resuelto por el dominio de <c>SalesInvoice</c> (<c>SalesInvoice.TotalIrbpnr</c>), Accounting lo consume tal cual, nunca lo recalcula (mismo criterio que <see cref="TotalVat"/>/<see cref="TotalIce"/>).</summary>
    public decimal TotalIrbpnr { get; }

    /// <summary>
    /// SALES-CASH-REAL-MONEY-01 — dinero real recibido en esta venta, excluyendo cualquier porción
    /// cubierta con un método de pago marcado <c>IsCreditAllowed</c> (p. ej. "Crédito" como
    /// marcador de saldo pendiente). Mismo monto que <c>SalesSettlementPolicy.Calculate</c> usa
    /// para decidir la CxC generada (ver <c>AuthorizeSalesInvoiceHandler</c>, commit
    /// SALES-SETTLEMENT-CREDIT-01) — Caja debe consumir este valor tal cual, nunca
    /// <see cref="GrandTotal"/> (que incluye la porción a crédito, si la hubiera).
    /// </summary>
    public decimal CashApplied { get; }

    /// <summary>
    /// ACCOUNTING-CONTINUITY-04 — destino real de cada cobro no-Crédito (caja de la sesión,
    /// cuenta bancaria de la transferencia o medio tarjeta/cheque), por identidad operativa.
    /// Sales valida el destino comercial; Accounting resuelve su cuenta con la configuración
    /// vigente en cada intento (nunca aquí). Suma de importes == <see cref="CashApplied"/> en el
    /// flujo real; vacío solo para callers/tests que no lo necesitan.
    /// </summary>
    public IReadOnlyList<SalesInvoiceCollectionDestination> CollectionDestinations { get; }

    /// <summary>
    /// CASH-SESSION-PHYSICAL-CASH-SSOT-01 — subconjunto de <see cref="CashApplied"/> cobrado con un
    /// método de pago marcado <c>PaymentMethod.AffectsPhysicalCash = true</c> (hoy solo Efectivo).
    /// Distinto de <see cref="CashApplied"/>: ese campo es "dinero real recibido" para
    /// settlement/CxC/contabilidad (incluye Transferencia/Tarjeta/Cheque); este es "efectivo que
    /// entra/sale del cajón físico" — el único monto que <c>Caja</c> (<see cref="ERP.Domain.Modules.Caja.Entities.CashSession"/>)
    /// debe registrar como <c>CashMovementType.SaleIncome</c>. Nunca inferir por Code/nombre en el
    /// handler: siempre viene de este campo, ya resuelto por Application con el mismo criterio
    /// SSOT que <see cref="CashApplied"/>.
    /// </summary>
    public decimal PhysicalCashApplied { get; }

    public SalesInvoiceAuthorizedEvent(
        Guid invoiceId,
        string invoiceNumber,
        decimal grandTotal,
        Guid userId,
        Guid cashSessionId,
        Guid tenantId,
        Guid companyId,
        DateOnly issueDate,
        decimal subtotal,
        decimal totalVat,
        decimal totalIce,
        decimal totalDiscount,
        decimal totalIrbpnr = 0m,
        decimal? cashApplied = null,
        IReadOnlyList<SalesInvoiceCollectionDestination>? collectionDestinations = null,
        decimal? physicalCashApplied = null
    )
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        GrandTotal = grandTotal;
        UserId = userId;
        CashSessionId = cashSessionId;
        TenantId = tenantId;
        CompanyId = companyId;
        IssueDate = issueDate;
        Subtotal = subtotal;
        TotalVat = totalVat;
        TotalIce = totalIce;
        TotalDiscount = totalDiscount;
        TotalIrbpnr = totalIrbpnr;
        // Default a GrandTotal solo para no romper callers/tests preexistentes que no pasan este
        // parámetro nuevo (ninguno de dominio hoy) — el único caller real de producción
        // (SalesInvoice.Authorize) siempre lo pasa explícitamente con el cashApplied real.
        CashApplied = cashApplied ?? grandTotal;
        CollectionDestinations = collectionDestinations ?? [];
        // Default a CashApplied solo para no romper callers/tests preexistentes que no distinguen
        // forma de pago (mismo criterio que el default de CashApplied arriba) — el único caller
        // real de producción (AuthorizeSalesUseCases vía SalesInvoice.Authorize) siempre lo pasa
        // explícitamente, ya filtrado por PaymentMethod.AffectsPhysicalCash.
        PhysicalCashApplied = physicalCashApplied ?? CashApplied;
    }
}

/// <summary>Tipo de destino financiero de un cobro de venta.</summary>
public enum SalesCollectionDestinationKind
{
    CashRegister = 1,
    BankAccount = 2,
    PaymentMethod = 3,
}

/// <summary>Destino de un cobro de venta por identidad operativa (caja, banco o medio de pago).</summary>
public sealed record SalesInvoiceCollectionDestination(
    SalesCollectionDestinationKind Kind,
    Guid ReferenceId,
    decimal Amount
);
