using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Devolución de compra (P0-02) — agregado independiente de <c>PurchaseInvoice</c>, referenciada
/// solo por Id (mismo patrón que <c>SalesReturn.SalesInvoiceId</c>). Mismo principio que
/// <c>P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md</c> Fase 1, con dos diferencias de diseño
/// deliberadas: (1) <see cref="BranchId"/> es obligatorio desde <see cref="CreateDraft"/> (Branch
/// Ownership Rule, diseño §5.2 — <c>SalesReturn</c> no lo tiene porque nunca se corrigió, no porque
/// no aplique); (2) el snapshot financiero de cada línea (<see cref="PurchaseReturnDetail"/>) no se
/// congela hasta <see cref="Authorize"/>, no al crear el borrador, porque depende de una fracción
/// sobre la línea original que solo es fiable en el momento de autorizar (§7.2, §11.1).
/// </summary>
public sealed class PurchaseReturn : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int ReasonMaxLen = 500;
    public const int ReturnNumberMaxLen = 8;
    public const int CancellationReasonMaxLen = 500;

    public Guid CompanyId { get; private set; }

    /// <summary>
    /// Sucursal de origen de la operación (Branch Ownership Rule, diseño §5.2) — asignada
    /// exclusivamente al crear el borrador desde <c>ICurrentBranch</c> del handler, nunca desde el
    /// cliente. Inmutable tras la creación: sin setter público, sin <c>ChangeBranch</c>/<c>SetBranch</c>.
    /// </summary>
    public Guid BranchId { get; private set; }

    public Guid PurchaseInvoiceId { get; private set; }

    /// <summary>Snapshot de <c>PurchaseInvoice.SupplierId</c> al crear el draft.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Asignado una única vez en <see cref="Authorize"/> — null mientras <c>Status == Draft</c> (§7.1bis).</summary>
    public string? ReturnNumber { get; private set; }

    public string Reason { get; private set; } = null!;

    public PurchaseReturnStatus Status { get; private set; } = PurchaseReturnStatus.Draft;

    /// <summary>Siempre tiene un valor explícito — nunca null (§9.2).</summary>
    public PurchaseReturnFiscalStatus FiscalStatus { get; private set; } =
        PurchaseReturnFiscalStatus.NotApplicable;

    /// <summary>Referencia (no copia) al <c>PurchaseReceptionDocument</c> tipo NC vinculado — 1:1, asignado una sola vez.</summary>
    public Guid? SupplierCreditNoteDocumentId { get; private set; }

    // ── Totales snapshot (congelados al autorizar, §11.1) ───────────────
    public decimal? AuthorizedSubtotal { get; private set; }
    public decimal? AuthorizedVatTotal { get; private set; }
    public decimal? AuthorizedIceTotal { get; private set; }
    public decimal? AuthorizedDiscountTotal { get; private set; }

    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-1) — suma de <c>PurchaseReturnDetail.IrbpnrAmount</c>
    /// de todas las líneas. Deliberadamente NO se agrega a <see cref="PurchaseReturnAuthorizedEvent"/>/
    /// <see cref="PurchaseReturnCancelledEvent"/> en esta subfase (esos eventos alimentan el
    /// traductor contable — fuera de alcance de 5D, corresponde a la Subfase 5E). Sí se incluye en
    /// <see cref="AuthorizedGrandTotal"/>: es el total financiero propio de la devolución (aplicado
    /// a CxP + crédito de proveedor), no un asiento contable.
    /// </summary>
    public decimal? AuthorizedIrbpnrTotal { get; private set; }
    public decimal? AuthorizedGrandTotal { get; private set; }

    /// <summary>Costo histórico de inventario revertido — solo tras autorizar (§19.1bis, distinto del valor reconocido).</summary>
    public decimal? HistoricalCostTotal { get; private set; }

    /// <summary>= HistoricalCostTotal − AuthorizedSubtotal. Puede ser positivo, negativo o cero (§19.1bis).</summary>
    public decimal? CostVarianceTotal { get; private set; }

    /// <summary>= MIN(GrandTotal, BalanceDue antes) — snapshot necesario para revertir exactamente el mismo valor en Cancel() (§11.2, §7.1).</summary>
    public decimal? AppliedToPayableAmount { get; private set; }

    /// <summary>= GrandTotal − AppliedToPayableAmount (§11.2).</summary>
    public decimal? SupplierCreditAmount { get; private set; }

    public DateTime? AuthorizedAtUtc { get; private set; }
    public Guid? AuthorizedByUserId { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }

    // ── Idempotencia (§7.1, §16.2 — Enmienda P0-02_PURCHASE_RETURN_DOMAIN_AMENDMENT_01) ─
    /// <summary>Obligatorio desde la creación — único por (TenantId, CreateClientRequestId).</summary>
    public Guid CreateClientRequestId { get; private set; }
    public string CreateRequestPayloadHash { get; private set; } = null!;

    /// <summary>Null hasta autorizar — asignado atómicamente junto con <see cref="AuthorizeRequestPayloadHash"/> en <see cref="Authorize"/>.</summary>
    public Guid? AuthorizeClientRequestId { get; private set; }
    public string? AuthorizeRequestPayloadHash { get; private set; }

    /// <summary>Null hasta cancelar — asignado atómicamente junto con <see cref="CancelRequestPayloadHash"/> en <see cref="Cancel"/>.</summary>
    public Guid? CancelClientRequestId { get; private set; }
    public string? CancelRequestPayloadHash { get; private set; }

    /// <summary>Null hasta vincular la NC — asignado atómicamente junto con <see cref="LinkCreditNoteRequestPayloadHash"/> en <see cref="LinkSupplierCreditNote"/>.</summary>
    public Guid? LinkCreditNoteClientRequestId { get; private set; }
    public string? LinkCreditNoteRequestPayloadHash { get; private set; }

    private readonly List<PurchaseReturnDetail> _lines = new();
    public IReadOnlyList<PurchaseReturnDetail> Lines => _lines.AsReadOnly();

    // ── Calculated (NOT persisted) ──────────────────────────────────────
    public decimal GrandTotal => AuthorizedGrandTotal ?? _lines.Sum(l => l.LineGrandTotal);

    private PurchaseReturn() { }

    /// <summary>Input de línea para <see cref="CreateDraft"/>/<see cref="UpdateDraft"/> — snapshot mínimo resuelto por Application desde <c>PurchaseInvoiceDetail</c>.</summary>
    public sealed record DraftLineInput(
        Guid OriginalInvoiceDetailId,
        Guid ItemId,
        decimal Quantity,
        Guid WarehouseId
    );

    /// <summary>
    /// Input por línea necesario para <see cref="Authorize"/> — snapshot de la
    /// <c>PurchaseInvoiceDetail</c> original resuelto por Application (fuera del alcance del
    /// dominio: cargar esa entidad es responsabilidad de un repositorio de otro agregado).
    /// Contiene exactamente los campos que exige la fórmula de §11.1/§19.1bis.
    /// </summary>
    public sealed record OriginalLineSnapshot(
        decimal OriginalQuantity,
        decimal LineSubtotal,
        decimal DiscountAmount,
        decimal VatAmount,
        decimal IceAmount,
        string VatCode,
        decimal VatRate,
        string? IceCode,
        decimal IceRate,
        decimal LandedUnitCost,
        IReadOnlyList<OriginalLineTaxSnapshot> Taxes
    );

    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-1) — snapshot de UNA fila de
    /// <c>PurchaseInvoiceDetailTax</c> de la línea original, resuelto por Application desde
    /// <c>originalLine.Taxes</c> (nunca desde los campos escalares legacy VatAmount/IceAmount de la
    /// factura). Base para prorratear IVA/ICE/IRBPNR en <see cref="Authorize"/>.
    /// </summary>
    public sealed record OriginalLineTaxSnapshot(
        string TaxCode,
        string TaxRateCode,
        string TaxName,
        decimal? Rate,
        ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType CalculationType,
        decimal TaxableBase,
        decimal TaxAmount
    );

    // ── Factory ─────────────────────────────────────────────────────────
    public static PurchaseReturn CreateDraft(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid purchaseInvoiceId,
        Guid supplierId,
        string reason,
        IEnumerable<DraftLineInput> lines,
        Guid createdBy,
        Guid createClientRequestId,
        string createRequestPayloadHash
    )
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (purchaseInvoiceId == Guid.Empty)
            throw new ArgumentException(
                "La factura de compra origen es obligatoria.",
                nameof(purchaseInvoiceId)
            );
        if (supplierId == Guid.Empty)
            throw new ArgumentException("El proveedor es obligatorio.", nameof(supplierId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "El motivo de la devolución es obligatorio.",
                nameof(reason)
            );
        if (createClientRequestId == Guid.Empty)
            throw new ArgumentException(
                "El identificador de idempotencia de creación es obligatorio.",
                nameof(createClientRequestId)
            );
        if (string.IsNullOrWhiteSpace(createRequestPayloadHash))
            throw new ArgumentException(
                "La huella del payload de creación es obligatoria.",
                nameof(createRequestPayloadHash)
            );

        var purchaseReturn = new PurchaseReturn
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            PurchaseInvoiceId = purchaseInvoiceId,
            SupplierId = supplierId,
            Reason = reason.Trim(),
            Status = PurchaseReturnStatus.Draft,
            FiscalStatus = PurchaseReturnFiscalStatus.NotApplicable,
            CreateClientRequestId = createClientRequestId,
            CreateRequestPayloadHash = createRequestPayloadHash,
        };
        purchaseReturn.SetCreated(createdBy);
        purchaseReturn.ReplaceLines(lines);

        return purchaseReturn;
    }

    // ── UpdateDraft ─────────────────────────────────────────────────────
    public void UpdateDraft(string reason, IEnumerable<DraftLineInput> lines, Guid updatedBy)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "El motivo de la devolución es obligatorio.",
                nameof(reason)
            );

        Reason = reason.Trim();
        ReplaceLines(lines);
        SetUpdated(updatedBy);
    }

    private void ReplaceLines(IEnumerable<DraftLineInput> lines)
    {
        var materialized = lines.ToList();
        if (materialized.Count == 0)
            throw new ArgumentException(
                "La devolución debe tener al menos una línea.",
                nameof(lines)
            );

        var duplicated = materialized
            .GroupBy(l => l.OriginalInvoiceDetailId)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null)
            throw new ArgumentException(
                "Una misma línea de factura no puede aparecer dos veces en la misma devolución.",
                nameof(lines)
            );

        _lines.Clear();
        foreach (var input in materialized)
        {
            _lines.Add(
                PurchaseReturnDetail.Create(
                    Id,
                    TenantId,
                    input.OriginalInvoiceDetailId,
                    input.ItemId,
                    input.Quantity,
                    input.WarehouseId
                )
            );
        }
    }

    // ── Authorize (final para las líneas — irreversible) ─────────────────
    /// <summary>
    /// Congela líneas, calcula los snapshots financieros y de costo (§11.1, §19.1bis), transiciona
    /// <c>Draft → Authorized</c>, fija <c>FiscalStatus → PendingSupplierCreditNote</c> y dispara
    /// <see cref="PurchaseReturnAuthorizedEvent"/>. Si el excedente sobre <paramref name="balanceDueBeforeApplication"/>
    /// es mayor a cero, crea y retorna el <see cref="SupplierCredit"/> correspondiente — su
    /// <see cref="SupplierCredit.BranchId"/> hereda literalmente <see cref="BranchId"/> (Branch
    /// Ownership Rule, §5.2), nunca un valor resuelto de forma independiente.
    /// </summary>
    /// <param name="returnNumber">Ya capturado por <c>PurchaseReturnSequence.CaptureNextAsync</c> (Fase 2) — el dominio solo lo persiste.</param>
    /// <param name="originalLinesByDetailId">Snapshot de la <c>PurchaseInvoiceDetail</c> original de cada línea, resuelto por Application, indexado por <c>OriginalInvoiceDetailId</c>.</param>
    /// <param name="balanceDueBeforeApplication">
    /// <c>AccountsPayable.OutstandingAmount</c> ya cargado bajo lock por el caller — el dominio de
    /// <see cref="PurchaseReturn"/> no conoce el repositorio de <c>AccountsPayable</c>, es un
    /// agregado distinto (§6, §11.2).
    /// </param>
    /// <param name="currencyCode">Snapshot de <c>PurchaseInvoice.CurrencyCode</c> — solo se usa para fijar <c>SupplierCredit.CurrencyCode</c> si se crea crédito.</param>
    /// <param name="hasIssuedWithholding">Resuelto por Application desde <c>IssuedWithholding.Status</c> bajo lock — si es <c>true</c>, rechaza (§17, §21 <c>PR-006</c>).</param>
    /// <param name="updatedBy">Actor que ejecuta la autorización.</param>
    /// <param name="authorizeClientRequestId">Idempotencia obligatoria de autorización (§16.2).</param>
    /// <param name="authorizeRequestPayloadHash">Huella determinista del payload de autorización (§16.2).</param>
    public SupplierCredit? Authorize(
        string returnNumber,
        IReadOnlyDictionary<Guid, OriginalLineSnapshot> originalLinesByDetailId,
        decimal balanceDueBeforeApplication,
        string currencyCode,
        bool hasIssuedWithholding,
        Guid updatedBy,
        Guid authorizeClientRequestId,
        string authorizeRequestPayloadHash
    )
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(returnNumber))
            throw new ArgumentException(
                "El número de devolución es obligatorio.",
                nameof(returnNumber)
            );
        if (_lines.Count == 0)
            throw new InvalidOperationException(
                "No puedes autorizar esta devolución porque no tiene líneas agregadas."
            );
        if (hasIssuedWithholding)
            throw new InvalidOperationException(
                "No puedes autorizar esta devolución porque la factura de compra tiene una retención emitida."
            );
        if (balanceDueBeforeApplication < 0)
            throw new ArgumentException(
                "El saldo pendiente de la cuenta por pagar no puede ser negativo.",
                nameof(balanceDueBeforeApplication)
            );
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("La moneda es obligatoria.", nameof(currencyCode));
        if (authorizeClientRequestId == Guid.Empty)
            throw new ArgumentException(
                "El identificador de idempotencia de autorización es obligatorio.",
                nameof(authorizeClientRequestId)
            );
        if (string.IsNullOrWhiteSpace(authorizeRequestPayloadHash))
            throw new ArgumentException(
                "La huella del payload de autorización es obligatoria.",
                nameof(authorizeRequestPayloadHash)
            );

        foreach (var line in _lines)
        {
            if (
                !originalLinesByDetailId.TryGetValue(line.OriginalInvoiceDetailId, out var original)
            )
                throw new InvalidOperationException(
                    "Falta el snapshot de la línea de factura original para poder autorizar la devolución."
                );
            if (original.OriginalQuantity <= 0)
                throw new InvalidOperationException(
                    "La cantidad de la línea de factura original debe ser mayor a cero."
                );

            var fraction = line.Quantity / original.OriginalQuantity;
            var returnedSubtotal = Round2(
                fraction * (original.LineSubtotal - original.DiscountAmount)
            );
            var returnedVat = Round2(fraction * original.VatAmount);
            var returnedIce = Round2(fraction * original.IceAmount);
            var returnedDiscount = Round2(fraction * original.DiscountAmount);
            var historicalCost = Round2(original.LandedUnitCost * line.Quantity);

            // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-1) — prorratea CADA fila del
            // snapshot fiscal original (IVA/ICE/IRBPNR) por la misma fracción que ya rige
            // returnedVat/returnedIce — nunca recalculado desde la configuración tributaria actual
            // del producto, siempre una proporción del snapshot ya congelado en la factura.
            var returnedTaxes = original.Taxes.Select(t => new PurchaseReturnDetail.ProratedTaxLine(
                t.TaxCode,
                t.TaxRateCode,
                t.TaxName,
                t.Rate,
                t.CalculationType,
                Round2(fraction * t.TaxableBase),
                Round2(fraction * t.TaxAmount)
            ));

            line.Freeze(
                original.LandedUnitCost,
                original.VatCode,
                original.VatRate,
                original.IceCode,
                original.IceRate,
                returnedSubtotal,
                returnedDiscount,
                returnedVat,
                returnedIce,
                historicalCost,
                returnedTaxes
            );
        }

        AuthorizedSubtotal = _lines.Sum(l => l.ReturnedSubtotal!.Value);
        AuthorizedVatTotal = _lines.Sum(l => l.ReturnedVatAmount!.Value);
        AuthorizedIceTotal = _lines.Sum(l => l.ReturnedIceAmount!.Value);
        AuthorizedDiscountTotal = _lines.Sum(l => l.ReturnedDiscountAmount!.Value);
        AuthorizedIrbpnrTotal = _lines.Sum(l => l.IrbpnrAmount);
        AuthorizedGrandTotal =
            AuthorizedSubtotal + AuthorizedVatTotal + AuthorizedIceTotal + AuthorizedIrbpnrTotal;
        HistoricalCostTotal = _lines.Sum(l => l.HistoricalCostAmount!.Value);
        CostVarianceTotal = HistoricalCostTotal - AuthorizedSubtotal;

        var appliedToPayable = Math.Min(AuthorizedGrandTotal.Value, balanceDueBeforeApplication);
        var supplierCreditAmount = AuthorizedGrandTotal.Value - appliedToPayable;
        AppliedToPayableAmount = appliedToPayable;
        SupplierCreditAmount = supplierCreditAmount;

        ReturnNumber = returnNumber.Trim();
        Status = PurchaseReturnStatus.Authorized;
        FiscalStatus = PurchaseReturnFiscalStatus.PendingSupplierCreditNote;
        AuthorizedAtUtc = DateTime.UtcNow;
        AuthorizedByUserId = updatedBy;
        AuthorizeClientRequestId = authorizeClientRequestId;
        AuthorizeRequestPayloadHash = authorizeRequestPayloadHash;
        SetUpdated(updatedBy);

        SupplierCredit? credit = null;
        if (supplierCreditAmount > 0)
        {
            credit = SupplierCredit.CreateFromReturn(
                TenantId,
                CompanyId,
                BranchId,
                SupplierId,
                currencyCode,
                Id,
                supplierCreditAmount,
                updatedBy
            );
        }

        RaiseDomainEvent(
            new PurchaseReturnAuthorizedEvent(
                Id,
                PurchaseInvoiceId,
                SupplierId,
                BranchId,
                TenantId,
                CompanyId,
                ReturnNumber,
                updatedBy,
                AuthorizedSubtotal.Value,
                AuthorizedVatTotal.Value,
                AuthorizedIceTotal.Value,
                AuthorizedDiscountTotal.Value,
                AuthorizedGrandTotal.Value,
                HistoricalCostTotal.Value,
                CostVarianceTotal.Value,
                AppliedToPayableAmount.Value,
                SupplierCreditAmount.Value,
                credit?.Id,
                Reason
            )
        );

        return credit;
    }

    // ── Vínculo de NC (§9.1, §18) — solo muta FiscalStatus, sin efectos financieros ───
    public void LinkSupplierCreditNote(
        Guid supplierCreditNoteDocumentId,
        Guid updatedBy,
        Guid linkCreditNoteClientRequestId,
        string linkCreditNoteRequestPayloadHash
    )
    {
        if (Status != PurchaseReturnStatus.Authorized)
            throw new InvalidOperationException(
                "Solo se puede vincular una Nota de Crédito a una devolución autorizada."
            );
        if (FiscalStatus != PurchaseReturnFiscalStatus.PendingSupplierCreditNote)
            throw new InvalidOperationException(
                "Esta devolución ya tiene una Nota de Crédito vinculada."
            );
        if (supplierCreditNoteDocumentId == Guid.Empty)
            throw new ArgumentException(
                "El documento de Nota de Crédito es obligatorio.",
                nameof(supplierCreditNoteDocumentId)
            );
        if (linkCreditNoteClientRequestId == Guid.Empty)
            throw new ArgumentException(
                "El identificador de idempotencia del vínculo de NC es obligatorio.",
                nameof(linkCreditNoteClientRequestId)
            );
        if (string.IsNullOrWhiteSpace(linkCreditNoteRequestPayloadHash))
            throw new ArgumentException(
                "La huella del payload del vínculo de NC es obligatoria.",
                nameof(linkCreditNoteRequestPayloadHash)
            );

        SupplierCreditNoteDocumentId = supplierCreditNoteDocumentId;
        FiscalStatus = PurchaseReturnFiscalStatus.SupplierCreditNoteRegistered;
        LinkCreditNoteClientRequestId = linkCreditNoteClientRequestId;
        LinkCreditNoteRequestPayloadHash = linkCreditNoteRequestPayloadHash;
        SetUpdated(updatedBy);

        // P0-02 Fase 9 — único efecto de este método es documental (§18.5: sin inventario/CxP/
        // crédito/contabilidad). Evento agregado exclusivamente para que
        // PurchaseReturnAuditHandler (ADR-022, Entity Audit) registre el vínculo — nunca para
        // disparar un PostingFact (§19.5, ningún traductor debe existir para este evento).
        RaiseDomainEvent(
            new PurchaseReturnSupplierCreditNoteLinkedEvent(
                Id,
                PurchaseInvoiceId,
                SupplierId,
                BranchId,
                TenantId,
                CompanyId,
                ReturnNumber!,
                GrandTotal,
                supplierCreditNoteDocumentId,
                updatedBy
            )
        );
    }

    // ── Cancel ─────────────────────────────────────────────────────────
    /// <summary>
    /// Válido desde <c>Draft</c> (sin reversas) o desde <c>Authorized</c> (con reversas auditadas
    /// por Application). La precondición de crédito íntegro
    /// (<c>SupplierCredit.AvailableAmount == OriginalAmount</c>, §5.1 casos 6/7, <c>PR-011</c>) se
    /// revalida bajo lock por el caller <em>antes</em> de invocar este método — el dominio no
    /// conoce <see cref="SupplierCredit"/> desde aquí porque es un agregado distinto; este método
    /// solo aplica la transición una vez que Application ya confirmó la precondición.
    /// </summary>
    public void Cancel(
        string reason,
        Guid cancelledBy,
        Guid cancelClientRequestId,
        string cancelRequestPayloadHash
    )
    {
        if (Status == PurchaseReturnStatus.Cancelled)
            throw new InvalidOperationException("Esta devolución ya está cancelada.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "El motivo de la cancelación es obligatorio.",
                nameof(reason)
            );
        if (cancelClientRequestId == Guid.Empty)
            throw new ArgumentException(
                "El identificador de idempotencia de cancelación es obligatorio.",
                nameof(cancelClientRequestId)
            );
        if (string.IsNullOrWhiteSpace(cancelRequestPayloadHash))
            throw new ArgumentException(
                "La huella del payload de cancelación es obligatoria.",
                nameof(cancelRequestPayloadHash)
            );

        var previousStatus = Status;
        Status = PurchaseReturnStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
        CancelledByUserId = cancelledBy;
        CancellationReason = reason.Trim();
        CancelClientRequestId = cancelClientRequestId;
        CancelRequestPayloadHash = cancelRequestPayloadHash;
        SetUpdated(cancelledBy);

        RaiseDomainEvent(
            new PurchaseReturnCancelledEvent(
                Id,
                PurchaseInvoiceId,
                SupplierId,
                BranchId,
                TenantId,
                CompanyId,
                ReturnNumber,
                CancellationReason,
                cancelledBy,
                previousStatus == PurchaseReturnStatus.Authorized ? AppliedToPayableAmount : null,
                previousStatus == PurchaseReturnStatus.Authorized ? SupplierCreditAmount : null,
                previousStatus == PurchaseReturnStatus.Authorized ? HistoricalCostTotal : null,
                previousStatus == PurchaseReturnStatus.Authorized ? CostVarianceTotal : null,
                previousStatus == PurchaseReturnStatus.Authorized ? AuthorizedVatTotal : null,
                previousStatus == PurchaseReturnStatus.Authorized ? AuthorizedIceTotal : null
            )
        );
    }

    // ── Guards ─────────────────────────────────────────────────────────
    private void EnsureDraft()
    {
        if (Status != PurchaseReturnStatus.Draft)
            throw new InvalidOperationException(
                "Esta devolución ya no está en borrador (fue autorizada o cancelada), por lo que no se puede modificar."
            );
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
}
