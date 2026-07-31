using ERP.Domain.Common;
using ERP.Domain.Modules.Caja.Enums;

namespace ERP.Domain.Modules.Caja.Entities;

public sealed class CashSession : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int NotesMaxLen = 500;
    public const int CloseNotesMaxLen = 500;
    public const int CashRegisterCodeSnapshotMaxLen = 30;
    public const int CashRegisterNameSnapshotMaxLen = 100;
    public const int EmissionPointCodeSnapshotMaxLen = 3;

    public Guid CompanyId { get; private set; }

    /// <summary>
    /// Sucursal de origen de la operación (Branch Ownership) — asignada exclusivamente al abrir
    /// la caja desde <see cref="ICurrentBranch"/> del handler, nunca desde el cliente.
    /// Inmutable tras la apertura.
    /// </summary>
    public Guid BranchId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>
    /// Caja física/lógica sobre la que se abrió el turno (ADR — Rediseño del módulo de Caja).
    /// Inmutable tras la apertura: identifica "quién cobra, en qué caja", distinto del punto de
    /// emisión SRI (<see cref="EmissionPointId"/>), que identifica "cómo se numera".
    /// </summary>
    public Guid CashRegisterId { get; private set; }

    /// <summary>
    /// Punto de emisión SRI efectivo del turno — snapshot tomado del <see cref="CashRegister"/>
    /// al momento de abrir la sesión. Nunca se resuelve en vivo contra la caja: si la caja
    /// reasigna su punto de emisión después, esta sesión ya cerrada conserva el que tuvo.
    /// </summary>
    public Guid EmissionPointId { get; private set; }

    /// <summary>Snapshot histórico del código de la caja al momento de abrir — nunca se resincroniza.</summary>
    public string CashRegisterCodeSnapshot { get; private set; } = null!;

    /// <summary>Snapshot histórico del nombre de la caja al momento de abrir — nunca se resincroniza.</summary>
    public string CashRegisterNameSnapshot { get; private set; } = null!;

    /// <summary>Snapshot histórico del código del punto de emisión al momento de abrir — nunca se resincroniza.</summary>
    public string EmissionPointCodeSnapshot { get; private set; } = null!;

    public DateTime OpenedAt { get; private set; }
    public decimal OpeningAmount { get; private set; }

    public CashSessionStatus Status { get; private set; } = CashSessionStatus.Open;

    public string? Notes { get; private set; }

    public DateTime? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public string? CloseNotes { get; private set; }

    public decimal? ExpectedAmount { get; private set; }
    public decimal? CountedAmount { get; private set; }
    public decimal? Difference { get; private set; }

    private readonly List<CashMovement> _movements = new();
    public IReadOnlyList<CashMovement> Movements => _movements.AsReadOnly();

    private readonly List<CashClosingCount> _closingCounts = new();
    public IReadOnlyList<CashClosingCount> ClosingCounts => _closingCounts.AsReadOnly();

    // Calculated (NOT persisted)
    // TotalIncome/TotalExpense excluyen el movimiento Opening deliberadamente: OpeningAmount ya
    // se suma por separado en CurrentBalance. Contar Opening también como "ingreso" duplicaba el
    // monto de apertura en CurrentBalance/ExpectedAmount (bug detectado con datos reales durante
    // la validación end-to-end de Caja→Ventas).
    public decimal TotalIncome =>
        _movements.Where(m => IsIncome(m.MovementType)).Sum(m => m.Amount);
    public decimal TotalExpense =>
        _movements.Where(m => IsExpense(m.MovementType)).Sum(m => m.Amount);
    public decimal CurrentBalance => OpeningAmount + TotalIncome - TotalExpense;

    private CashSession() { }

    public static CashSession Open(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid userId,
        Guid cashRegisterId,
        string cashRegisterCodeSnapshot,
        string cashRegisterNameSnapshot,
        Guid emissionPointId,
        string emissionPointCodeSnapshot,
        decimal openingAmount,
        Guid createdBy,
        string? notes = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (userId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(userId));
        if (cashRegisterId == Guid.Empty)
            throw new ArgumentException("La caja es obligatoria.", nameof(cashRegisterId));
        if (string.IsNullOrWhiteSpace(cashRegisterCodeSnapshot))
            throw new ArgumentException(
                "El código de la caja es obligatorio.",
                nameof(cashRegisterCodeSnapshot)
            );
        if (string.IsNullOrWhiteSpace(cashRegisterNameSnapshot))
            throw new ArgumentException(
                "El nombre de la caja es obligatorio.",
                nameof(cashRegisterNameSnapshot)
            );
        if (emissionPointId == Guid.Empty)
            throw new ArgumentException(
                "El punto de emisión es obligatorio.",
                nameof(emissionPointId)
            );
        if (string.IsNullOrWhiteSpace(emissionPointCodeSnapshot))
            throw new ArgumentException(
                "El código del punto de emisión es obligatorio.",
                nameof(emissionPointCodeSnapshot)
            );
        if (openingAmount < 0)
            throw new ArgumentException(
                "El monto de apertura no puede ser negativo.",
                nameof(openingAmount)
            );

        var session = new CashSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            UserId = userId,
            CashRegisterId = cashRegisterId,
            CashRegisterCodeSnapshot = cashRegisterCodeSnapshot.Trim(),
            CashRegisterNameSnapshot = cashRegisterNameSnapshot.Trim(),
            EmissionPointId = emissionPointId,
            EmissionPointCodeSnapshot = emissionPointCodeSnapshot.Trim(),
            OpenedAt = DateTime.UtcNow,
            OpeningAmount = openingAmount,
            Status = CashSessionStatus.Open,
            Notes = notes?.Trim(),
        };
        session.SetCreated(createdBy);

        session._movements.Add(
            CashMovement.Create(
                session.Id,
                tenantId,
                companyId,
                CashMovementType.Opening,
                openingAmount,
                "Apertura de caja",
                createdBy
            )
        );

        return session;
    }

    public CashMovement RecordMovement(
        CashMovementType movementType,
        decimal amount,
        string description,
        Guid createdBy,
        CashReferenceType referenceType = CashReferenceType.None,
        Guid? referenceId = null,
        string? referenceNumber = null
    )
    {
        EnsureOpen();
        if (movementType == CashMovementType.Opening)
            throw new InvalidOperationException(
                "No se puede registrar un movimiento de apertura manualmente."
            );

        var movement = CashMovement.Create(
            Id,
            TenantId,
            CompanyId,
            movementType,
            amount,
            description,
            createdBy,
            referenceType,
            referenceId,
            referenceNumber
        );

        _movements.Add(movement);
        SetUpdated(createdBy);
        return movement;
    }

    public void Close(
        Guid closedBy,
        List<CashClosingCount> closingCounts,
        string? closeNotes = null
    )
    {
        EnsureOpen();

        ClosedAt = DateTime.UtcNow;
        ClosedBy = closedBy;
        CloseNotes = closeNotes?.Trim();
        Status = CashSessionStatus.Closed;

        _closingCounts.Clear();
        decimal totalCounted = 0m;

        foreach (var count in closingCounts)
        {
            _closingCounts.Add(count);
            totalCounted += count.Total;
        }

        var expected = CurrentBalance;
        ExpectedAmount = expected;
        CountedAmount = totalCounted;
        Difference = totalCounted - expected;

        SetUpdated(closedBy);
    }

    private void EnsureOpen()
    {
        if (Status != CashSessionStatus.Open)
            throw new InvalidOperationException("La caja no está abierta.");
    }

    private static bool IsIncome(CashMovementType type) =>
        type is CashMovementType.SaleIncome or CashMovementType.ManualIncome;

    private static bool IsExpense(CashMovementType type) =>
        type is CashMovementType.ManualExpense or CashMovementType.Withdrawal or CashMovementType.SaleRefund;
}
