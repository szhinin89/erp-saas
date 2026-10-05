using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Caja.Enums;

namespace ERP.Domain.Modules.Caja.Entities;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B (diseño 02E-A) — solicitud de efectivo de una
/// <see cref="CashSession"/> que controla OTRO usuario, para un pago a proveedor. La solicitud ES la
/// intención completa del pago (<see cref="PaymentPayload"/>, snapshot versionado): nada financiero
/// ocurre mientras está <see cref="CashFundingRequestStatus.Pending"/> — ni banco, ni caja, ni
/// asiento. El SupplierPayment nace solo al <see cref="Fulfill"/>, ejecutado por el cajero que
/// controla la sesión (la regla de ownership nunca se exceptúa) en la misma transacción que el
/// CashMovement y el posting.
/// <para>
/// Actores: <see cref="RequestedByUserId"/> preparó el pago (originador);
/// <see cref="ResolvedByUserId"/> entregó/rechazó (cajero) o canceló.
/// </para>
/// <para>
/// Orden único de locks: <c>CashSession</c> (FOR UPDATE) → <c>CashFundingRequest</c> (FOR UPDATE) →
/// resto de locks financieros ya existentes.
/// </para>
/// </summary>
public sealed class CashFundingRequest : ICompanyOperationalEntity
{
    public const int PayloadHashMaxLen = 64;
    public const int ResolutionReasonMaxLen = 500;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }

    /// <summary>Sucursal de la caja objetivo (el pago se emite desde la sucursal donde está el efectivo).</summary>
    public Guid BranchId { get; private set; }

    public Guid CashRegisterId { get; private set; }
    public Guid CashSessionId { get; private set; }

    public Guid SupplierId { get; private set; }

    /// <summary>Total del pago preparado (puede incluir líneas bancarias además del efectivo solicitado).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Efectivo solicitado a la caja objetivo — Σ de las líneas de esa caja en el snapshot.</summary>
    public decimal CashAmount { get; private set; }

    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }

    public CashFundingRequestStatus Status { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public string? ResolutionReason { get; private set; }

    /// <summary>Solo en <see cref="CashFundingRequestStatus.Fulfilled"/> — el pago que consumió la solicitud.</summary>
    public Guid? SupplierPaymentId { get; private set; }

    /// <summary>Snapshot canónico (jsonb) de la intención de pago — contrato versionado, nunca el command CLR.</summary>
    public string PaymentPayload { get; private set; } = null!;
    public int PayloadVersion { get; private set; }

    /// <summary>SHA-256 (hex) de la representación canónica de <see cref="PaymentPayload"/>.</summary>
    public string PayloadHash { get; private set; } = null!;

    /// <summary>Idempotencia de la creación — único por (TenantId, ClientRequestId).</summary>
    public Guid ClientRequestId { get; private set; }

    public bool IsPending => Status == CashFundingRequestStatus.Pending;

    private CashFundingRequest() { }

    public static CashFundingRequest Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid cashRegisterId,
        Guid cashSessionId,
        Guid supplierId,
        decimal totalAmount,
        decimal cashAmount,
        Guid requestedByUserId,
        string paymentPayload,
        int payloadVersion,
        string payloadHash,
        Guid clientRequestId
    )
    {
        Require(tenantId, nameof(tenantId), "El tenant es obligatorio.");
        Require(companyId, nameof(companyId), "La empresa es obligatoria.");
        Require(branchId, nameof(branchId), "La sucursal es obligatoria.");
        Require(cashRegisterId, nameof(cashRegisterId), "La caja objetivo es obligatoria.");
        Require(cashSessionId, nameof(cashSessionId), "La sesión de caja objetivo es obligatoria.");
        Require(supplierId, nameof(supplierId), "El proveedor es obligatorio.");
        Require(requestedByUserId, nameof(requestedByUserId), "El solicitante es obligatorio.");
        Require(
            clientRequestId,
            nameof(clientRequestId),
            "El identificador de idempotencia es obligatorio."
        );
        if (cashAmount <= 0)
            throw new ArgumentException(
                "El efectivo solicitado debe ser mayor a cero.",
                nameof(cashAmount)
            );
        if (totalAmount < cashAmount)
            throw new ArgumentException(
                "El total del pago no puede ser menor que el efectivo solicitado.",
                nameof(totalAmount)
            );
        if (string.IsNullOrWhiteSpace(paymentPayload))
            throw new ArgumentException(
                "La intención de pago es obligatoria.",
                nameof(paymentPayload)
            );
        if (payloadVersion < 1)
            throw new ArgumentException(
                "La versión del snapshot debe ser 1 o mayor.",
                nameof(payloadVersion)
            );
        if (string.IsNullOrWhiteSpace(payloadHash) || payloadHash.Length > PayloadHashMaxLen)
            throw new ArgumentException(
                "La huella del snapshot es obligatoria.",
                nameof(payloadHash)
            );

        return new CashFundingRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            CashRegisterId = cashRegisterId,
            CashSessionId = cashSessionId,
            SupplierId = supplierId,
            TotalAmount = totalAmount,
            CashAmount = cashAmount,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = DateTime.UtcNow,
            Status = CashFundingRequestStatus.Pending,
            PaymentPayload = paymentPayload,
            PayloadVersion = payloadVersion,
            PayloadHash = payloadHash,
            ClientRequestId = clientRequestId,
        };
    }

    /// <summary>
    /// El cajero entregó el efectivo y el SupplierPayment que lo consume ya se creó en la misma
    /// transacción. Terminal: la solicitud no puede volver a usarse.
    /// </summary>
    public void Fulfill(Guid fulfilledByUserId, Guid supplierPaymentId)
    {
        EnsurePending();
        Require(
            fulfilledByUserId,
            nameof(fulfilledByUserId),
            "El cajero que entrega es obligatorio."
        );
        Require(
            supplierPaymentId,
            nameof(supplierPaymentId),
            "El pago a proveedor es obligatorio."
        );
        Resolve(CashFundingRequestStatus.Fulfilled, fulfilledByUserId, reason: null);
        SupplierPaymentId = supplierPaymentId;
    }

    /// <summary>El cajero rechaza la solicitud (motivo obligatorio). Terminal, sin efecto financiero.</summary>
    public void Reject(Guid rejectedByUserId, string reason)
    {
        EnsurePending();
        Require(rejectedByUserId, nameof(rejectedByUserId), "Quien rechaza es obligatorio.");
        Resolve(CashFundingRequestStatus.Rejected, rejectedByUserId, RequireReason(reason));
    }

    /// <summary>
    /// Cancelación por el solicitante (o por el sistema, p. ej. al cerrarse la caja). Motivo
    /// obligatorio para dejar trazabilidad. Terminal, sin efecto financiero.
    /// </summary>
    public void Cancel(Guid cancelledByUserId, string reason)
    {
        EnsurePending();
        Require(cancelledByUserId, nameof(cancelledByUserId), "Quien cancela es obligatorio.");
        Resolve(CashFundingRequestStatus.Cancelled, cancelledByUserId, RequireReason(reason));
    }

    private void Resolve(CashFundingRequestStatus status, Guid resolvedBy, string? reason)
    {
        Status = status;
        ResolvedByUserId = resolvedBy;
        ResolvedAtUtc = DateTime.UtcNow;
        ResolutionReason = reason;
    }

    private void EnsurePending()
    {
        if (!IsPending)
            throw new DomainRuleViolationException(
                "La solicitud de efectivo ya fue resuelta y no puede volver a usarse."
            );
    }

    private static string RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo es obligatorio.", nameof(reason));
        var trimmed = reason.Trim();
        if (trimmed.Length > ResolutionReasonMaxLen)
            throw new ArgumentException(
                $"El motivo no puede superar {ResolutionReasonMaxLen} caracteres.",
                nameof(reason)
            );
        return trimmed;
    }

    private static void Require(Guid value, string paramName, string message)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(message, paramName);
    }
}
