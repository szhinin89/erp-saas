using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Accounting.Queries;

/// <summary>
/// ACCOUNTING-SOURCE-TRACEABILITY-04 — origen documental humano de un asiento, resuelto en
/// tiempo de lectura (nunca persistido en JournalEntry, nunca modifica un asiento histórico).
/// Todos los campos son opcionales por diseño: si el origen no puede resolverse (módulo sin
/// resolver dedicado, FactType no soportado, documento inexistente), el consumidor (DTO/frontend)
/// debe mostrar SourceModule/SourceEventType/SourceEventId "técnicos" en vez de ocultar el dato —
/// ver JournalEntryListItemDto/JournalEntryDetailDto y ChartOfAccounts... (frontend).
/// </summary>
public sealed record JournalEntrySourceInfo(
    string SourceDocumentType,
    string SourceDocumentNumber,
    DateOnly? SourceDocumentDate,
    string? SourcePartyName,
    string? SourceStatus,
    string? SourceRoute
);

/// <summary>Una solicitud de resolución — identifica al JournalEntry para poder devolver el resultado indexado por su Id (no por SourceEventId, que no es necesariamente único entre módulos).</summary>
public sealed record JournalEntrySourceRequest(
    Guid JournalEntryId,
    string SourceModule,
    string SourceEventType,
    Guid SourceEventId
);

/// <summary>
/// Punto de entrada único que usan los query handlers de JournalEntry — despacha por
/// <see cref="JournalEntrySourceRequest.SourceModule"/> hacia el <see cref="IJournalEntrySourceModuleResolver"/>
/// registrado para ese módulo (Strategy, mismo criterio ya exigido por ADR-026 §6.2 para
/// PostingRule: extender agregando una nueva implementación registrada en DI, nunca una rama
/// condicional cerrada aquí). Ningún request se pierde: los no resueltos simplemente no aparecen
/// en el resultado — el llamador decide cómo mostrar la ausencia.
/// </summary>
public interface IJournalEntrySourceResolver
{
    Task<IReadOnlyDictionary<Guid, JournalEntrySourceInfo>> ResolveManyAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<JournalEntrySourceRequest> requests,
        CancellationToken ct = default
    );
}

/// <summary>
/// Estrategia de resolución de origen para un único SourceModule (p.ej. "Sales", "Purchases",
/// "Finance"). ACCOUNTING-CASH-POSTING-06: `companyId` se agrega (antes solo `tenantId`) porque
/// `IPaymentRepository` — a diferencia de `ISalesInvoiceRepository`/`IPurchaseInvoiceRepository`
/// — sí exige CompanyId explícito en su scoping (`ICompanyOperationalEntity`); Sales/Purchases
/// simplemente lo ignoran.
/// </summary>
public interface IJournalEntrySourceModuleResolver
{
    string SourceModule { get; }

    Task<IReadOnlyDictionary<Guid, JournalEntrySourceInfo>> ResolveAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<JournalEntrySourceRequest> requests,
        CancellationToken ct
    );
}

/// <summary>
/// Sin dependencias directas hacia Sales/Purchases (ADR-026 §Contexto: "sin acoplamiento
/// transversal... de Accounting hacia los agregados internos de otros módulos") — este composite
/// solo conoce la abstracción <see cref="IJournalEntrySourceModuleResolver"/>; el acoplamiento
/// real a los repositorios de Sales/Purchases vive exclusivamente en cada resolver de módulo
/// (<see cref="SalesJournalSourceResolver"/>/<see cref="PurchaseJournalSourceResolver"/>), mismo
/// patrón ya usado por Finance (<c>ApplySupplierCreditUseCases</c> inyecta
/// <c>IPurchaseInvoiceRepository</c> directamente para una lectura cross-módulo puntual).
/// </summary>
public sealed class JournalEntrySourceResolver : IJournalEntrySourceResolver
{
    private readonly IReadOnlyDictionary<string, IJournalEntrySourceModuleResolver> _byModule;

