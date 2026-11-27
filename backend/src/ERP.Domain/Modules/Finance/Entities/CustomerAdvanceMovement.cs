using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Finance.Enums;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// ADR-052 — movimiento append-only de <see cref="CustomerAdvance"/>: el único libro que determina
/// su saldo disponible. Nunca se edita ni se borra; toda corrección es un movimiento nuevo ligado a
/// su original (<see cref="ReversalOfMovementId"/>). Su único dato posterior a la creación es el
/// vínculo, de una sola vez, con el movimiento de caja del ingreso o de su reversa.
/// </summary>
public sealed class CustomerAdvanceMovement : IMustHaveTenant, ICompanyOperationalEntity
{
    public const int ReasonMaxLen = 500;
    public const int ReferenceMaxLen = 100;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerAdvanceId { get; private set; }
    public CustomerAdvanceMovementType MovementType { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>Intención del cliente: una por operación, compartida por las líneas de una aplicación.</summary>
    public Guid ClientRequestId { get; private set; }

    /// <summary><c>1..N</c> en una aplicación y su reversa; <c>0</c> en las operaciones de un solo efecto.</summary>
    public short LineNumber { get; private set; }

    public string RequestPayloadHash { get; private set; } = null!;

    /// <summary>Sucursal desde la que se hizo la operación: trazabilidad, nunca filtra saldo.</summary>
    public Guid BranchId { get; private set; }

    public DateOnly EffectiveDate { get; private set; }
    public string? Reason { get; private set; }
    public string? Reference { get; private set; }

    /// <summary>Fecha que consta en la evidencia externa (reversa del ingreso o de una devolución).</summary>
    public DateOnly? EvidenceDate { get; private set; }

    public Guid? ReceivableId { get; private set; }

    /// <summary>Sin FK física: las cuotas de una CxC pueden reconstruirse o eliminarse.</summary>
    public Guid? InstallmentId { get; private set; }

    public Guid? ReversalOfMovementId { get; private set; }
    public Guid? CashSessionId { get; private set; }
    public Guid? CashMovementId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CustomerAdvanceMovement() { }

    internal static CustomerAdvanceMovement Create(
        CustomerAdvance advance,
        CustomerAdvanceMovementType movementType,
        decimal amount,
        ClientRequestKey key,
        short lineNumber,
        Guid branchId,
        DateOnly effectiveDate,
        Guid actorId,
        string? reason = null,
        string? reference = null,
        DateOnly? evidenceDate = null,
        Guid? receivableId = null,
        Guid? installmentId = null,
        Guid? reversalOfMovementId = null
    )
    {
        CustomerAdvanceGuards.Amount(amount, nameof(amount));
        if (key.Id == Guid.Empty)
            throw new ArgumentException(
                "El identificador de idempotencia del movimiento es obligatorio.",
                nameof(key)
            );
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (actorId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(actorId));
        if (effectiveDate == default)
            throw new ArgumentException("La fecha efectiva es obligatoria.", nameof(effectiveDate));

        return new CustomerAdvanceMovement
        {
            Id = Guid.NewGuid(),
            TenantId = advance.TenantId,
            CompanyId = advance.CompanyId,
            CustomerAdvanceId = advance.Id,
            MovementType = movementType,
            Amount = amount,
            ClientRequestId = key.Id,
            LineNumber = lineNumber,
            RequestPayloadHash = key.PayloadHash,
            BranchId = branchId,
            EffectiveDate = effectiveDate,
            Reason = CustomerAdvanceGuards.Optional(reason, ReasonMaxLen, nameof(reason)),
            Reference = CustomerAdvanceGuards.Optional(
                reference,
                ReferenceMaxLen,
                nameof(reference)
            ),
            EvidenceDate = evidenceDate,
            ReceivableId = receivableId,
            InstallmentId = installmentId,
            ReversalOfMovementId = reversalOfMovementId,
            CreatedByUserId = actorId,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    internal void LinkCashMovement(Guid cashSessionId, Guid cashMovementId)
    {
        if (
            MovementType
            is not (
                CustomerAdvanceMovementType.Received
                or CustomerAdvanceMovementType.IncomeReversed
            )
        )
            throw new DomainRuleViolationException(
                "Solo el ingreso del anticipo y su reversa se vinculan a caja desde el movimiento."
            );
        if (CashMovementId is not null)
            throw new DomainRuleViolationException(
                "El movimiento ya está vinculado a un movimiento de caja."
            );
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("La sesión de caja es obligatoria.", nameof(cashSessionId));
        if (cashMovementId == Guid.Empty)
            throw new ArgumentException(
                "El movimiento de caja es obligatorio.",
                nameof(cashMovementId)
            );

        CashSessionId = cashSessionId;
        CashMovementId = cashMovementId;
    }
}
