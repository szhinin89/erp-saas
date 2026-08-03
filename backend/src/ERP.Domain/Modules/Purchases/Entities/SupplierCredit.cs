using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Crédito a favor frente a un proveedor, originado exclusivamente por una
/// <see cref="PurchaseReturn"/> (diseño §6.1 — <c>SourceType</c> fue eliminado explícitamente,
/// <see cref="SourcePurchaseReturnId"/> es la única referencia de origen). <see cref="IsOpen"/> no
/// es un campo persistido — se deriva siempre de <see cref="AvailableAmount"/> (§7.4). Una única
/// colección de movimientos (<see cref="SupplierCreditMovement"/>) es la fuente de verdad completa
/// del saldo (§13.2, §13.5) — nunca dos tablas separadas para aplicación/reembolso.
/// </summary>
public sealed class SupplierCredit : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>
    /// Heredado literalmente de <c>PurchaseReturn.BranchId</c> al crearse en
    /// <see cref="CreateFromReturn"/> — nunca una decisión financiera independiente del cliente
    /// (Branch Ownership Rule, diseño §5.2). Inmutable: sin setter público. Ninguna operación
    /// posterior (<see cref="ApplyToPayable"/>, <see cref="ReverseApplication"/>,
    /// <see cref="RegisterRefund"/>, <see cref="ReverseRefund"/>) puede sustituirlo por la sucursal
    /// activa del operador en tiempo de ejecución.
    /// </summary>
    public Guid BranchId { get; private set; }

    public Guid SupplierId { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public Guid SourcePurchaseReturnId { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public decimal AvailableAmount { get; private set; }

    private readonly List<SupplierCreditMovement> _movements = new();
    public IReadOnlyList<SupplierCreditMovement> Movements => _movements.AsReadOnly();

    /// <summary>Derivado siempre de <see cref="AvailableAmount"/> — nunca un campo que pueda contradecirlo (§7.4).</summary>
    public bool IsOpen => AvailableAmount > 0;

    private SupplierCredit() { }

    /// <summary>
    /// Único punto de creación — invocado desde <c>PurchaseReturn.Authorize()</c> cuando el
    /// excedente sobre el saldo pendiente de la CxP es mayor a cero (§6.2, §11.2). Nunca se crea
    /// un <see cref="SupplierCredit"/> con monto cero.
    /// </summary>
    public static SupplierCredit CreateFromReturn(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid supplierId,
        string currencyCode,
        Guid sourcePurchaseReturnId,
        decimal originalAmount,
        Guid createdBy
    )
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (supplierId == Guid.Empty)
            throw new ArgumentException("El proveedor es obligatorio.", nameof(supplierId));
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("La moneda es obligatoria.", nameof(currencyCode));
        if (sourcePurchaseReturnId == Guid.Empty)
            throw new ArgumentException(
                "La devolución de origen es obligatoria.",
                nameof(sourcePurchaseReturnId)
            );
        if (originalAmount <= 0)
            throw new ArgumentException(
                "El monto original del crédito debe ser mayor a cero.",
                nameof(originalAmount)
            );

        var credit = new SupplierCredit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            SourcePurchaseReturnId = sourcePurchaseReturnId,
            OriginalAmount = originalAmount,
            AvailableAmount = originalAmount,
        };
        credit.SetCreated(createdBy);
        return credit;
    }

    /// <summary>Aplica el crédito contra una <c>PurchasePayable</c> destino (§9.3, §13.4). El proveedor/moneda del destino se valida en Application antes de invocar — este método valida el guard defensivo de sobreaplicación (§13.4).</summary>
    public SupplierCreditMovement ApplyToPayable(
        Guid targetPurchasePayableId,
        decimal amount,
        Guid actorId,
        Guid clientRequestId,
        string requestPayloadHash
    )
    {
        if (targetPurchasePayableId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta por pagar destino es obligatoria.",
                nameof(targetPurchasePayableId)
            );
        EnsureSufficientBalance(amount);

        var movement = SupplierCreditMovement.Create(
            Id,
            TenantId,
            SupplierCreditMovementType.Application,
            amount,
            targetPurchasePayableId,
            reversalOfMovementId: null,
            actorId,
            clientRequestId,
            requestPayloadHash
        );
        _movements.Add(movement);
        Recalculate();
        SetUpdated(actorId);

        RaiseDomainEvent(
            new SupplierCreditAppliedEvent(
                Id,
                movement.Id,
                targetPurchasePayableId,
                TenantId,
                CompanyId,
                amount,
                AvailableAmount,
                actorId
            )
        );
        return movement;
    }

    /// <summary>Reversa una aplicación previa — nunca edita el movimiento original, crea uno nuevo (§13.4).</summary>
    public SupplierCreditMovement ReverseApplication(
        Guid originalMovementId,
        Guid actorId,
        Guid clientRequestId,
        string requestPayloadHash
    )
    {
        var original = FindMovementOrThrow(
            originalMovementId,
            SupplierCreditMovementType.Application
        );
        EnsureNotAlreadyReversed(originalMovementId);

        var movement = SupplierCreditMovement.Create(
            Id,
            TenantId,
            SupplierCreditMovementType.ReversalOfApplication,
            original.Amount,
            original.TargetPurchasePayableId,
            originalMovementId,
            actorId,
            clientRequestId,
            requestPayloadHash
        );
        _movements.Add(movement);
        Recalculate();
        SetUpdated(actorId);

        RaiseDomainEvent(
            new SupplierCreditApplicationReversedEvent(
                Id,
                movement.Id,
                originalMovementId,
                original.TargetPurchasePayableId!.Value,
                TenantId,
                CompanyId,
                original.Amount,
                AvailableAmount,
                actorId
            )
        );
        return movement;
    }

    /// <summary>
    /// Crea el movimiento de saldo del reembolso (§9.3, §13.5). La creación atómica del hecho
    /// financiero (<c>SupplierCreditRefundTransaction</c>, destino, cuenta contable, caja) es
    /// responsabilidad de Application bajo los bloqueos de fila de §6.4quater — fuera del alcance
    /// del dominio puro de esta fase.
    /// </summary>
    public SupplierCreditMovement RegisterRefund(
        decimal amount,
        Guid actorId,
        Guid clientRequestId,
        string requestPayloadHash
    )
    {
        EnsureSufficientBalance(amount);

        var movement = SupplierCreditMovement.Create(
            Id,
            TenantId,
            SupplierCreditMovementType.Refund,
            amount,
            targetPurchasePayableId: null,
            reversalOfMovementId: null,
            actorId,
            clientRequestId,
            requestPayloadHash
        );
        _movements.Add(movement);
        Recalculate();
        SetUpdated(actorId);

        RaiseDomainEvent(
            new SupplierCreditRefundedEvent(
                Id,
                movement.Id,
                TenantId,
                CompanyId,
                amount,
                AvailableAmount,
                actorId
            )
        );
        return movement;
    }

    /// <summary>Reversa un reembolso previo — nunca edita el movimiento original (§9.3, §6.4quinquies).</summary>
    public SupplierCreditMovement ReverseRefund(
        Guid originalMovementId,
        Guid actorId,
        Guid clientRequestId,
        string requestPayloadHash
    )
    {
        var original = FindMovementOrThrow(originalMovementId, SupplierCreditMovementType.Refund);
        EnsureNotAlreadyReversed(originalMovementId);

        var movement = SupplierCreditMovement.Create(
            Id,
            TenantId,
            SupplierCreditMovementType.ReversalOfRefund,
            original.Amount,
            targetPurchasePayableId: null,
            originalMovementId,
            actorId,
            clientRequestId,
            requestPayloadHash
        );
        _movements.Add(movement);
        Recalculate();
        SetUpdated(actorId);

        RaiseDomainEvent(
            new SupplierCreditRefundReversedEvent(
                Id,
                movement.Id,
                originalMovementId,
                TenantId,
                CompanyId,
                original.Amount,
                AvailableAmount,
                actorId
            )
        );
        return movement;
    }

    /// <summary>
    /// Efecto atómico de <c>PurchaseReturn.Cancel()</c> sobre el crédito íntegro que originó
    /// (§9.1, §9.3, §5.1 casos 6/7). Solo válido si <see cref="AvailableAmount"/> ==
    /// <see cref="OriginalAmount"/> — precondición ya revalidada por Application antes de invocar
    /// (el dominio la revalida además como defensa en profundidad). Genera un movimiento de
    /// sistema <see cref="SupplierCreditMovementType.SourceReturnCancelled"/>, nunca seleccionable
    /// por el usuario, dejando <see cref="AvailableAmount"/> exactamente en 0.
    /// </summary>
    public SupplierCreditMovement RegisterSourceReturnCancellation(
        Guid actorId,
        Guid clientRequestId,
        string requestPayloadHash
    )
    {
        if (AvailableAmount != OriginalAmount)
            throw new InvalidOperationException(
                "No se puede cancelar el origen de este crédito porque ya tiene aplicaciones o reembolsos activos."
            );

        var movement = SupplierCreditMovement.Create(
            Id,
            TenantId,
            SupplierCreditMovementType.SourceReturnCancelled,
            AvailableAmount,
            targetPurchasePayableId: null,
            reversalOfMovementId: null,
            actorId,
            clientRequestId,
            requestPayloadHash
        );
        _movements.Add(movement);
        Recalculate();
        SetUpdated(actorId);
        return movement;
    }

    // ── Guards / cálculo ─────────────────────────────────────────────────
    private void EnsureSufficientBalance(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero.", nameof(amount));
        if (amount > AvailableAmount)
            throw new InvalidOperationException(
                "El monto solicitado excede el saldo disponible del crédito de proveedor."
            );
    }

    private SupplierCreditMovement FindMovementOrThrow(
        Guid movementId,
        SupplierCreditMovementType expectedType
    )
    {
        var movement = _movements.FirstOrDefault(m => m.Id == movementId);
        if (movement is null)
            throw new InvalidOperationException(
                "El movimiento indicado no pertenece a este crédito de proveedor."
            );
        if (movement.MovementType != expectedType)
            throw new InvalidOperationException(
                $"El movimiento indicado no es de tipo {expectedType} y no puede revertirse por esta vía."
            );
        return movement;
    }

    private void EnsureNotAlreadyReversed(Guid movementId)
    {
        if (_movements.Any(m => m.ReversalOfMovementId == movementId))
            throw new InvalidOperationException(
                "Este movimiento ya fue revertido anteriormente — no se puede revertir dos veces."
            );
    }

    /// <summary>Fórmula completa de §13.5 — recalcula siempre desde la colección completa de movimientos, nunca incrementalmente.</summary>
    private void Recalculate()
    {
        var available =
            OriginalAmount
            - _movements
                .Where(m => m.MovementType == SupplierCreditMovementType.Application)
                .Sum(m => m.Amount)
            + _movements
                .Where(m => m.MovementType == SupplierCreditMovementType.ReversalOfApplication)
                .Sum(m => m.Amount)
            - _movements
                .Where(m => m.MovementType == SupplierCreditMovementType.Refund)
                .Sum(m => m.Amount)
            + _movements
                .Where(m => m.MovementType == SupplierCreditMovementType.ReversalOfRefund)
                .Sum(m => m.Amount)
            - _movements
                .Where(m => m.MovementType == SupplierCreditMovementType.SourceReturnCancelled)
                .Sum(m => m.Amount);

        if (available < 0 || available > OriginalAmount)
            throw new InvalidOperationException(
                "Invariante violada: el saldo disponible del crédito de proveedor quedó fuera del rango [0, monto original]."
            );

        AvailableAmount = available;
    }
}
