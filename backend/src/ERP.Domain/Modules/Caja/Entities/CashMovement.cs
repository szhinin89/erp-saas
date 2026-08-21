using ERP.Domain.Common;
using ERP.Domain.Modules.Caja.Enums;

namespace ERP.Domain.Modules.Caja.Entities;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX02 (P1-2) — decisión documentada: <c>CashMovement</c> implementa solo
/// <see cref="IMustHaveTenant"/> (filtro EF tenant-only), no <c>ICompanyOperationalEntity</c>,
/// porque nunca se consulta directamente — siempre se accede como hijo del agregado
/// <see cref="CashSession"/> (<c>ErpDbContext.CashMovements</c> no se usa fuera del mapeo EF; toda
/// lectura pasa por <c>ICashSessionRepository.GetByIdAsync</c>, ya scopeado por Company vía
/// <c>ForOperationalScope</c> y, tras ERP-CORE-CLOSEOUT-05-FIX01, también por BranchId en
/// <c>GetCashSessionByIdHandler</c>/<c>CloseCashSessionHandler</c>/<c>RecordCashMovementHandler</c>).
/// Promover a <c>ICompanyOperationalEntity</c> exigiría una migración (agregar <c>CompanyId</c> a
/// la tabla) sin cerrar ningún hueco real hoy — se deja fuera de este fix. Si en el futuro aparece
/// una consulta directa de <c>CashMovement</c>, debe pasar primero por company+branch scope antes
/// de agregarse; <see cref="ERP.Infrastructure.Tests.Persistence.CashMovementDirectQueryAuditTests"/>
/// (Infrastructure.Tests) hace cumplir que eso no ocurra silenciosamente.
/// </summary>
public sealed class CashMovement : IMustHaveTenant
{
    public const int DescriptionMaxLen = 300;
    public const int ReferenceNumberMaxLen = 50;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CashSessionId { get; private set; }

    public CashMovementType MovementType { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    public CashReferenceType ReferenceType { get; private set; } = CashReferenceType.None;
    public Guid? ReferenceId { get; private set; }
    public string? ReferenceNumber { get; private set; }

    private CashMovement() { }

    internal static CashMovement Create(
        Guid cashSessionId,
        Guid tenantId,
        Guid companyId,
        CashMovementType movementType,
        decimal amount,
        string description,
        Guid createdBy,
        CashReferenceType referenceType = CashReferenceType.None,
        Guid? referenceId = null,
        string? referenceNumber = null
    )
    {
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("La sesión de caja es obligatoria.", nameof(cashSessionId));
        if (amount < 0)
            throw new ArgumentException("El monto no puede ser negativo.", nameof(amount));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La descripción es obligatoria.", nameof(description));

        return new CashMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CashSessionId = cashSessionId,
            MovementType = movementType,
            Amount = amount,
            Description = description.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ReferenceNumber = referenceNumber?.Trim(),
        };
    }
}