    public JournalEntrySourceResolver(IEnumerable<IJournalEntrySourceModuleResolver> resolvers)
    {
        _byModule = resolvers.ToDictionary(r => r.SourceModule, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<Guid, JournalEntrySourceInfo>> ResolveManyAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<JournalEntrySourceRequest> requests,
        CancellationToken ct = default
    )
    {
        var result = new Dictionary<Guid, JournalEntrySourceInfo>();

        foreach (var group in requests.GroupBy(r => r.SourceModule, StringComparer.Ordinal))
        {
            if (!_byModule.TryGetValue(group.Key, out var resolver))
                continue; // Módulo sin resolver dedicado — brecha reportada, no un error.

            var resolved = await resolver.ResolveAsync(tenantId, companyId, group.ToList(), ct);
            foreach (var (journalEntryId, info) in resolved)
                result[journalEntryId] = info;
        }

        return result;
    }
}

/// <summary>
/// ACCOUNTING-SOURCE-TRACEABILITY-04: Sales resuelve FactType "InvoiceIssued" y (desde
/// ACCOUNTING-INVENTORY-COGS-07) "CostOfGoodsSold" — ambos comparten el mismo SourceEventId
/// (SalesInvoice.Id), así que ambos resuelven contra el mismo lookup (el asiento de costo es "el
/// mismo documento humano", solo un hecho contable distinto derivado de él). "SalesReturn"/
/// "CostOfGoodsSoldReversed" quedan sin resolver deliberadamente en esta fase (brecha reportada en
/// el entregable, no un olvido — su SourceEventId es SalesReturn.Id, no SalesInvoice.Id) —
/// extenderlo es aditivo: una rama más aquí + el repositorio de SalesReturn, sin tocar este
/// contrato ni el Posting Engine.
/// </summary>
public sealed class SalesJournalSourceResolver : IJournalEntrySourceModuleResolver
{
    private const string InvoiceIssuedFactType = "InvoiceIssued";
    private const string CostOfGoodsSoldFactType = "CostOfGoodsSold";

    private readonly ISalesInvoiceRepository _salesInvoiceRepository;

    public SalesJournalSourceResolver(ISalesInvoiceRepository salesInvoiceRepository)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
    }

    public string SourceModule => "Sales";

    public async Task<IReadOnlyDictionary<Guid, JournalEntrySourceInfo>> ResolveAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<JournalEntrySourceRequest> requests,
        CancellationToken ct
    )
    {
        var invoiceRequests = requests
            .Where(r => r.SourceEventType is InvoiceIssuedFactType or CostOfGoodsSoldFactType)
            .ToList();
        if (invoiceRequests.Count == 0)
            return new Dictionary<Guid, JournalEntrySourceInfo>();

        var invoiceIds = invoiceRequests.Select(r => r.SourceEventId).Distinct().ToList();
        var summaries = await _salesInvoiceRepository.GetJournalSourceSummariesByIdsAsync(
            tenantId,
            invoiceIds,
            ct
        );

        var result = new Dictionary<Guid, JournalEntrySourceInfo>();
        foreach (var request in invoiceRequests)
        {
            if (!summaries.TryGetValue(request.SourceEventId, out var summary))
                continue; // Factura no encontrada (borrada/otra empresa) — se omite, no se inventa.

            result[request.JournalEntryId] = new JournalEntrySourceInfo(
                request.SourceEventType == CostOfGoodsSoldFactType
                    ? "Costo de venta (factura de venta)"
                    : "Factura de venta",
                summary.InvoiceNumber,
                summary.IssueDate,
                summary.CustomerName,
                summary.Status,
                $"/sales?invoiceId={request.SourceEventId}"
            );
        }
        return result;
    }
}

/// <summary>
/// ACCOUNTING-SOURCE-TRACEABILITY-04: Purchases resuelve FactType "InvoiceReceived" y (desde
/// ACCOUNTING-CREDIT-NOTES-POSTING-08) "PurchaseCreditNoteAuthorized"/"PurchaseCreditNoteCancelled"
/// — dos sub-flujos con distinto agregado origen (PurchaseInvoice vs PurchaseCreditNote), cada uno
/// resuelto contra su propio repositorio, ambos bajo el mismo <see cref="SourceModule"/>. Igual
/// criterio que <see cref="SalesJournalSourceResolver"/> para "InvoiceIssued"/"CostOfGoodsSold".
/// "PurchaseReturn"/"SupplierCredit*" quedan sin resolver deliberadamente (brecha reportada,
/// extensión aditiva futura — su SourceEventId es PurchaseReturn.Id, no PurchaseInvoice.Id ni
/// PurchaseCreditNote.Id, así que necesitan su propio repositorio de lookup).
/// </summary>
public sealed class PurchaseJournalSourceResolver : IJournalEntrySourceModuleResolver
{
    private const string InvoiceReceivedFactType = "InvoiceReceived";
    private const string CreditNoteAuthorizedFactType = "PurchaseCreditNoteAuthorized";
    private const string CreditNoteCancelledFactType = "PurchaseCreditNoteCancelled";

