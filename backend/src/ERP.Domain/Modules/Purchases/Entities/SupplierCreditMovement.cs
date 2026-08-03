using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Movimiento append-only de <see cref="SupplierCredit"/> — única colección que determina
/// <see cref="SupplierCredit.AvailableAmount"/> (diseño §7.5, §13.5). Nunca se edita ni se borra:
/// una reversa siempre es un movimiento nuevo (<see cref="SupplierCreditMovementType.ReversalOfApplication"/>/
/// <see cref="SupplierCreditMovementType.ReversalOfRefund"/>). No tiene ninguna columna de
/// referencia hacia el hecho financiero del reembolso — esa relación 1:1 vive exclusivamente en
/// sentido inverso (<c>SupplierCreditRefundTransaction.SupplierCreditMovementId</c>, §6.4, §7.5,
/// §13.3), evitando la dependencia circular detectada entre ambas entidades.
/// </summary>
public sealed class SupplierCreditMovement : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SupplierCreditId { get; private set; }
    public SupplierCreditMovementType MovementType { get; private set; }
    public decimal Amount { get; private set; }
    public Guid? TargetPurchasePayableId { get; private set; }
    public Guid? ReversalOfMovementId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Obligatorio e inmutable — único por (TenantId, ClientRequestId), §7.5/§16.2.</summary>
    public Guid ClientRequestId { get; private set; }

    /// <summary>Huella determinista del payload de la operación que originó el movimiento — obligatoria e inmutable (§7.5/§16.2).</summary>
    public string RequestPayloadHash { get; private set; } = null!;

    private SupplierCreditMovement() { }

    internal static SupplierCreditMovement Create(
        Guid supplierCreditId,
        Guid tenantId,
        SupplierCreditMovementType movementType,
        decimal amount,
        Guid? targetPurchasePayableId,
        Guid? reversalOfMovementId,
        Guid createdBy,
        Guid clientRequestId,
        string requestPayloadHash
    )
    {
        if (supplierCreditId == Guid.Empty)
            throw new ArgumentException(
                "El crédito de proveedor destino es obligatorio.",
                nameof(supplierCreditId)
            );
        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero.", nameof(amount));
        if (clientRequestId == Guid.Empty)
            throw new ArgumentException(
                "El identificador de idempotencia del movimiento es obligatorio.",
                nameof(clientRequestId)
            );
        if (string.IsNullOrWhiteSpace(requestPayloadHash))
            throw new ArgumentException(
                "La huella del payload del movimiento es obligatoria.",
                nameof(requestPayloadHash)
            );

        var requiresTarget =
            movementType
                is SupplierCreditMovementType.Application
                    or SupplierCreditMovementType.ReversalOfApplication;
        if (requiresTarget && (targetPurchasePayableId is null || targetPurchasePayableId == Guid.Empty))
            throw new ArgumentException(
                "La cuenta por pagar destino es obligatoria para este tipo de movimiento.",
                nameof(targetPurchasePayableId)
            );
        if (!requiresTarget && targetPurchasePayableId is not null)
            throw new ArgumentException(
                "Este tipo de movimiento no admite una cuenta por pagar destino.",
                nameof(targetPurchasePayableId)
            );

        var requiresReversalRef =
            movementType
                is SupplierCreditMovementType.ReversalOfApplication
                    or SupplierCreditMovementType.ReversalOfRefund;
        if (requiresReversalRef && (reversalOfMovementId is null || reversalOfMovementId == Guid.Empty))
            throw new ArgumentException(
                "El movimiento original a revertir es obligatorio para una reversa.",
                nameof(reversalOfMovementId)
            );
        if (!requiresReversalRef && reversalOfMovementId is not null)
            throw new ArgumentException(
                "Este tipo de movimiento no es una reversa y no admite un movimiento original.",
                nameof(reversalOfMovementId)
            );

        return new SupplierCreditMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCreditId = supplierCreditId,
            MovementType = movementType,
            Amount = amount,
            TargetPurchasePayableId = targetPurchasePayableId,
            ReversalOfMovementId = reversalOfMovementId,
            ClientRequestId = clientRequestId,
            RequestPayloadHash = requestPayloadHash,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdBy,
        };
    }
}
