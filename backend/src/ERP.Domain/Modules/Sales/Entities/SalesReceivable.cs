using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Sales.Enums;
using static ERP.Domain.Common.FiscalPrecision;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesReceivable
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int StatusMaxLen = 20;
    public const int DocumentNumberMaxLen = 50;

    public Guid CompanyId { get; private set; }
    public SalesReceivableOrigin Origin { get; private set; } = SalesReceivableOrigin.Invoice;

    /// <summary>Solo <see cref="SalesReceivableOrigin.Invoice"/>; null para un saldo inicial.</summary>
    public Guid? InvoiceId { get; private set; }
    public Guid CustomerId { get; private set; }

    // IL-5A — datos propios de un saldo inicial (Origin = InitialBalance); null para Invoice, cuyo
    // número/fecha/sucursal viven en la factura.
    public string? DocumentNumber { get; private set; }

    /// <summary>
    /// Clave de unicidad de <see cref="DocumentNumber"/> (<see cref="NormalizeDocumentNumber"/>),
    /// calculada por la fábrica y protegida por índice único parcial en BD.
    /// </summary>
    public string? DocumentNumberNormalized { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? ImportBatchId { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public string Status { get; private set; } = "pending";

    public decimal BalanceDue => OriginalAmount - PaidAmount;

    private readonly List<SalesReceivableInstallment> _installments = new();
    public IReadOnlyList<SalesReceivableInstallment> Installments => _installments.AsReadOnly();

    private SalesReceivable() { }

    public static SalesReceivable Create(
        Guid tenantId,
        Guid companyId,
        Guid invoiceId,
        Guid customerId,
        decimal originalAmount,
        Guid createdBy
    )
    {
        if (invoiceId == Guid.Empty)
            throw new ArgumentException("La factura es obligatoria.", nameof(invoiceId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("El cliente es obligatorio.", nameof(customerId));
        if (originalAmount <= 0)
            throw new ArgumentException(
                "El monto original debe ser mayor a cero.",
                nameof(originalAmount)
            );

        var r = new SalesReceivable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            InvoiceId = invoiceId,
            CustomerId = customerId,
            OriginalAmount = originalAmount,
            PaidAmount = 0,
            Status = "pending",
        };
        r.SetCreated(createdBy);
        return r;
    }

    /// <summary>
    /// IL-5A — saldo pendiente de un documento de un cliente al corte (Carga Inicial de CxC). Nunca
    /// reconstruye la venta ni los cobros históricos: el monto original ES el saldo pendiente y se
    /// genera una única cuota por ese saldo con su vencimiento.
    /// </summary>
    public static SalesReceivable CreateInitialBalance(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        string documentNumber,
        DateOnly issueDate,
        DateOnly dueDate,
        decimal balance,
        Guid importBatchId,
        Guid createdBy
    )
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("El cliente es obligatorio.", nameof(customerId));
        if (importBatchId == Guid.Empty)
            throw new ArgumentException("El lote de importación es obligatorio.", nameof(importBatchId));
        var number = documentNumber?.Trim() ?? string.Empty;
        if (number.Length == 0)
            throw new ArgumentException("El número de documento es obligatorio.", nameof(documentNumber));
        if (number.Length > DocumentNumberMaxLen)
            throw new ArgumentException(
                $"El número de documento no puede superar {DocumentNumberMaxLen} caracteres.",
                nameof(documentNumber)
            );
        var normalized = NormalizeDocumentNumber(number);
        if (normalized.Length == 0)
            throw new ArgumentException(
                "El número de documento debe contener letras o dígitos.",
                nameof(documentNumber)
            );
        if (dueDate < issueDate)
            throw new ArgumentException(
                "La fecha de vencimiento no puede ser anterior a la fecha de emisión.",
                nameof(dueDate)
            );
        if (balance <= 0)
            throw new ArgumentException("El saldo pendiente debe ser mayor a cero.", nameof(balance));
        if (decimal.Round(balance, TaxAmount) != balance)
            throw new ArgumentException(
                $"El saldo pendiente admite como máximo {TaxAmount} decimales.",
                nameof(balance)
            );

        var r = new SalesReceivable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Origin = SalesReceivableOrigin.InitialBalance,
            InvoiceId = null,
            CustomerId = customerId,
            DocumentNumber = number,
            DocumentNumberNormalized = normalized,
            IssueDate = issueDate,
            BranchId = branchId,
            ImportBatchId = importBatchId,
            OriginalAmount = balance,
            PaidAmount = 0,
            Status = "pending",
        };
        r._installments.Add(SalesReceivableInstallment.Create(r.Id, tenantId, 1, dueDate, balance));
        r.SetCreated(createdBy);
        return r;
    }

    /// <summary>
    /// IL-5A — clave de comparación de números de documento para detectar duplicados
    /// (<see cref="DocumentNumberKey"/>, compartida con la CxP inicial).
    /// </summary>
    public static string NormalizeDocumentNumber(string? documentNumber) =>
        DocumentNumberKey.Normalize(documentNumber);

    public void GenerateInstallments(DateOnly baseDate, int creditTermDays, int installmentCount)
    {
        _installments.Clear();

        if (installmentCount <= 0)
            installmentCount = 1;
        var intervalDays =
            installmentCount > 1 ? creditTermDays / installmentCount : creditTermDays;

        var baseAmount = Math.Round(
            OriginalAmount / installmentCount,
            TaxAmount,
            MidpointRounding.AwayFromZero
        );
        var accumulated = 0m;

        for (var i = 1; i <= installmentCount; i++)
        {
            var isLast = i == installmentCount;
            var amount = isLast ? OriginalAmount - accumulated : baseAmount;
            var dueDate = baseDate.AddDays(intervalDays * i);

            _installments.Add(SalesReceivableInstallment.Create(Id, TenantId, i, dueDate, amount));

            accumulated += amount;
        }
    }

    /// <summary>
    /// ADR-033, Fase 4 — crea las cuotas de la CxC copiando exactamente número, fecha y monto del
    /// cronograma ya congelado del documento de origen (SalesInvoice.PaymentSchedules). A
    /// diferencia de <see cref="GenerateInstallments"/> (que recalcula desde PaymentTerm/
    /// CreditTermDays), este método NO recalcula nada — es la fuente preferida cuando el
    /// documento ya tiene cronograma persistido. GenerateInstallments queda como fallback
    /// defensivo únicamente para documentos legacy sin cronograma.
    /// </summary>
    public void CreateInstallmentsFromSchedule(IReadOnlyList<SalesPaymentSchedule> schedule)
    {
        _installments.Clear();
        foreach (var s in schedule.OrderBy(s => s.InstallmentNumber))
        {
            _installments.Add(
                SalesReceivableInstallment.Create(
                    Id,
                    TenantId,
                    s.InstallmentNumber,
                    s.DueDate,
                    s.Amount
                )
            );
        }
    }

    public void Cancel(Guid updatedBy)
    {
        // IL-5 decisión 10: la cancelación genérica acompaña a la anulación de una factura; un saldo
        // inicial no tiene factura y su corrección/reverso será un flujo específico.
        if (Origin == SalesReceivableOrigin.InitialBalance)
            throw new DomainRuleViolationException(
                "Un saldo inicial de cuentas por cobrar no se cancela con la anulación de facturas."
            );
        if (PaidAmount > 0)
            throw new DomainRuleViolationException(
                "No se puede cancelar una cuenta por cobrar con pagos registrados."
            );

        Status = "cancelled";
        _installments.Clear();
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Registra un cobro aplicado contra esta CxC (Fase 5.5.5.2) — invocado por el caso de uso de
    /// Application (fase futura) que coordina un <c>Payment</c> (dirección Collection) ya
    /// <c>Applied</c> contra una o más <c>SalesReceivable</c>. No levanta ningún evento propio:
    /// el hecho de negocio "cobro aplicado" ya lo publica <c>Payment.Apply()</c> — este método
    /// solo mantiene el saldo de la CxC consistente con lo que ese pago registró.
    /// </summary>
    public void RegisterCollection(decimal amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentException(
                "El monto del cobro debe ser mayor a cero.",
                nameof(amount)
            );
        if (Status == "cancelled")
            throw new DomainRuleViolationException(
                "No se puede registrar un cobro sobre una cuenta por cobrar cancelada."
            );
        if (amount > BalanceDue)
            throw new DomainRuleViolationException(
                "El monto del cobro excede el saldo pendiente de la cuenta por cobrar."
            );

        PaidAmount += amount;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Reversa un cobro previamente registrado (Fase 5.5.5.2) — invocado cuando el caso de uso de
    /// Application procesa un <c>Payment.Reverse()</c> ya ejecutado. Sin evento propio, mismo
    /// criterio que <see cref="RegisterCollection"/>.
    /// </summary>
    public void ReverseCollection(decimal amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentException(
                "El monto a reversar debe ser mayor a cero.",
                nameof(amount)
            );
        if (amount > PaidAmount)
            throw new DomainRuleViolationException(
                "El monto a reversar excede el monto cobrado registrado en la cuenta por cobrar."
            );

        PaidAmount -= amount;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Aplica el crédito de una devolución de venta autorizada (P0-01) reduciendo el monto
    /// original de la CxC — nunca el monto ya cobrado (<see cref="PaidAmount"/>). Invocado por el
    /// caso de uso de Application (fase futura) que coordina un <c>SalesReturn.Authorize()</c> ya
    /// ejecutado. Sin evento propio, mismo criterio que <see cref="RegisterCollection"/>: el hecho
    /// de negocio "devolución autorizada" ya lo publica <c>SalesReturn.Authorize()</c> — este
    /// método solo mantiene el saldo de la CxC consistente con lo que esa devolución acreditó.
    /// </summary>
    public void ApplyReturnCredit(decimal amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentException(
                "El monto del crédito debe ser mayor a cero.",
                nameof(amount)
            );
        if (Status == "cancelled")
            throw new DomainRuleViolationException(
                "No se puede aplicar un crédito de devolución sobre una cuenta por cobrar cancelada."
            );
        if (amount > BalanceDue)
            throw new DomainRuleViolationException(
                "El monto del crédito excede el saldo pendiente de la cuenta por cobrar."
            );

        OriginalAmount -= amount;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Reconstruye las cuotas conservando siempre el número de cuotas y las fechas de vencimiento
    /// ya generadas — solo reprorratea <see cref="BalanceDue"/> entre ellas según el peso relativo
    /// de cada cuota existente. Mismo patrón que <c>PurchasePayable.RebuildInstallments()</c>,
    /// adaptado porque <c>SalesReceivable</c> no recibe un cronograma externo: la distribución
    /// previa de <see cref="Installments"/> es la fuente del peso relativo. Invocado tras
    /// <see cref="ApplyReturnCredit"/> por el caso de uso de Application (fase futura).
    /// </summary>
    public void RebuildInstallments()
    {
        var previous = _installments.OrderBy(i => i.InstallmentNumber).ToList();
        _installments.Clear();

        var netAmount = BalanceDue;
        var previousTotal = previous.Sum(i => i.Amount);
        if (netAmount <= 0 || previous.Count == 0 || previousTotal <= 0)
            return;

        decimal allocated = 0;
        for (var i = 0; i < previous.Count; i++)
        {
            var installment = previous[i];
            var amount =
                i == previous.Count - 1
                    ? netAmount - allocated
                    : Math.Round(netAmount * installment.Amount / previousTotal, TaxAmount);
            _installments.Add(
                SalesReceivableInstallment.Create(
                    Id,
                    TenantId,
                    installment.InstallmentNumber,
                    installment.DueDate,
                    amount
                )
            );
            allocated += amount;
        }
    }
}
