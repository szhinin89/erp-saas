using ERP.Application.Common.Services;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;

namespace ERP.Application.Modules.Finance.UseCases;

// ── DTOs (máx. 2 por entidad: List + Detail) ────────────────────────────

/// <summary>
/// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — fila del listado de saldos a favor de proveedores.
/// <see cref="SourceDocumentId"/> es el Id de la devolución o del pago de origen (según
/// <see cref="SourceType"/>) — la UI arma la navegación, nunca el backend.
/// </summary>
public sealed record SupplierCreditListItemDto(
    Guid Id,
    Guid SupplierId,
    string? SupplierName,
    string SourceType,
    Guid SourceDocumentId,
    string? SourceDocumentNumber,
    DateOnly? SourceDate,
    string CurrencyCode,
    decimal OriginalAmount,
    decimal AvailableAmount,
    bool IsOpen
);

/// <summary>
/// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — movimiento del historial, con los datos que el dominio
/// realmente guarda según su tipo (nunca inventados):
/// <list type="bullet">
/// <item>Aplicación / su reversa: <see cref="AccountsPayableId"/> + número y origen de la CxP.
/// La cuota no se guarda por movimiento (reparto FIFO en la CxP) y la reversa de aplicación no
/// guarda motivo.</item>
/// <item>Reembolso / su reversa: <see cref="RefundTransactionId"/>, fecha efectiva, destino
/// (Cash/Bank + nombre congelado), código y nombre (catálogo oficial PaymentMethod, 02D-E) de la
/// forma de pago, referencia (solo en el ingreso) y
/// motivo (solo en la reversa).</item>
/// <item>Reversas: <see cref="ReversalOfMovementId"/> (movimiento original) y, en el original,
/// <see cref="ReversedByMovementId"/>.</item>
/// </list>
/// Los campos de enriquecimiento (nombres, número de CxP, datos del reembolso) son <c>null</c> en
/// las respuestas internas de los comandos; la API siempre responde con la lectura enriquecida.
/// </summary>
public sealed record SupplierCreditMovementDto(
    Guid Id,
    string MovementType,
    decimal Amount,
    DateTime CreatedAtUtc,
    Guid CreatedByUserId,
    string? CreatedByName,
    Guid? ReversalOfMovementId,
    Guid? ReversedByMovementId,
    Guid? AccountsPayableId,
    string? PayableDocumentNumber,
    string? PayableOriginType,
    Guid? RefundTransactionId,
    DateOnly? EffectiveDate,
    string? DestinationType,
    string? DestinationName,
    string? PaymentMethodCode,
    string? PaymentMethodName,
    string? ReferenceNumber,
    string? Reason
);

/// <summary>
/// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — detalle de un saldo a favor (también respuesta de
/// aplicar/reversar). Origen: <see cref="SourceType"/> + <see cref="SourceDocumentId"/> + exactamente
/// uno de <see cref="SourcePurchaseReturnId"/>/<see cref="SourceSupplierPaymentId"/> + número y fecha
/// reales. Movimientos en orden cronológico.
/// </summary>
public sealed record SupplierCreditDto(
    Guid Id,
    Guid SupplierId,
    string? SupplierName,
    Guid BranchId,
    string CurrencyCode,
    string SourceType,
    Guid SourceDocumentId,
    Guid? SourcePurchaseReturnId,
    Guid? SourceSupplierPaymentId,
    string? SourceDocumentNumber,
    DateOnly? SourceDate,
    decimal OriginalAmount,
    decimal AvailableAmount,
    bool IsOpen,
    IReadOnlyList<SupplierCreditMovementDto> Movements
);

// ── Proyección única (list + detail + respuesta de comandos) ────────────

/// <summary>Origen ya resuelto a fecha de negocio de la empresa.</summary>
internal sealed record SupplierCreditSourceRef(Guid DocumentId, string? Number, DateOnly? Date);

/// <summary>Datos de lectura ya resueltos en lote para enriquecer el detalle (vacío = respuesta básica).</summary>
internal sealed record SupplierCreditReadContext(
    string? SupplierName,
    SupplierCreditSourceRef? Source,
    IReadOnlyDictionary<Guid, (string DocumentNumber, AccountsPayableOriginType OriginType)> Payables,
    IReadOnlyDictionary<Guid, SupplierCreditRefundTransaction> RefundTransactionsByMovement,
    IReadOnlyDictionary<Guid, string> UserNames,
    IReadOnlyDictionary<string, string> PaymentMethodNames
)
{
    public static readonly SupplierCreditReadContext Empty = new(
        null,
        null,
        new Dictionary<Guid, (string, AccountsPayableOriginType)>(),
        new Dictionary<Guid, SupplierCreditRefundTransaction>(),
        new Dictionary<Guid, string>(),
        new Dictionary<string, string>()
    );
}