    private readonly IPurchaseInvoiceRepository _purchaseInvoiceRepository;
    private readonly IPurchaseCreditNoteRepository _purchaseCreditNoteRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;

    public PurchaseJournalSourceResolver(
        IPurchaseInvoiceRepository purchaseInvoiceRepository,
        IPurchaseCreditNoteRepository purchaseCreditNoteRepository,
        IBusinessPartnerRepository businessPartnerRepository
    )
    {
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _purchaseCreditNoteRepository = purchaseCreditNoteRepository;
        _businessPartnerRepository = businessPartnerRepository;
    }

    public string SourceModule => "Purchases";

    public async Task<IReadOnlyDictionary<Guid, JournalEntrySourceInfo>> ResolveAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<JournalEntrySourceRequest> requests,
        CancellationToken ct
    )
    {
        var result = new Dictionary<Guid, JournalEntrySourceInfo>();

        await ResolveInvoicesAsync(tenantId, requests, result, ct);
        await ResolveCreditNotesAsync(tenantId, requests, result, ct);

        return result;
    }

    private async Task ResolveInvoicesAsync(
        Guid tenantId,
        IReadOnlyList<JournalEntrySourceRequest> requests,
        Dictionary<Guid, JournalEntrySourceInfo> result,
        CancellationToken ct
    )
    {
        var invoiceRequests = requests
            .Where(r => r.SourceEventType == InvoiceReceivedFactType)
            .ToList();
        if (invoiceRequests.Count == 0)
            return;

        var invoiceIds = invoiceRequests.Select(r => r.SourceEventId).Distinct().ToList();
        var summaries = await _purchaseInvoiceRepository.GetJournalSourceSummariesByIdsAsync(
            tenantId,
            invoiceIds,
            ct
        );

        foreach (var request in invoiceRequests)
        {
            if (!summaries.TryGetValue(request.SourceEventId, out var summary))
                continue; // Compra no encontrada (borrada/otra empresa) — se omite, no se inventa.

            result[request.JournalEntryId] = new JournalEntrySourceInfo(
                "Factura de compra",
                summary.InvoiceNumber,
                summary.IssueDate,
                summary.SupplierName,
                summary.Status,
                $"/purchases?invoiceId={request.SourceEventId}"
            );
        }
    }

    private async Task ResolveCreditNotesAsync(
        Guid tenantId,
        IReadOnlyList<JournalEntrySourceRequest> requests,
        Dictionary<Guid, JournalEntrySourceInfo> result,
        CancellationToken ct
    )
    {
        var creditNoteRequests = requests
            .Where(r => r.SourceEventType is CreditNoteAuthorizedFactType or CreditNoteCancelledFactType)
            .ToList();
        if (creditNoteRequests.Count == 0)
            return;

        var creditNoteIds = creditNoteRequests.Select(r => r.SourceEventId).Distinct().ToList();
        var summaries = await _purchaseCreditNoteRepository.GetJournalSourceSummariesByIdsAsync(
            tenantId,
            creditNoteIds,
            ct
        );

        var supplierIds = summaries.Values.Select(s => s.SupplierId).Distinct().ToList();
        var supplierNames = await _businessPartnerRepository.GetNamesByIdsAsync(supplierIds, ct);

        foreach (var request in creditNoteRequests)
        {
            if (!summaries.TryGetValue(request.SourceEventId, out var summary))
                continue; // NC no encontrada (borrada/otra empresa) — se omite, no se inventa.

            supplierNames.TryGetValue(summary.SupplierId, out var supplierName);

            result[request.JournalEntryId] = new JournalEntrySourceInfo(
                request.SourceEventType == CreditNoteCancelledFactType
                    ? "Nota de crédito de compra (cancelación)"
                    : "Nota de crédito de compra",
                summary.CreditNoteNumber,
                summary.IssueDate,
                supplierName,
                summary.Status,
                $"/purchases/credit-notes/{request.SourceEventId}"
            );
        }
    }
}

