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
    /// SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — desglose de <see cref="CashApplied"/> por cuenta
    /// contable real (Caja general / Bancos / etc.), agrupado por la cuenta configurada
    /// (<c>PaymentMethodAccount</c>) del método de pago usado en cada línea de
    /// <c>SalesInvoicePayment</c> no marcada <c>IsCreditAllowed</c>. Resuelto y validado por
    /// Application (<c>AuthorizeSalesInvoiceHandler</c>) ANTES de llamar a <c>Authorize()</c> — el
    /// dominio no conoce <c>PaymentMethodAccount</c> ni accede a repositorios, solo transporta el
    /// resultado ya calculado (mismo criterio que <see cref="CashApplied"/>/<see cref="Subtotal"/>).
    /// Suma de los valores == <see cref="CashApplied"/> siempre que se provea (garantizado por el
    /// handler, que construye ambos del mismo recorrido de <c>Payments</c>). Vacío/null solo por
    /// compatibilidad con callers/tests que no lo necesiten — <see cref="SalesInvoiceAuthorizedPostingTranslator"/>
    /// (Accounting) lo usa para enrutar el Debe de "Sales/InvoiceIssued" a la cuenta real por método
    /// de pago, en vez de la cuenta fija histórica ("Caja general" para cualquier método).
    /// </summary>
    public IReadOnlyDictionary<Guid, decimal> CashByAccount { get; }

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
        IReadOnlyDictionary<Guid, decimal>? cashByAccount = null
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
        CashByAccount = cashByAccount ?? new Dictionary<Guid, decimal>();
    }
}