/// <summary>
/// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — ÚNICA resolución/formato de origen y ÚNICO mapeo a DTO
/// de <see cref="SupplierCredit"/>, compartidos por el listado, el detalle y los comandos.
/// </summary>
internal static class SupplierCreditReadModel
{
    /// <summary>
    /// Origen de un lote de créditos: 2 consultas fijas (devoluciones + pagos) + a lo sumo 1 para la
    /// zona horaria de la empresa (solo si hay devoluciones — su fecha es el instante de
    /// autorización convertido a fecha de la empresa, ADR-034; el pago ya tiene fecha de negocio).
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, SupplierCreditSourceRef>> ResolveSourcesAsync(
        ISupplierCreditRepository credits,
        ICompanyRepository companies,
        Guid tenantId,
        Guid companyId,
        IReadOnlyCollection<Guid> creditIds,
        CancellationToken ct
    )
    {
        var documents = await credits.GetSourceDocumentsAsync(tenantId, creditIds, ct);
        TimeZoneInfo? tz = null;
        if (documents.Values.Any(d => d.AuthorizedAtUtc is not null))
            tz = CompanyTimeZone.Resolve((await companies.GetByIdAsync(companyId, ct))?.Timezone);

        return documents.ToDictionary(
            x => x.Key,
            x => new SupplierCreditSourceRef(
                x.Value.DocumentId,
                x.Value.Number,
                x.Value.BusinessDate
                    ?? (x.Value.AuthorizedAtUtc is { } at ? CompanyTimeZone.LocalDate(at, tz!) : null)
            )
        );
    }

    public static SupplierCreditListItemDto ToListItem(
        SupplierCredit c,
        string? supplierName,
        SupplierCreditSourceRef? source
    ) =>
        new(
            c.Id,
            c.SupplierId,
            supplierName,
            c.SourceType.ToString(),
            SourceDocumentId(c),
            source?.Number,
            source?.Date,
            c.CurrencyCode,
            c.OriginalAmount,
            c.AvailableAmount,
            c.IsOpen
        );

    public static SupplierCreditDto ToDetail(SupplierCredit c, SupplierCreditReadContext ctx)
    {
        var reversedBy = c
            .Movements.Where(m => m.ReversalOfMovementId is not null)
            .ToDictionary(m => m.ReversalOfMovementId!.Value, m => m.Id);

        return new(
            c.Id,
            c.SupplierId,
            ctx.SupplierName,
            c.BranchId,
            c.CurrencyCode,
            c.SourceType.ToString(),
            SourceDocumentId(c),
            c.SourcePurchaseReturnId,
            c.SourceSupplierPaymentId,
            ctx.Source?.Number,
            ctx.Source?.Date,
            c.OriginalAmount,
            c.AvailableAmount,
            c.IsOpen,
            c.Movements.OrderBy(m => m.CreatedAtUtc)
                .ThenBy(m => m.Id)
                .Select(m => ToMovement(m, reversedBy.GetValueOrDefault(m.Id), ctx))
                .ToList()
        );
    }

    private static SupplierCreditMovementDto ToMovement(
        SupplierCreditMovement m,
        Guid reversedBy,
        SupplierCreditReadContext ctx
    )
    {
        var payableId = m.TargetPurchasePayableId;
        var hasPayable = payableId is { } pid && ctx.Payables.ContainsKey(pid);
        var payable = hasPayable ? ctx.Payables[payableId!.Value] : default;
        var tx = ctx.RefundTransactionsByMovement.GetValueOrDefault(m.Id);

        return new(
            m.Id,
            m.MovementType.ToString(),
            m.Amount,
            m.CreatedAtUtc,
            m.CreatedByUserId,
            ctx.UserNames.GetValueOrDefault(m.CreatedByUserId),
            m.ReversalOfMovementId,
            reversedBy == Guid.Empty ? null : reversedBy,
            payableId,
            hasPayable ? payable.DocumentNumber : null,
            hasPayable ? payable.OriginType.ToString() : null,
            tx?.Id,
            tx?.EffectiveDate,
            tx is null ? null : DestinationType(tx.DestinationTypeSnapshot),
            tx?.DestinationNameSnapshot,
            tx?.PaymentMethodCode,
            tx is null ? null : ctx.PaymentMethodNames.GetValueOrDefault(tx.PaymentMethodCode),
            tx?.ExternalReference,
            tx?.TransactionTypeCode == RefundTransactionTypeCode.RefundReversed ? tx.Reason : null
        );
    }

    private static Guid SourceDocumentId(SupplierCredit c) =>
        c.SourceType == SupplierCreditSourceType.SupplierPayment
            ? c.SourceSupplierPaymentId!.Value
            : c.SourcePurchaseReturnId!.Value;

    private static string DestinationType(string snapshot) =>
        snapshot switch
        {
            "CashRegister" => "Cash",
            "BankAccount" => "Bank",
            _ => snapshot,
        };
}
