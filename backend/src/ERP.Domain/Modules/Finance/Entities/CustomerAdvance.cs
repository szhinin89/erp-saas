using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>Línea de una aplicación: la CxC se entrega cargada para verificar su alcance.</summary>
public sealed record CustomerAdvanceApplicationLine(
    SalesReceivable Receivable,
    Guid? InstallmentId,
    decimal Amount
);

/// <summary>
/// ADR-052 §6 — datos obligatorios de toda anulación, reversa y devolución. El usuario se recibe
/// aparte y la fecha efectiva la resuelve Application.
/// </summary>
public sealed record CustomerAdvanceCompensation(
    string Reason,
    string Reference,
    DateOnly EffectiveDate,
    DateOnly? EvidenceDate = null
);

/// <summary>
/// ADR-052 — anticipo recibido de un cliente sin factura, propiedad de Finance. El saldo pertenece
/// al cliente y a la empresa; <see cref="BranchId"/> es solo el origen. <see cref="AvailableAmount"/>
/// se recalcula siempre desde el único libro append-only (<see cref="Movements"/>).
/// </summary>
/// <remarks>
/// El dominio no reduce ni restaura cuentas por cobrar, no mueve caja y no resuelve fechas ni
/// idempotencia: eso orquesta Application bajo el lock de la fila.
/// </remarks>
public sealed class CustomerAdvance
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int CurrencyCodeLen = 3;
    public const int ExternalReferenceMaxLen = CustomerAdvanceMovement.ReferenceMaxLen;
    public const int NoteMaxLen = CustomerAdvanceMovement.ReasonMaxLen;

    public Guid CompanyId { get; private set; }

    /// <summary>Sucursal de origen: inmutable, trazabilidad y reportes.</summary>
    public Guid BranchId { get; private set; }

    public Guid CustomerId { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public decimal OriginalAmount { get; private set; }
    public decimal AvailableAmount { get; private set; }
    public DateOnly AdvanceDate { get; private set; }
    public Guid PaymentMethodId { get; private set; }

    /// <summary>Exactamente uno de este campo o <see cref="CashRegisterId"/>.</summary>
    public Guid? CompanyBankAccountId { get; private set; }

    public Guid? CashRegisterId { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? Note { get; private set; }
    public CustomerAdvanceStatus Status { get; private set; }

    private readonly List<CustomerAdvanceMovement> _movements = new();
    public IReadOnlyList<CustomerAdvanceMovement> Movements => _movements.AsReadOnly();

    /// <summary>Sin aplicaciones ni devoluciones vigentes.</summary>
    public bool IsIntact => AvailableAmount == OriginalAmount;

    private CustomerAdvance() { }

    public static CustomerAdvance Register(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        string currencyCode,
        decimal amount,
        DateOnly advanceDate,
        Guid paymentMethodId,
        Guid? companyBankAccountId,
        Guid? cashRegisterId,
        Guid actorId,
        ClientRequestKey key,
        string? externalReference = null,
        string? note = null
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("El cliente es obligatorio.", nameof(customerId));
        var currency = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (currency.Length != CurrencyCodeLen)
            throw new ArgumentException("La moneda es obligatoria.", nameof(currencyCode));
        CustomerAdvanceGuards.Amount(amount, nameof(amount));
        if (paymentMethodId == Guid.Empty)
            throw new ArgumentException(
                "El medio de pago es obligatorio.",
                nameof(paymentMethodId)
            );
        CustomerAdvanceGuards.Destination(companyBankAccountId, cashRegisterId);

        var advance = new CustomerAdvance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            CustomerId = customerId,
            CurrencyCode = currency,
            OriginalAmount = amount,
            AvailableAmount = amount,
            AdvanceDate = advanceDate,
            PaymentMethodId = paymentMethodId,
            CompanyBankAccountId = companyBankAccountId,
            CashRegisterId = cashRegisterId,
            ExternalReference = CustomerAdvanceGuards.Optional(
                externalReference,
                ExternalReferenceMaxLen,
                nameof(externalReference)
            ),
            Note = CustomerAdvanceGuards.Optional(note, NoteMaxLen, nameof(note)),
            Status = CustomerAdvanceStatus.Confirmed,
        };
        advance.SetCreated(actorId);

        var received = CustomerAdvanceMovement.Create(
            advance,
            CustomerAdvanceMovementType.Received,
            amount,
            key,
            lineNumber: 0,
            branchId,
            advanceDate,
            actorId,
            reason: advance.Note,
            reference: advance.ExternalReference
        );
        advance._movements.Add(received);
        advance.RaiseDomainEvent(
            new CustomerAdvanceReceivedEvent(
                advance,
                received,
                balanceBefore: 0m,
                statusBefore: null
            )
        );
        return advance;
    }

    /// <summary>
    /// Aplica el anticipo a una o varias CxC del mismo cliente y empresa: un movimiento por línea,
    /// numerado <c>1..N</c>. Una línea inválida rechaza toda la operación sin efectos. Reducir cada
    /// CxC es responsabilidad de Application con <c>SalesReceivable.RegisterCollection</c>.
    /// </summary>
    public IReadOnlyList<CustomerAdvanceMovement> Apply(
        IReadOnlyList<CustomerAdvanceApplicationLine> lines,
        DateOnly effectiveDate,
        Guid branchId,
        Guid actorId,
        ClientRequestKey key
    )
    {
        EnsureCanOperate(key);
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0 || lines.Count > short.MaxValue)
            throw new ArgumentException(
                "La aplicación requiere al menos una línea.",
                nameof(lines)
            );

        var total = 0m;
        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line.Receivable);
            CustomerAdvanceGuards.Amount(line.Amount, nameof(lines));
            if (line.Receivable.TenantId != TenantId || line.Receivable.CompanyId != CompanyId)
                throw new DomainRuleViolationException(
                    "La cuenta por cobrar no pertenece a la empresa del anticipo."
                );
            if (line.Receivable.CustomerId != CustomerId)
                throw new DomainRuleViolationException(
                    "La cuenta por cobrar no pertenece al cliente del anticipo."
                );
            total += line.Amount;
        }
        EnsureSufficientBalance(total);

        // Los movimientos se construyen todos antes de tocar el libro: o entran todos o ninguno.
        var created = lines
            .Select(
                (line, index) =>
                    CustomerAdvanceMovement.Create(
                        this,
                        CustomerAdvanceMovementType.Application,
                        line.Amount,
                        key,
                        (short)(index + 1),
                        branchId,
                        effectiveDate,
                        actorId,
                        receivableId: line.Receivable.Id,
                        installmentId: line.InstallmentId
                    )
            )
            .ToList();

        foreach (var movement in created)
        {
            var (balanceBefore, statusBefore) = (AvailableAmount, Status);
            Append(movement, actorId);
            RaiseDomainEvent(
                new CustomerAdvanceAppliedEvent(this, movement, balanceBefore, statusBefore)
            );
        }
        return created;
    }

    /// <summary>
    /// Revierte la operación de aplicación completa identificada por su <c>ClientRequestId</c>
    /// (TA-04): un movimiento por línea, cada uno ligado a su original.
    /// </summary>
    public IReadOnlyList<CustomerAdvanceMovement> ReverseApplication(
        Guid applicationClientRequestId,
        CustomerAdvanceCompensation compensation,
        Guid branchId,
        Guid actorId,
        ClientRequestKey key
    )
    {
        EnsureCanOperate(key);
        Require(compensation);

        var originals = _movements
            .Where(m =>
                m.MovementType == CustomerAdvanceMovementType.Application
                && m.ClientRequestId == applicationClientRequestId
            )
            .OrderBy(m => m.LineNumber)
            .ToList();
        if (originals.Count == 0)
            throw new DomainRuleViolationException(
                "La aplicación indicada no pertenece a este anticipo."
            );
        foreach (var original in originals)
            EnsureNotAlreadyReversed(original.Id);

        var created = originals
            .Select(
                (original, index) =>
                    CustomerAdvanceMovement.Create(
                        this,
                        CustomerAdvanceMovementType.ReversalOfApplication,
                        original.Amount,
                        key,
                        (short)(index + 1),
                        branchId,
                        compensation.EffectiveDate,
                        actorId,
                        compensation.Reason,
                        compensation.Reference,
                        compensation.EvidenceDate,
                        original.ReceivableId,
                        original.InstallmentId,
                        original.Id
                    )
            )
            .ToList();

        foreach (var movement in created)
        {
            var (balanceBefore, statusBefore) = (AvailableAmount, Status);
            Append(movement, actorId);
            RaiseDomainEvent(
                new CustomerAdvanceApplicationReversedEvent(
                    this,
                    movement,
                    balanceBefore,
                    statusBefore
                )
            );
        }
        return created;
    }

    /// <summary>Devolución efectiva de dinero, hasta el saldo disponible.</summary>
    public CustomerAdvanceMovement RegisterRefund(
        decimal amount,
        CustomerAdvanceCompensation compensation,
        Guid branchId,
        Guid actorId,
        ClientRequestKey key
    )
    {
        EnsureCanOperate(key);
        Require(compensation);
        CustomerAdvanceGuards.Amount(amount, nameof(amount));
        EnsureSufficientBalance(amount);

        var movement = CustomerAdvanceMovement.Create(
            this,
            CustomerAdvanceMovementType.Refund,
            amount,
            key,
            lineNumber: 0,
            branchId,
            compensation.EffectiveDate,
            actorId,
            compensation.Reason,
            compensation.Reference,
            compensation.EvidenceDate
        );
        var (balanceBefore, statusBefore) = (AvailableAmount, Status);
        Append(movement, actorId);
        RaiseDomainEvent(
            new CustomerAdvanceRefundedEvent(this, movement, balanceBefore, statusBefore)
        );
        return movement;
    }

    /// <summary>Reversa de una devolución que no llegó a ejecutarse: exige la fecha de la recuperación.</summary>
    public CustomerAdvanceMovement ReverseRefund(
        Guid refundMovementId,
        CustomerAdvanceCompensation compensation,
        Guid branchId,
        Guid actorId,
        ClientRequestKey key
    )
    {
        EnsureCanOperate(key);
        RequireWithEvidenceDate(compensation);

        var original =
            _movements.FirstOrDefault(m =>
                m.Id == refundMovementId && m.MovementType == CustomerAdvanceMovementType.Refund
            )
            ?? throw new DomainRuleViolationException(
                "La devolución indicada no pertenece a este anticipo."
            );
        EnsureNotAlreadyReversed(original.Id);

        var movement = CustomerAdvanceMovement.Create(
            this,
            CustomerAdvanceMovementType.ReversalOfRefund,
            original.Amount,
            key,
            lineNumber: 0,
            branchId,
            compensation.EffectiveDate,
            actorId,
            compensation.Reason,
            compensation.Reference,
            compensation.EvidenceDate,
            reversalOfMovementId: original.Id
        );
        var (balanceBefore, statusBefore) = (AvailableAmount, Status);
        Append(movement, actorId);
        RaiseDomainEvent(
            new CustomerAdvanceRefundReversedEvent(this, movement, balanceBefore, statusBefore)
        );
        return movement;
    }

    /// <summary>
    /// Anulación administrativa: solo un anticipo íntegro, sin caja y con la declaración expresa de
    /// que el dinero no fue recibido. Que no exista asiento publicado lo comprueba Application.
    /// </summary>
    public CustomerAdvanceMovement CancelAdministratively(
        bool moneyNotReceivedDeclared,
        CustomerAdvanceCompensation compensation,
        Guid branchId,
        Guid actorId,
        ClientRequestKey key
    )
    {
        EnsureCanOperate(key);
        Require(compensation);
        if (!moneyNotReceivedDeclared)
            throw new DomainRuleViolationException(
                "La anulación administrativa exige declarar que el dinero no fue recibido."
            );
        if (CashRegisterId is not null)
            throw new DomainRuleViolationException(
                "Un anticipo en efectivo no admite anulación administrativa: corresponde reversar el ingreso o devolverlo."
            );

        var movement = Terminate(
            CustomerAdvanceMovementType.SourceCancelled,
            compensation,
            branchId,
            actorId,
            key,
            out var balanceBefore,
            out var statusBefore
        );
        RaiseDomainEvent(
            new CustomerAdvanceCancelledEvent(this, movement, balanceBefore, statusBefore)
        );
        return movement;
    }

    /// <summary>Reversa de un ingreso que realmente se deshizo: exige la fecha de la evidencia.</summary>
    public CustomerAdvanceMovement ReverseIncome(
        CustomerAdvanceCompensation compensation,
        Guid branchId,
        Guid actorId,
        ClientRequestKey key
    )
    {
        EnsureCanOperate(key);
        RequireWithEvidenceDate(compensation);

        var movement = Terminate(
            CustomerAdvanceMovementType.IncomeReversed,
            compensation,
            branchId,
            actorId,
            key,
            out var balanceBefore,
            out var statusBefore
        );
        RaiseDomainEvent(
            new CustomerAdvanceIncomeReversedEvent(this, movement, balanceBefore, statusBefore)
        );
        return movement;
    }

    /// <summary>
    /// Vincula, una sola vez, el movimiento de caja del ingreso o de su reversa (la caja necesita el
    /// Id del movimiento como referencia). Admite la reversa de un anticipo ya cancelado.
    /// </summary>
    public void LinkCashMovement(Guid movementId, Guid cashSessionId, Guid cashMovementId)
    {
        if (CashRegisterId is null)
            throw new DomainRuleViolationException(
                "Solo un anticipo con caja destino puede vincularse a un movimiento de caja."
            );
        var movement =
            _movements.FirstOrDefault(m => m.Id == movementId)
            ?? throw new DomainRuleViolationException(
                "El movimiento indicado no pertenece a este anticipo."
            );
        movement.LinkCashMovement(cashSessionId, cashMovementId);
    }

    // ── Guards / libro ───────────────────────────────────────────────────
    private CustomerAdvanceMovement Terminate(
        CustomerAdvanceMovementType type,
        CustomerAdvanceCompensation compensation,
        Guid branchId,
        Guid actorId,
        ClientRequestKey key,
        out decimal balanceBefore,
        out CustomerAdvanceStatus statusBefore
    )
    {
        if (!IsIntact)
            throw new DomainRuleViolationException(
                "El anticipo tiene aplicaciones o devoluciones vigentes y no puede anularse ni reversarse."
            );
        var received = _movements.Single(m =>
            m.MovementType == CustomerAdvanceMovementType.Received
        );
        var movement = CustomerAdvanceMovement.Create(
            this,
            type,
            AvailableAmount,
            key,
            lineNumber: 0,
            branchId,
            compensation.EffectiveDate,
            actorId,
            compensation.Reason,
            compensation.Reference,
            compensation.EvidenceDate,
            reversalOfMovementId: received.Id
        );
        balanceBefore = AvailableAmount;
        statusBefore = Status;
        Status = CustomerAdvanceStatus.Cancelled;
        Append(movement, actorId);
        return movement;
    }

    private void EnsureCanOperate(ClientRequestKey key)
    {
        // Fail-closed: sin su ingreso, el libro no está cargado y el saldo no puede recalcularse.
        if (_movements.Count(m => m.MovementType == CustomerAdvanceMovementType.Received) != 1)
            throw new InvalidOperationException(
                "Invariante violada: el libro de movimientos del anticipo no está cargado."
            );
        if (Status != CustomerAdvanceStatus.Confirmed)
            throw new DomainRuleViolationException(
                "El anticipo está cancelado y no admite más operaciones."
            );
        if (_movements.Any(m => m.ClientRequestId == key.Id))
            throw new DomainRuleViolationException(
                "El identificador de idempotencia ya fue usado en este anticipo."
            );
    }

    private static void Require(CustomerAdvanceCompensation compensation)
    {
        ArgumentNullException.ThrowIfNull(compensation);
        if (string.IsNullOrWhiteSpace(compensation.Reason))
            throw new ArgumentException("El motivo es obligatorio.", nameof(compensation));
        if (string.IsNullOrWhiteSpace(compensation.Reference))
            throw new ArgumentException("La referencia es obligatoria.", nameof(compensation));
    }

    private static void RequireWithEvidenceDate(CustomerAdvanceCompensation compensation)
    {
        Require(compensation);
        if (compensation.EvidenceDate is null)
            throw new ArgumentException(
                "La fecha de la evidencia es obligatoria.",
                nameof(compensation)
            );
    }

    private void EnsureSufficientBalance(decimal amount)
    {
        if (amount > AvailableAmount)
            throw new DomainRuleViolationException(
                "El monto solicitado excede el saldo disponible del anticipo."
            );
    }

    private void EnsureNotAlreadyReversed(Guid movementId)
    {
        if (_movements.Any(m => m.ReversalOfMovementId == movementId))
            throw new DomainRuleViolationException(
                "Este movimiento ya fue revertido anteriormente — no se puede revertir dos veces."
            );
    }

    /// <summary>Agrega el movimiento y recalcula el saldo desde el libro completo, nunca incrementalmente.</summary>
    private void Append(CustomerAdvanceMovement movement, Guid actorId)
    {
        _movements.Add(movement);
        var available = OriginalAmount + _movements.Sum(SignedEffect);
        if (available < 0 || available > OriginalAmount)
            throw new InvalidOperationException(
                "Invariante violada: el saldo disponible del anticipo quedó fuera del rango [0, monto original]."
            );
        AvailableAmount = available;
        SetUpdated(actorId);
    }

    private static decimal SignedEffect(CustomerAdvanceMovement m) =>
        m.MovementType switch
        {
            CustomerAdvanceMovementType.Received => 0m,
            CustomerAdvanceMovementType.ReversalOfApplication
            or CustomerAdvanceMovementType.ReversalOfRefund => m.Amount,
            CustomerAdvanceMovementType.Application
            or CustomerAdvanceMovementType.Refund
            or CustomerAdvanceMovementType.SourceCancelled
            or CustomerAdvanceMovementType.IncomeReversed => -m.Amount,
            _ => throw new InvalidOperationException(
                $"Invariante violada: tipo de movimiento de anticipo no soportado: {m.MovementType}."
            ),
        };
}

/// <summary>Validaciones comunes de las entidades de anticipos de clientes.</summary>
internal static class CustomerAdvanceGuards
{
    /// <summary>Dinero con la escala fija del sistema (<c>numeric(18,2)</c>): nunca se redondea en silencio.</summary>
    public static void Amount(decimal amount, string paramName)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero.", paramName);
        if (decimal.Round(amount, FiscalPrecision.TaxAmount) != amount)
            throw new ArgumentException(
                "El monto excede la precisión monetaria permitida.",
                paramName
            );
    }

    public static void Destination(Guid? companyBankAccountId, Guid? cashRegisterId)
    {
        var hasBank = companyBankAccountId is { } bank && bank != Guid.Empty;
        var hasCash = cashRegisterId is { } cash && cash != Guid.Empty;
        if (
            hasBank == hasCash
            || companyBankAccountId == Guid.Empty
            || cashRegisterId == Guid.Empty
        )
            throw new ArgumentException(
                "Debe indicarse exactamente un destino: cuenta bancaria o caja.",
                nameof(companyBankAccountId)
            );
    }

    public static string? Optional(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException(
                $"El texto no puede superar {maxLength} caracteres.",
                paramName
            );
        return trimmed;
    }
}