/// <summary>
/// ACCOUNTING-CASH-POSTING-06: resuelve FactType "CollectionApplied" (cobro de cliente) y
/// "SupplierPaymentApplied" (pago a proveedor) — <c>Payment</c> (Fase 5.5.5.2) es el aggregate
/// compartido por ambas direcciones (<see cref="PaymentDirection"/>). "CollectionReversed"/
/// "SupplierPaymentReversed" quedan sin resolver deliberadamente (mismo criterio que
/// "SalesReturn"/"PurchaseReturn" en los resolvers hermanos — brecha reportada, extensión aditiva
/// futura). <c>Payment</c> no tiene numeración documental propia (no es un comprobante SRI, ver
/// ADR-026 §7) — <c>SourceDocumentNumber</c> solo resuelve si <c>Payment.Reference</c> fue
/// capturado; sin él, este resolver omite el request entero (nunca inventa un número) y el
/// consumidor cae al fallback técnico (SourceModule/SourceEventType/SourceEventId).
/// </summary>
public sealed class FinanceJournalSourceResolver : IJournalEntrySourceModuleResolver
{
    private const string CollectionAppliedFactType = "CollectionApplied";
    private const string SupplierPaymentAppliedFactType = "SupplierPaymentApplied";

    private readonly IPaymentRepository _paymentRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;

    public FinanceJournalSourceResolver(
        IPaymentRepository paymentRepository,
        IBusinessPartnerRepository businessPartnerRepository
    )
    {
        _paymentRepository = paymentRepository;
        _businessPartnerRepository = businessPartnerRepository;
    }

    public string SourceModule => "Finance";

    public async Task<IReadOnlyDictionary<Guid, JournalEntrySourceInfo>> ResolveAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<JournalEntrySourceRequest> requests,
        CancellationToken ct
    )
    {
        var paymentRequests = requests
            .Where(r =>
                r.SourceEventType is CollectionAppliedFactType or SupplierPaymentAppliedFactType
            )
            .ToList();
        if (paymentRequests.Count == 0)
            return new Dictionary<Guid, JournalEntrySourceInfo>();

        var paymentIds = paymentRequests.Select(r => r.SourceEventId).Distinct().ToList();
        var summaries = await _paymentRepository.GetJournalSourceSummariesByIdsAsync(
            tenantId,
            companyId,
            paymentIds,
            ct
        );

        // GetNamesByIdsAsync (BusinessPartner, IBusinessPartnerRepository) — mismo repo cubre
        // cliente (Collection) y proveedor (Payment): ambos son roles de un mismo BusinessPartner
        // (BP V2), sin necesidad de dos lookups distintos por dirección.
        var partnerIds = summaries.Values.Select(s => s.PartnerId).Distinct().ToList();
        var partnerNames = await _businessPartnerRepository.GetNamesByIdsAsync(partnerIds, ct);

        var result = new Dictionary<Guid, JournalEntrySourceInfo>();
        foreach (var request in paymentRequests)
        {
            if (!summaries.TryGetValue(request.SourceEventId, out var summary))
                continue; // Pago no encontrado (borrado/otra empresa) — se omite, no se inventa.
            if (string.IsNullOrWhiteSpace(summary.Reference))
                continue; // Sin referencia capturada — no hay número que mostrar, nunca se inventa uno.

            partnerNames.TryGetValue(summary.PartnerId, out var partnerName);

            result[request.JournalEntryId] = new JournalEntrySourceInfo(
                request.SourceEventType == CollectionAppliedFactType
                    ? "Cobro de cliente"
                    : "Pago a proveedor",
                summary.Reference,
                summary.PaymentDate,
                partnerName,
                summary.Status,
                null // Sin ruta de navegación segura conocida hoy (no existe una vista de detalle de Payment deep-linkeable, a diferencia de /sales?invoiceId=/purchases?invoiceId=).
            );
        }
        return result;
    }
}
