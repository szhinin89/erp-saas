using ERP.Domain.Common;
using ERP.Domain.Modules.Payables.Enums;

namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// PAYABLES-GENERIC-FOUNDATION-09 — CxP genérica: la deuda viva con el proveedor, desacoplada de
/// su documento de origen (Compra o Gasto son documentos de origen; esta entidad es la obligación
/// resultante). <see cref="OriginType"/>/<see cref="OriginId"/> apuntan al documento que la generó
/// — únicos por (TenantId, CompanyId, OriginType, OriginId), ver
/// <c>AccountsPayableConfiguration</c> para el índice único real y
/// <c>AccountsPayableService.CreateFromOriginAsync</c> para la idempotencia a nivel de aplicación.
/// <see cref="Domain.Modules.Purchases.Entities.PurchasePayable"/> sigue existiendo y no se toca —
/// esta es una fundación nueva y paralela, no un reemplazo (ver ticket: "No romper PurchasePayable
/// existente").
/// </summary>
/// <remarks>
/// Pagos/abonos no existen todavía (fase separada) — <see cref="PaidAmount"/>/<see cref="OutstandingAmount"/>
/// se derivan de las cuotas (nunca columnas propias, mismo criterio que <c>ExpenseDocument.Subtotal</c>/
/// <c>GrandTotal</c>), y con cero cuotas pagadas todo nace y permanece en <see cref="AccountsPayableStatus.Pending"/>.
/// </remarks>
public sealed class AccountsPayable : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int DocumentTypeMaxLen = 5;
    public const int DocumentNumberMaxLen = 30;

    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid SupplierId { get; private set; }
    public AccountsPayableOriginType OriginType { get; private set; }
    public Guid OriginId { get; private set; }
    public string DocumentType { get; private set; } = null!;
    public string DocumentNumber { get; private set; } = null!;
    public DateOnly IssueDate { get; private set; }
    public DateOnly AccountingDate { get; private set; }
    public AccountsPayableStatus Status { get; private set; } = AccountsPayableStatus.Pending;

    private readonly List<AccountsPayableInstallment> _installments = new();
    public IReadOnlyList<AccountsPayableInstallment> Installments => _installments.AsReadOnly();

    public decimal TotalAmount => _installments.Sum(i => i.Amount);
    public decimal PaidAmount => _installments.Sum(i => i.PaidAmount);
    public decimal OutstandingAmount => _installments.Sum(i => i.OutstandingAmount);

    private AccountsPayable() { }

    public static AccountsPayable CreateFromOrigin(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid supplierId,
        AccountsPayableOriginType originType,
        Guid originId,
        string documentType,
        string documentNumber,
        DateOnly issueDate,
        DateOnly accountingDate,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (supplierId == Guid.Empty)
            throw new ArgumentException("El proveedor es obligatorio.", nameof(supplierId));
        if (originId == Guid.Empty)
            throw new ArgumentException("El documento de origen es obligatorio.", nameof(originId));
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("El tipo de documento es obligatorio.", nameof(documentType));
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new ArgumentException("El número de documento es obligatorio.", nameof(documentNumber));

        var payable = new AccountsPayable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            OriginType = originType,
            OriginId = originId,
            DocumentType = documentType.Trim(),
            DocumentNumber = documentNumber.Trim(),
            IssueDate = issueDate,
            AccountingDate = accountingDate,
            Status = AccountsPayableStatus.Pending,
        };
        payable.SetCreated(createdBy);
        return payable;
    }

    /// <summary>
    /// Agrega una cuota. Por ahora todos los orígenes generan una sola cuota por el total (ver
    /// <c>AccountsPayableService.CreateFromOriginAsync</c>) — este método es genérico y admite N
    /// cuotas a futuro (p. ej. cronogramas de Compras/Gastos), sin cambios aquí.
    /// </summary>
    public AccountsPayableInstallment AddInstallment(int installmentNumber, DateOnly dueDate, decimal amount)
    {
        if (_installments.Any(i => i.InstallmentNumber == installmentNumber))
            throw new InvalidOperationException(
                $"Ya existe una cuota con el número {installmentNumber}."
            );

        var installment = AccountsPayableInstallment.Create(Id, TenantId, installmentNumber, dueDate, amount);
        _installments.Add(installment);
        return installment;
    }
}
