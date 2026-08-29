using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Cabecera fiscal única de toda nota de crédito de compra/proveedor — diseño
/// FLOW-READY-02C-R1.1. Cubre los dos tipos de aplicación (<see cref="PurchaseCreditNoteApplicationType"/>):
/// <c>Discount</c> (descuento/promoción, flujo original FLOW-READY-02C, autoriza/cancela aquí mismo
/// contra <c>AccountsPayable.CreditNoteAmount</c>, sin inventario ni efecto contable) y
/// <c>Return</c> (devolución física, donde esta entidad es solo captura/referencia del documento
/// fiscal — el movimiento de inventario/CxP/<c>SupplierCredit</c> sigue siendo responsabilidad
/// exclusiva de <see cref="PurchaseReturn"/>, vinculada vía <see cref="LinkPurchaseReturn"/>; nunca
/// se reimplementa aquí, ni se autoriza vía <see cref="Authorize"/>).
/// </summary>
public sealed class PurchaseCreditNote : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int CreditNoteNumberMaxLen = 17;
    public const int AccessKeyMaxLen = 49;
    public const int AuthorizationNumberMaxLen = 49;
    public const int ReasonMaxLen = 500;
    public const int CancellationReasonMaxLen = 500;

    public Guid CompanyId { get; private set; }

    /// <summary>Branch Ownership Rule — asignado exclusivamente al crear el borrador, inmutable tras la creación.</summary>
    public Guid BranchId { get; private set; }

    public Guid SupplierId { get; private set; }
    public Guid PurchaseInvoiceId { get; private set; }

    /// <summary>Referencia (no copia) al <c>PurchaseReceptionDocument</c> tipo NC vinculado — opcional, 1:1.</summary>
    public Guid? ReceptionDocumentId { get; private set; }

    /// <summary>Obligatorio desde <see cref="CreateDraft"/>, inmutable — determina el motor que aplica esta nota de crédito (§FLOW-READY-02C-R1.1).</summary>
    public PurchaseCreditNoteApplicationType ApplicationType { get; private set; }

    /// <summary>Set-once vía <see cref="LinkPurchaseReturn"/> — solo para <see cref="PurchaseCreditNoteApplicationType.Return"/>.</summary>
    public Guid? LinkedPurchaseReturnId { get; private set; }

    public PurchaseCreditNoteStatus Status { get; private set; } = PurchaseCreditNoteStatus.Draft;

    public string CreditNoteNumber { get; private set; } = null!;
    public string? AccessKey { get; private set; }
    public string? AuthorizationNumber { get; private set; }

    /// <summary>Fecha documental/SRI de autorización — nunca un instante, no se persiste con hora (FLOW-READY-02C-R1.1-FIX01).</summary>
    public DateOnly? AuthorizationDate { get; private set; }

    /// <summary>Fecha fiscal/documental de emisión — mismo criterio que <see cref="PurchaseInvoice.IssueDate"/> (FLOW-READY-02C-R1.1-FIX01).</summary>
    public DateOnly IssueDate { get; private set; }

    public string Reason { get; private set; } = null!;

    // ── Snapshot financiero (editable en Draft, congelado al autorizar — §5.3) ──
    public decimal Subtotal { get; private set; }
    public decimal IceAmount { get; private set; }
    public decimal VatAmount { get; private set; }

    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2) — suma de
    /// <c>PurchaseCreditNoteTaxSummary.IrbpnrAmount</c>. Se incluye en <see cref="TotalAmount"/> (es
    /// el total financiero propio de la NC, aplicado a CxP — igual criterio que
    /// <c>PurchaseReturn.AuthorizedGrandTotal</c> en la Subfase 5D-1). Deliberadamente NO se agrega a
    /// <see cref="PurchaseCreditNoteAuthorizedEvent"/> en esta subfase — ese evento alimenta
    /// el traductor contable, fuera de alcance (Subfase 5E).
    /// </summary>
    public decimal IrbpnrAmount { get; private set; }
    public decimal TotalAmount { get; private set; }

    /// <summary>= TotalAmount, congelado en Authorize() — nunca truncado (§0.2 ajuste #1, §4.2).</summary>
    public decimal? AppliedToPayableAmount { get; private set; }

    public DateTime? AuthorizedAtUtc { get; private set; }
    public Guid? AuthorizedByUserId { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }

    // ── Idempotencia (mismo mecanismo que PurchaseReturn) ──
    public Guid CreateClientRequestId { get; private set; }
    public string CreateRequestPayloadHash { get; private set; } = null!;

    public Guid? AuthorizeClientRequestId { get; private set; }
    public string? AuthorizeRequestPayloadHash { get; private set; }

    public Guid? CancelClientRequestId { get; private set; }
    public string? CancelRequestPayloadHash { get; private set; }

    private readonly List<PurchaseCreditNoteDetail> _lines = new();
    public IReadOnlyList<PurchaseCreditNoteDetail> Lines => _lines.AsReadOnly();

    /// <summary>
    /// FLOW-READY-02C-R1.2 — flujo principal de descuento/promoción: una línea por grupo de
    /// impuesto real de la compra (<see cref="PurchaseInvoiceTaxSummary"/>), nunca líneas libres.
    /// <see cref="Lines"/> se mantiene solo por compatibilidad con NC ya creadas antes de esta fase.
    /// </summary>
    private readonly List<PurchaseCreditNoteTaxSummary> _taxSummaries = new();
    public IReadOnlyList<PurchaseCreditNoteTaxSummary> TaxSummaries => _taxSummaries.AsReadOnly();

    private PurchaseCreditNote() { }

    /// <summary>Input de línea libre (legado) para <see cref="CreateDraft"/>/<see cref="UpdateDraft"/> — ya no es el flujo principal (§FLOW-READY-02C-R1.2).</summary>
    public sealed record DraftLineInput(
        string Description,
        decimal Subtotal,
        string? VatCode,
        decimal? VatRate,
        decimal VatAmount
    );

    /// <summary>
    /// Input de línea por resumen fiscal de compra para <see cref="CreateDraft"/>/<see cref="UpdateDraft"/>
    /// — flujo principal de <c>ApplicationType.Discount</c> (§FLOW-READY-02C-R1.2). VatCode/VatRate/
    /// VatName/IceCode/IceRate/IceName deben resolverse en Application desde el
    /// <see cref="PurchaseInvoiceTaxSummary"/> real de la compra (<see cref="SourcePurchaseInvoiceTaxSummaryId"/>)
    /// — el dominio nunca los acepta como "confía en el cliente", pero tampoco los vuelve a resolver:
    /// son responsabilidad de quien arma este input.
    /// </summary>
    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2) — <c>SourceTaxableBase</c>/
    /// <c>SourceIrbpnrAmount</c> son el <c>TaxableBase</c>/<c>IrbpnrAmount</c> TOTAL del
    /// <see cref="PurchaseInvoiceTaxSummary"/> de origen (nunca la porción de esta NC) — la base que
    /// <see cref="ReplaceTaxSummaryLines"/> necesita para prorratear IRBPNR por la misma fracción que
    /// ya rige IVA/ICE en este flujo. IrbpnrCode/IrbpnrRate/IrbpnrName, igual que Vat/Ice, deben venir
    /// resueltos por Application desde el resumen fiscal real — el dominio nunca los acepta "confía
    /// en el cliente".
    /// </summary>
    public sealed record TaxSummaryDraftLineInput(
        Guid SourcePurchaseInvoiceTaxSummaryId,
        string VatCode,
        decimal VatRate,
        string? VatName,
        string? IceCode,
        decimal IceRate,
        string? IceName,
        decimal TaxableBase,
        string? IrbpnrCode = null,
        decimal IrbpnrRate = 0m,
        string? IrbpnrName = null,
        decimal SourceTaxableBase = 0m,
        decimal SourceIrbpnrAmount = 0m
    );

    // ── Factory ─────────────────────────────────────────────────────────
    public static PurchaseCreditNote CreateDraft(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid supplierId,
        Guid purchaseInvoiceId,
        Guid? receptionDocumentId,
        PurchaseCreditNoteApplicationType applicationType,
        string creditNoteNumber,
        string? accessKey,
        string? authorizationNumber,
        DateOnly? authorizationDate,
        DateOnly issueDate,
        string reason,
        IEnumerable<DraftLineInput> lines,
        IEnumerable<TaxSummaryDraftLineInput> taxSummaryLines,
        Guid createdBy,
        Guid createClientRequestId,
        string createRequestPayloadHash
    )
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (supplierId == Guid.Empty)
            throw new ArgumentException("El proveedor es obligatorio.", nameof(supplierId));
        if (purchaseInvoiceId == Guid.Empty)
            throw new ArgumentException(
                "La factura de compra afectada es obligatoria.",
                nameof(purchaseInvoiceId)
            );
        if (receptionDocumentId == Guid.Empty)
            throw new ArgumentException(
                "El documento de recepción vinculado no puede ser un Guid vacío.",
                nameof(receptionDocumentId)
            );
        if (!Enum.IsDefined(typeof(PurchaseCreditNoteApplicationType), applicationType))
            throw new ArgumentException(
                "El tipo de aplicación de la nota de crédito (Return/Discount) es obligatorio.",
                nameof(applicationType)
            );
        if (string.IsNullOrWhiteSpace(creditNoteNumber))
            throw new ArgumentException(
                "El número de nota de crédito es obligatorio.",
                nameof(creditNoteNumber)
            );
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "El motivo/concepto de la nota de crédito es obligatorio.",
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

        var creditNote = new PurchaseCreditNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            PurchaseInvoiceId = purchaseInvoiceId,
            ReceptionDocumentId = receptionDocumentId,
            ApplicationType = applicationType,
            Status = PurchaseCreditNoteStatus.Draft,
            CreditNoteNumber = creditNoteNumber.Trim(),
            AccessKey = OptionalCode.Normalize(accessKey),
            AuthorizationNumber = OptionalCode.Normalize(authorizationNumber),
            AuthorizationDate = authorizationDate,
            IssueDate = issueDate,
            Reason = reason.Trim(),
            CreateClientRequestId = createClientRequestId,
            CreateRequestPayloadHash = createRequestPayloadHash,
        };
        creditNote.SetCreated(createdBy);
        creditNote.ReplaceLines(lines);
        creditNote.ReplaceTaxSummaryLines(taxSummaryLines);
        creditNote.EnsureHasContent();
        creditNote.RecalculateTotals();

        return creditNote;
    }

    // ── UpdateDraft ─────────────────────────────────────────────────────
    public void UpdateDraft(
        string creditNoteNumber,
        string? accessKey,
        string? authorizationNumber,
        DateOnly? authorizationDate,
        DateOnly issueDate,
        string reason,
        IEnumerable<DraftLineInput> lines,
        IEnumerable<TaxSummaryDraftLineInput> taxSummaryLines,
        Guid updatedBy
    )
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(creditNoteNumber))
            throw new ArgumentException(
                "El número de nota de crédito es obligatorio.",
                nameof(creditNoteNumber)
            );
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "El motivo/concepto de la nota de crédito es obligatorio.",
                nameof(reason)
            );

        CreditNoteNumber = creditNoteNumber.Trim();
        AccessKey = OptionalCode.Normalize(accessKey);
        AuthorizationNumber = OptionalCode.Normalize(authorizationNumber);
        AuthorizationDate = authorizationDate;
        IssueDate = issueDate;
        Reason = reason.Trim();
        ReplaceLines(lines);
        ReplaceTaxSummaryLines(taxSummaryLines);
        EnsureHasContent();
        RecalculateTotals();
        SetUpdated(updatedBy);
    }

    private void ReplaceLines(IEnumerable<DraftLineInput> lines)
    {
        _lines.Clear();
        foreach (var input in lines)
        {
            _lines.Add(
                PurchaseCreditNoteDetail.Create(
                    Id,
                    TenantId,
                    input.Description,
                    input.Subtotal,
                    input.VatCode,
                    input.VatRate,
                    input.VatAmount
                )
            );
        }
    }

    /// <summary>
    /// FLOW-READY-02C-R1.2 — reemplaza íntegramente las líneas de resumen fiscal (flujo principal
    /// de Discount). Cada input ya debe traer la identidad de impuesto (VatCode/VatRate/IceCode/
    /// IceRate/nombres) resuelta por Application desde el <see cref="PurchaseInvoiceTaxSummary"/> de
    /// origen — este método solo calcula IceAmount/VatAmount con <see cref="SriTaxCalculator"/> sobre
    /// la base de descuento, nunca confía en impuestos ya calculados desde fuera.
    /// </summary>
    private void ReplaceTaxSummaryLines(IEnumerable<TaxSummaryDraftLineInput> taxSummaryLines)
    {
        _taxSummaries.Clear();
        foreach (var input in taxSummaryLines)
        {
            var (iceAmount, vatAmount, _) = SriTaxCalculator.Compute(
                input.TaxableBase,
                input.VatRate,
                !string.IsNullOrWhiteSpace(input.IceCode) ? input.IceRate : 0m
            );

            // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2) — IRBPNR se prorratea por la
            // misma fracción TaxableBase/fuente.TaxableBase que ya rige IVA/ICE en este flujo (para
            // Percentage, SriTaxCalculator ya es matemáticamente equivalente a esa proporción —
            // aplicar la misma fracción al monto agregado de IRBPNR es la única forma correcta
            // cuando IRBPNR es de monto fijo por unidad (Specific), sin recalcular desde una tarifa.
            var irbpnrAmount = 0m;
            if (!string.IsNullOrWhiteSpace(input.IrbpnrCode) && input.SourceTaxableBase > 0)
            {
                var fraction = input.TaxableBase / input.SourceTaxableBase;
                irbpnrAmount = Round2(fraction * input.SourceIrbpnrAmount);
            }

            // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2 — corrección post-revisión):
            // se construye una fila por impuesto real (nunca una columna fija por impuesto) — mismo
            // patrón que PurchaseInvoiceDetail/PurchaseReturnDetail.
            var taxes = new List<PurchaseCreditNoteTaxLine>
            {
                new(
                    SriTaxCategoryCodes.Vat,
                    input.VatCode,
                    string.IsNullOrWhiteSpace(input.VatName) ? "IVA" : input.VatName,
                    input.VatRate,
                    ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Percentage,
                    vatAmount
                ),
            };
            if (!string.IsNullOrWhiteSpace(input.IceCode))
                taxes.Add(
                    new PurchaseCreditNoteTaxLine(
                        SriTaxCategoryCodes.Ice,
                        input.IceCode,
                        string.IsNullOrWhiteSpace(input.IceName) ? "ICE" : input.IceName,
                        input.IceRate,
                        ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Percentage,
                        iceAmount
                    )
                );
            if (!string.IsNullOrWhiteSpace(input.IrbpnrCode))
                taxes.Add(
                    new PurchaseCreditNoteTaxLine(
                        SriTaxCategoryCodes.Irbpnr,
                        input.IrbpnrCode,
                        string.IsNullOrWhiteSpace(input.IrbpnrName) ? "IRBPNR" : input.IrbpnrName,
                        input.IrbpnrRate,
                        ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific,
                        irbpnrAmount
                    )
                );

            _taxSummaries.Add(
                PurchaseCreditNoteTaxSummary.Create(
                    TenantId,
                    CompanyId,
                    BranchId,
                    Id,
                    PurchaseInvoiceId,
                    input.SourcePurchaseInvoiceTaxSummaryId,
                    input.TaxableBase,
                    taxes
                )
            );
        }
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);

    private void EnsureHasContent()
    {
        if (_lines.Count == 0 && _taxSummaries.Count == 0)
            throw new ArgumentException(
                "La nota de crédito debe tener al menos una línea o un resumen fiscal aplicado."
            );
    }

    private void RecalculateTotals()
    {
        Subtotal = _lines.Sum(l => l.Subtotal) + _taxSummaries.Sum(s => s.TaxableBase);
        IceAmount = _taxSummaries.Sum(s => s.IceAmount);
        VatAmount = _lines.Sum(l => l.VatAmount) + _taxSummaries.Sum(s => s.VatAmount);
        IrbpnrAmount = _taxSummaries.Sum(s => s.IrbpnrAmount);
        TotalAmount = Subtotal + IceAmount + VatAmount + IrbpnrAmount;
    }

    // ── Authorize ─────────────────────────────────────────────────────
    /// <summary>
    /// Autoriza la nota de crédito. Bloquea (no trunca) si <see cref="TotalAmount"/> excede
    /// <paramref name="balanceDueBeforeApplication"/> — ajuste obligatorio #1 del diseño (§4.2):
    /// nunca genera <c>SupplierCredit</c> por el excedente en esta fase. Congela el snapshot
    /// financiero (<see cref="AppliedToPayableAmount"/>) aunque los datos fiscales provengan de un
    /// <c>PurchaseReceptionDocument</c> vinculado (§5.3, ajuste obligatorio #3). No mueve inventario
    /// ni genera <c>PostingFact</c> (§4.3, ajuste obligatorio #2).
    /// </summary>
    public void Authorize(
        decimal balanceDueBeforeApplication,
        Guid updatedBy,
        Guid authorizeClientRequestId,
        string authorizeRequestPayloadHash
    )
    {
        // FLOW-READY-02C-R1.1: las notas de crédito tipo Return nunca se autorizan aquí — el motor
        // de aplicación (inventario/CxP/SupplierCredit) es exclusivamente PurchaseReturn.Authorize(),
        // vinculado vía LinkPurchaseReturn(). Autorizar aquí duplicaría la aplicación contra la CxP.
        if (ApplicationType == PurchaseCreditNoteApplicationType.Return)
            throw new InvalidOperationException(
                "Esta nota de crédito es de tipo Devolución: se aplica mediante la devolución de compra vinculada, no se autoriza aquí."
            );
        EnsureDraft();
        if (_lines.Count == 0 && _taxSummaries.Count == 0)
            throw new InvalidOperationException(
                "No puedes autorizar esta nota de crédito porque no tiene líneas ni resumen fiscal agregado."
            );
        if (balanceDueBeforeApplication < 0)
            throw new ArgumentException(
                "El saldo pendiente de la cuenta por pagar no puede ser negativo.",
                nameof(balanceDueBeforeApplication)
            );
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
        if (TotalAmount > balanceDueBeforeApplication)
            throw new InvalidOperationException(
                "No puedes autorizar esta nota de crédito porque el total excede el saldo pendiente de la factura afectada."
            );

        AppliedToPayableAmount = TotalAmount;
        Status = PurchaseCreditNoteStatus.Authorized;
        AuthorizedAtUtc = DateTime.UtcNow;
        AuthorizedByUserId = updatedBy;
        AuthorizeClientRequestId = authorizeClientRequestId;
        AuthorizeRequestPayloadHash = authorizeRequestPayloadHash;
        SetUpdated(updatedBy);

        RaiseDomainEvent(
            new PurchaseCreditNoteAuthorizedEvent(
                Id,
                PurchaseInvoiceId,
                SupplierId,
                BranchId,
                TenantId,
                CompanyId,
                CreditNoteNumber,
                updatedBy,
                Subtotal,
                VatAmount,
                TotalAmount,
                AppliedToPayableAmount.Value,
                IceAmount
            )
        );
    }

    // ── Cancel ─────────────────────────────────────────────────────────
    public void Cancel(
        string reason,
        Guid cancelledBy,
        Guid cancelClientRequestId,
        string cancelRequestPayloadHash
    )
    {
        if (Status == PurchaseCreditNoteStatus.Cancelled)
            throw new InvalidOperationException("Esta nota de crédito ya está cancelada.");
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
        Status = PurchaseCreditNoteStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
        CancelledByUserId = cancelledBy;
        CancellationReason = reason.Trim();
        CancelClientRequestId = cancelClientRequestId;
        CancelRequestPayloadHash = cancelRequestPayloadHash;
        SetUpdated(cancelledBy);

        RaiseDomainEvent(
            new PurchaseCreditNoteCancelledEvent(
                Id,
                PurchaseInvoiceId,
                SupplierId,
                BranchId,
                TenantId,
                CompanyId,
                CreditNoteNumber,
                CancellationReason,
                cancelledBy,
                previousStatus == PurchaseCreditNoteStatus.Authorized
                    ? AppliedToPayableAmount
                    : null
            )
        );
    }

    // ── LinkPurchaseReturn ────────────────────────────────────────────────
    /// <summary>
    /// Vincula esta nota de crédito (tipo <see cref="PurchaseCreditNoteApplicationType.Return"/>) al
    /// <see cref="PurchaseReturn"/> que la aplica realmente — set-once, puramente documental. Nunca
    /// mueve inventario/CxP/contabilidad (eso ya ocurrió en <c>PurchaseReturn.Authorize()</c>, motor
    /// exclusivo para el caso Return). No cambia <see cref="Status"/> — esta nota permanece como
    /// registro de captura fiscal (§FLOW-READY-02C-R1.1).
    /// </summary>
    public void LinkPurchaseReturn(Guid purchaseReturnId, Guid updatedBy)
    {
        if (ApplicationType != PurchaseCreditNoteApplicationType.Return)
            throw new InvalidOperationException(
                "Solo una nota de crédito de tipo Devolución se puede vincular a una devolución de compra."
            );
        EnsureDraft();
        if (purchaseReturnId == Guid.Empty)
            throw new ArgumentException(
                "La devolución de compra a vincular es obligatoria.",
                nameof(purchaseReturnId)
            );
        if (LinkedPurchaseReturnId is not null)
            throw new InvalidOperationException(
                "Esta nota de crédito ya está vinculada a una devolución de compra."
            );

        LinkedPurchaseReturnId = purchaseReturnId;
        SetUpdated(updatedBy);
    }

    // ── Guards ─────────────────────────────────────────────────────────
    private void EnsureDraft()
    {
        if (Status != PurchaseCreditNoteStatus.Draft)
            throw new InvalidOperationException(
                "Esta nota de crédito ya no está en borrador (fue autorizada o cancelada), por lo que no se puede modificar."
            );
    }
}
