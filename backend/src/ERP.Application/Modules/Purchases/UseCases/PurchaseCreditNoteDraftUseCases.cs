using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentValidation;
using MediatR;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Shared input ────────────────────────────────────────────────────────

public sealed record PurchaseCreditNoteDraftLineInput(
    string Description,
    decimal Subtotal,
    string? VatCode,
    decimal? VatRate,
    decimal VatAmount
);

/// <summary>
/// FLOW-READY-02C-R1.2 — línea de descuento por resumen fiscal de compra (flujo principal de
/// <c>ApplicationType.Discount</c>). El cliente solo envía la referencia y la base a descontar —
/// VatCode/VatRate/nombres/IceCode/IceRate se resuelven en el handler desde el
/// <c>PurchaseInvoiceTaxSummary</c> real de la factura, nunca desde este input.
/// </summary>
public sealed record PurchaseCreditNoteTaxSummaryLineInput(
    Guid SourcePurchaseInvoiceTaxSummaryId,
    decimal TaxableBase
);

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>
/// FLOW-READY-02C.2 — crea el borrador de una nota de crédito de compra (descuento/promoción, v1).
/// No expone <c>BranchId</c> ni <c>SupplierId</c> — el handler resuelve <c>BranchId</c> de
/// <c>ICurrentBranch</c> (Branch Ownership Rule) y <c>SupplierId</c> de la factura afectada. No
/// afecta CxP/inventario/contabilidad/recepción — eso ocurre únicamente al autorizar. Idempotente —
/// <c>ClientRequestId</c> obligatorio (mismo mecanismo que <c>CreatePurchaseReturnDraftCommand</c>).
/// </summary>
public sealed record CreateDraftPurchaseCreditNoteCommand(
    Guid ClientRequestId,
    Guid PurchaseInvoiceId,
    Guid? ReceptionDocumentId,
    PurchaseCreditNoteApplicationType ApplicationType,
    string CreditNoteNumber,
    string? AccessKey,
    string? AuthorizationNumber,
    DateOnly? AuthorizationDate,
    DateOnly IssueDate,
    string Reason,
    IReadOnlyList<PurchaseCreditNoteDraftLineInput> Lines,
    IReadOnlyList<PurchaseCreditNoteTaxSummaryLineInput>? TaxSummaryLines = null
) : IRequest<Result<PurchaseCreditNoteDto>>, IBranchScopedRequest;

/// <summary>FLOW-READY-02C.2 — reemplaza por completo los datos fiscales/motivo/líneas de una <c>PurchaseCreditNote</c> en <c>Draft</c>. Sin idempotencia (no es una operación financiera).</summary>
public sealed record UpdatePurchaseCreditNoteDraftCommand(
    Guid Id,
    string CreditNoteNumber,
    string? AccessKey,
    string? AuthorizationNumber,
    DateOnly? AuthorizationDate,
    DateOnly IssueDate,
    string Reason,
    IReadOnlyList<PurchaseCreditNoteDraftLineInput> Lines,
    IReadOnlyList<PurchaseCreditNoteTaxSummaryLineInput>? TaxSummaryLines = null
) : IRequest<Result<PurchaseCreditNoteDto>>, IBranchScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateDraftPurchaseCreditNoteValidator
    : AbstractValidator<CreateDraftPurchaseCreditNoteCommand>
{
    public CreateDraftPurchaseCreditNoteValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.PurchaseInvoiceId)
            .NotEmpty()
            .WithMessage("La factura afectada es obligatoria.");
        RuleFor(x => x.ApplicationType)
            .IsInEnum()
            .WithMessage("El tipo de aplicación de la nota de crédito (Devolución/Descuento) es obligatorio.");
        RuleFor(x => x.CreditNoteNumber)
            .NotEmpty()
            .WithMessage("El número de nota de crédito es obligatorio.")
            .MaximumLength(PurchaseCreditNote.CreditNoteNumberMaxLen);
        RuleFor(x => x.AccessKey).MaximumLength(PurchaseCreditNote.AccessKeyMaxLen);
        RuleFor(x => x.AuthorizationNumber)
            .MaximumLength(PurchaseCreditNote.AuthorizationNumberMaxLen);
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo/concepto es obligatorio.")
            .MaximumLength(PurchaseCreditNote.ReasonMaxLen);
        RuleFor(x => x)
            .Must(x => x.Lines.Count > 0 || (x.TaxSummaryLines?.Count ?? 0) > 0)
            .WithMessage("Debe incluir al menos una línea o un resumen fiscal aplicado.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Description)
                    .NotEmpty()
                    .WithMessage("La descripción de la línea es obligatoria.")
                    .MaximumLength(PurchaseCreditNoteDetail.DescriptionMaxLen);
                line.RuleFor(l => l.Subtotal)
                    .GreaterThan(0)
                    .WithMessage("El subtotal de la línea debe ser mayor a cero.");
                line.RuleFor(l => l.VatAmount)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("El IVA de la línea no puede ser negativo.");
            });
        RuleForEach(x => x.TaxSummaryLines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.SourcePurchaseInvoiceTaxSummaryId)
                    .NotEmpty()
                    .WithMessage("El resumen fiscal de compra de origen es obligatorio.");
                line.RuleFor(l => l.TaxableBase)
                    .GreaterThan(0)
                    .WithMessage("La base de descuento debe ser mayor a cero.");
            });
    }
}

public sealed class UpdatePurchaseCreditNoteDraftValidator
    : AbstractValidator<UpdatePurchaseCreditNoteDraftCommand>
{
    public UpdatePurchaseCreditNoteDraftValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CreditNoteNumber)
            .NotEmpty()
            .WithMessage("El número de nota de crédito es obligatorio.")
            .MaximumLength(PurchaseCreditNote.CreditNoteNumberMaxLen);
        RuleFor(x => x.AccessKey).MaximumLength(PurchaseCreditNote.AccessKeyMaxLen);
        RuleFor(x => x.AuthorizationNumber)
            .MaximumLength(PurchaseCreditNote.AuthorizationNumberMaxLen);
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo/concepto es obligatorio.")
            .MaximumLength(PurchaseCreditNote.ReasonMaxLen);
        RuleFor(x => x)
            .Must(x => x.Lines.Count > 0 || (x.TaxSummaryLines?.Count ?? 0) > 0)
            .WithMessage("Debe incluir al menos una línea o un resumen fiscal aplicado.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Description)
                    .NotEmpty()
                    .WithMessage("La descripción de la línea es obligatoria.")
                    .MaximumLength(PurchaseCreditNoteDetail.DescriptionMaxLen);
                line.RuleFor(l => l.Subtotal)
                    .GreaterThan(0)
                    .WithMessage("El subtotal de la línea debe ser mayor a cero.");
                line.RuleFor(l => l.VatAmount)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("El IVA de la línea no puede ser negativo.");
            });
        RuleForEach(x => x.TaxSummaryLines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.SourcePurchaseInvoiceTaxSummaryId)
                    .NotEmpty()
                    .WithMessage("El resumen fiscal de compra de origen es obligatorio.");
                line.RuleFor(l => l.TaxableBase)
                    .GreaterThan(0)
                    .WithMessage("La base de descuento debe ser mayor a cero.");
            });
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreateDraftPurchaseCreditNoteHandler
    : IRequestHandler<CreateDraftPurchaseCreditNoteCommand, Result<PurchaseCreditNoteDto>>
{
    private readonly IPurchaseCreditNoteRepository _creditNoteRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public CreateDraftPurchaseCreditNoteHandler(
        IPurchaseCreditNoteRepository creditNoteRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _creditNoteRepo = creditNoteRepo;
        _invoiceRepo = invoiceRepo;
        _payableRepo = payableRepo;
        _receptionRepo = receptionRepo;
        _dbEx = dbEx;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
    }

    public async Task<Result<PurchaseCreditNoteDto>> Handle(
        CreateDraftPurchaseCreditNoteCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;

        // ── Idempotencia: buscar por (TenantId, ClientRequestId) antes de tocar cualquier
        // agregado — mismo criterio que CreatePurchaseReturnDraftHandler. ──
        var hash = ComputePayloadHash(cmd);
        var existing = await _creditNoteRepo.GetByCreateClientRequestIdAsync(
            tid,
            cmd.ClientRequestId,
            ct
        );
        if (existing is not null)
            return existing.CreateRequestPayloadHash == hash
                ? Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(existing))
                : Result<PurchaseCreditNoteDto>.ValidationFailure(
                    "Ya existe una solicitud de creación con este identificador pero con datos distintos."
                );

        var invoice = await _invoiceRepo.GetByIdAsync(tid, cmd.PurchaseInvoiceId, ct);
        if (invoice is null)
            return Result<PurchaseCreditNoteDto>.NotFound("Factura de compra no encontrada.");
        if (invoice.Status != PurchaseStatus.Confirmed)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Solo se pueden registrar notas de crédito sobre facturas de compra confirmadas."
            );

        var payable = await _payableRepo.GetByOriginAsync(
            tid,
            _c.CompanyId,
            AccountsPayableOriginType.PurchaseInvoice,
            invoice.Id,
            ct
        );
        if (payable is null)
            return Result<PurchaseCreditNoteDto>.NotFound(
                "Cuenta por pagar de la factura de compra no encontrada."
            );
        if (payable.OutstandingAmount <= 0)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "La factura afectada no tiene saldo pendiente."
            );

        Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionDocument? receptionDoc =
            null;
        if (cmd.ReceptionDocumentId is { } receptionDocumentId)
        {
            var validation = await ValidateReceptionDocumentAsync(
                tid,
                receptionDocumentId,
                invoice,
                ct
            );
            if (validation.Error is not null)
                return validation.Error;
            receptionDoc = validation.Document;
        }

        if (
            !string.IsNullOrWhiteSpace(cmd.AccessKey)
            && await _creditNoteRepo.ExistsByAccessKeyAsync(tid, cmd.AccessKey, ct)
        )
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Ya existe una nota de crédito registrada con esta clave de acceso."
            );

        if (
            await _creditNoteRepo.ExistsBySupplierAndCreditNoteNumberAsync(
                tid,
                _c.CompanyId,
                invoice.SupplierId,
                cmd.CreditNoteNumber,
                ct
            )
        )
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Ya existe una nota de crédito con este número para este proveedor."
            );

        var requestedTaxSummaryLines = cmd.TaxSummaryLines ?? [];
        if (
            cmd.ApplicationType == PurchaseCreditNoteApplicationType.Return
            && requestedTaxSummaryLines.Count > 0
        )
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Las notas de crédito tipo Devolución no aplican resúmenes fiscales — se aplican mediante la devolución de compra vinculada."
            );

        var (taxSummaryLines, taxSummaryError) = await CreditNoteTaxSummaryResolver.ResolveAsync(
            invoice,
            requestedTaxSummaryLines,
            _creditNoteRepo,
            tid,
            excludeCreditNoteId: null,
            ct
        );
        if (taxSummaryError is not null)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(taxSummaryError);

        var lines = cmd.Lines.Select(l => new PurchaseCreditNote.DraftLineInput(
                l.Description,
                l.Subtotal,
                l.VatCode,
                l.VatRate,
                l.VatAmount
            ))
            .ToList();

        PurchaseCreditNote creditNote;
        try
        {
            creditNote = PurchaseCreditNote.CreateDraft(
                tid,
                _c.CompanyId,
                _b.BranchId,
                invoice.SupplierId,
                invoice.Id,
                cmd.ReceptionDocumentId,
                cmd.ApplicationType,
                cmd.CreditNoteNumber,
                cmd.AccessKey,
                cmd.AuthorizationNumber,
                cmd.AuthorizationDate,
                cmd.IssueDate,
                cmd.Reason,
                lines,
                taxSummaryLines,
                _u.UserId,
                cmd.ClientRequestId,
                hash
            );
        }
        catch (ArgumentException ex)
        {
            return Result<PurchaseCreditNoteDto>.ValidationFailure(ex.Message);
        }

        await _creditNoteRepo.AddAsync(creditNote, ct);
        try
        {
            await _creditNoteRepo.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
        {
            var mapped = MapUniqueViolation(info.ConstraintName);
            if (mapped is not null)
                return Result<PurchaseCreditNoteDto>.ValidationFailure(mapped);

            // Colisión de ClientRequestId por una causa distinta al chequeo preventivo (carrera
            // concurrente) — mismo patrón de reconsulta que CreatePurchaseReturnDraftHandler.
            var winner = await _creditNoteRepo.GetByCreateClientRequestIdAsync(
                tid,
                cmd.ClientRequestId,
                ct
            );
            if (winner is null)
                throw;

            return winner.CreateRequestPayloadHash == hash
                ? Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(winner))
                : Result<PurchaseCreditNoteDto>.ValidationFailure(
                    "Ya existe una solicitud de creación con este identificador pero con datos distintos."
                );
        }

        return Result<PurchaseCreditNoteDto>.Success(
            CreditNoteMap.ToDto(
                creditNote,
                invoice.InvoiceNumber,
                invoice.SupplierName,
                payable.OutstandingAmount,
                receptionDoc?.AccessKey
            )
        );
    }

    private async Task<ReceptionValidationResult> ValidateReceptionDocumentAsync(
        Guid tenantId,
        Guid receptionDocumentId,
        Domain.Modules.Purchases.Entities.PurchaseInvoice invoice,
        CancellationToken ct
    )
    {
        var doc = await _receptionRepo.GetByIdAsync(tenantId, receptionDocumentId, ct);
        if (doc is null)
            return new(
                null,
                Result<PurchaseCreditNoteDto>.NotFound(
                    "Documento de recepción (Nota de Crédito) no encontrado."
                )
            );
        if (doc.CompanyId != _c.CompanyId)
            return new(
                null,
                Result<PurchaseCreditNoteDto>.ValidationFailure(
                    "El documento de recepción no pertenece a la empresa activa."
                )
            );
        if (doc.SourceDocType != PurchaseReceptionSourceDocType.CreditNote)
            return new(
                null,
                Result<PurchaseCreditNoteDto>.ValidationFailure(
                    "El documento de recepción indicado no es una Nota de Crédito."
                )
            );
        if (doc.PurchaseId is not null)
            return new(
                null,
                Result<PurchaseCreditNoteDto>.ValidationFailure(
                    "El documento de recepción ya fue procesado/vinculado a otra compra."
                )
            );
        if (await _creditNoteRepo.ExistsByReceptionDocumentIdAsync(tenantId, doc.Id, ct))
            return new(
                null,
                Result<PurchaseCreditNoteDto>.ValidationFailure(
                    "El documento de recepción ya está vinculado a otra nota de crédito."
                )
            );
        if (doc.SupplierId is { } supplierId && supplierId != invoice.SupplierId)
            return new(
                null,
                Result<PurchaseCreditNoteDto>.ValidationFailure(
                    "El proveedor del documento de recepción no coincide con el de la factura afectada."
                )
            );
        if (
            !string.IsNullOrWhiteSpace(doc.ModifiedDocumentNumber)
            && !string.Equals(
                doc.ModifiedDocumentNumber.Trim(),
                invoice.InvoiceNumber.Trim(),
                StringComparison.OrdinalIgnoreCase
            )
        )
            return new(
                null,
                Result<PurchaseCreditNoteDto>.ValidationFailure(
                    "El documento afectado indicado en el documento de recepción no coincide con la factura seleccionada."
                )
            );

        return new(doc, null);
    }

    private sealed record ReceptionValidationResult(
        Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionDocument? Document,
        Result<PurchaseCreditNoteDto>? Error
    );

    public static string? MapUniqueViolation(string? constraintName) =>
        constraintName switch
        {
            "uq_purchase_credit_notes_tenant_reception_document_id" =>
                "El documento de recepción ya está vinculado a otra nota de crédito.",
            "uq_purchase_credit_notes_tenant_access_key" =>
                "Ya existe una nota de crédito registrada con esta clave de acceso.",
            "uq_purchase_credit_notes_tenant_company_supplier_number" =>
                "Ya existe una nota de crédito con este número para este proveedor.",
            "uq_purchase_credit_notes_tenant_linked_purchase_return_id" =>
                "Esta devolución de compra ya está vinculada a otra nota de crédito.",
            _ => null,
        };

    /// <summary>Huella determinista: <c>OperationType</c> + factura + recepción + datos fiscales + motivo + líneas canonicalizadas (orden estable, no depende del JSON recibido).</summary>
    public static string ComputePayloadHash(CreateDraftPurchaseCreditNoteCommand cmd)
    {
        var canonicalLines = cmd
            .Lines.OrderBy(l => l.Description, StringComparer.Ordinal)
            .ThenBy(l => l.Subtotal)
            .Select(l =>
                l.Description.Trim()
                + ":"
                + l.Subtotal.ToString(CultureInfo.InvariantCulture)
                + ":"
                + (l.VatCode ?? "")
                + ":"
                + l.VatAmount.ToString(CultureInfo.InvariantCulture)
            );
        var canonicalTaxSummaryLines = (cmd.TaxSummaryLines ?? [])
            .OrderBy(l => l.SourcePurchaseInvoiceTaxSummaryId)
            .Select(l =>
                l.SourcePurchaseInvoiceTaxSummaryId.ToString("D")
                + ":"
                + l.TaxableBase.ToString(CultureInfo.InvariantCulture)
            );
        var canonical = string.Join(
            '\u0001',
            "CreateDraftPurchaseCreditNote",
            cmd.PurchaseInvoiceId.ToString("D"),
            cmd.ReceptionDocumentId?.ToString("D") ?? "",
            cmd.ApplicationType.ToString(),
            cmd.CreditNoteNumber.Trim(),
            (cmd.AccessKey ?? "").Trim(),
            cmd.IssueDate.ToString("O", CultureInfo.InvariantCulture),
            cmd.Reason.Trim(),
            string.Join('\u0001', canonicalLines),
            string.Join('\u0001', canonicalTaxSummaryLines)
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}

internal static class CreditNoteTaxSummaryResolver
{
    public static async Task<(
        List<PurchaseCreditNote.TaxSummaryDraftLineInput> Resolved,
        string? Error
    )> ResolveAsync(
        Domain.Modules.Purchases.Entities.PurchaseInvoice invoice,
        IReadOnlyList<PurchaseCreditNoteTaxSummaryLineInput> requestLines,
        IPurchaseCreditNoteRepository creditNoteRepo,
        Guid tenantId,
        Guid? excludeCreditNoteId,
        CancellationToken ct
    )
    {
        var resolved = new List<PurchaseCreditNote.TaxSummaryDraftLineInput>();
        if (requestLines.Count == 0)
            return (resolved, null);

        var sourceIds = requestLines
            .Select(l => l.SourcePurchaseInvoiceTaxSummaryId)
            .Distinct()
            .ToList();
        var creditedBySourceId =
            await creditNoteRepo.GetCreditedTaxableBaseByPurchaseTaxSummaryIdsAsync(
                tenantId,
                sourceIds,
                excludeCreditNoteId,
                ct
            );

        foreach (var line in requestLines)
        {
            var source = invoice.TaxSummaries.FirstOrDefault(s =>
                s.Id == line.SourcePurchaseInvoiceTaxSummaryId
            );
            if (source is null)
                return (
                    resolved,
                    "Uno de los resúmenes fiscales indicados no pertenece a la factura afectada."
                );

            var credited = creditedBySourceId.GetValueOrDefault(source.Id);
            var available = source.TaxableBase - credited;
            if (line.TaxableBase > available)
                return (
                    resolved,
                    $"La base de descuento para el impuesto {source.VatCode} excede la base disponible (disponible: {available:F2})."
                );

            resolved.Add(
                new PurchaseCreditNote.TaxSummaryDraftLineInput(
                    source.Id,
                    source.VatCode,
                    source.VatRate,
                    source.VatName,
                    source.IceCode,
                    source.IceRate,
                    source.IceName,
                    line.TaxableBase
                )
            );
        }

        return (resolved, null);
    }
}

public sealed class UpdatePurchaseCreditNoteDraftHandler
    : IRequestHandler<UpdatePurchaseCreditNoteDraftCommand, Result<PurchaseCreditNoteDto>>
{
    private readonly IPurchaseCreditNoteRepository _creditNoteRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public UpdatePurchaseCreditNoteDraftHandler(
        IPurchaseCreditNoteRepository creditNoteRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _creditNoteRepo = creditNoteRepo;
        _invoiceRepo = invoiceRepo;
        _payableRepo = payableRepo;
        _dbEx = dbEx;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PurchaseCreditNoteDto>> Handle(
        UpdatePurchaseCreditNoteDraftCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;

        var creditNote = await _creditNoteRepo.GetByIdAsync(tid, cmd.Id, ct);
        if (creditNote is null)
            return Result<PurchaseCreditNoteDto>.NotFound("Nota de crédito no encontrada.");

        if (
            !string.Equals(creditNote.CreditNoteNumber, cmd.CreditNoteNumber.Trim(), StringComparison.Ordinal)
            && await _creditNoteRepo.ExistsBySupplierAndCreditNoteNumberAsync(
                tid,
                _c.CompanyId,
                creditNote.SupplierId,
                cmd.CreditNoteNumber,
                ct
            )
        )
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Ya existe una nota de crédito con este número para este proveedor."
            );

        if (
            !string.IsNullOrWhiteSpace(cmd.AccessKey)
            && !string.Equals(creditNote.AccessKey, cmd.AccessKey, StringComparison.Ordinal)
            && await _creditNoteRepo.ExistsByAccessKeyAsync(tid, cmd.AccessKey, ct)
        )
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Ya existe una nota de crédito registrada con esta clave de acceso."
            );

        var invoice = await _invoiceRepo.GetByIdAsync(tid, creditNote.PurchaseInvoiceId, ct);
        if (invoice is null)
            return Result<PurchaseCreditNoteDto>.NotFound("Factura de compra no encontrada.");

        var requestedTaxSummaryLines = cmd.TaxSummaryLines ?? [];
        if (
            creditNote.ApplicationType == PurchaseCreditNoteApplicationType.Return
            && requestedTaxSummaryLines.Count > 0
        )
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Las notas de crédito tipo Devolución no aplican resúmenes fiscales — se aplican mediante la devolución de compra vinculada."
            );

        var (taxSummaryLines, taxSummaryError) = await CreditNoteTaxSummaryResolver.ResolveAsync(
            invoice,
            requestedTaxSummaryLines,
            _creditNoteRepo,
            tid,
            excludeCreditNoteId: creditNote.Id,
            ct
        );
        if (taxSummaryError is not null)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(taxSummaryError);

        var lines = cmd.Lines.Select(l => new PurchaseCreditNote.DraftLineInput(
                l.Description,
                l.Subtotal,
                l.VatCode,
                l.VatRate,
                l.VatAmount
            ))
            .ToList();

        try
        {
            creditNote.UpdateDraft(
                cmd.CreditNoteNumber,
                cmd.AccessKey,
                cmd.AuthorizationNumber,
                cmd.AuthorizationDate,
                cmd.IssueDate,
                cmd.Reason,
                lines,
                taxSummaryLines,
                _u.UserId
            );
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Result<PurchaseCreditNoteDto>.ValidationFailure(ex.Message);
        }

        try
        {
            await _creditNoteRepo.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
        {
            var mapped = CreateDraftPurchaseCreditNoteHandler.MapUniqueViolation(
                info.ConstraintName
            );
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                mapped ?? "Conflicto de datos duplicados al actualizar la nota de crédito."
            );
        }

        var payable = await _payableRepo.GetByOriginAsync(
            tid,
            _c.CompanyId,
            AccountsPayableOriginType.PurchaseInvoice,
            invoice.Id,
            ct
        );

        return Result<PurchaseCreditNoteDto>.Success(
            CreditNoteMap.ToDto(
                creditNote,
                invoice?.InvoiceNumber,
                invoice?.SupplierName,
                payable?.OutstandingAmount
            )
        );
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

internal static class CreditNoteMap
{
    public static PurchaseCreditNoteDto ToDto(
        PurchaseCreditNote c,
        string? invoiceNumber = null,
        string? supplierName = null,
        decimal? invoiceBalanceDue = null,
        string? receptionDocumentAccessKey = null
    ) =>
        new(
            c.Id,
            c.PurchaseInvoiceId,
            c.SupplierId,
            c.BranchId,
            c.ReceptionDocumentId,
            c.ApplicationType.ToString(),
            c.LinkedPurchaseReturnId,
            c.Status.ToString(),
            c.CreditNoteNumber,
            c.AccessKey,
            c.AuthorizationNumber,
            c.AuthorizationDate,
            c.IssueDate,
            c.Reason,
            c.Subtotal,
            c.IceAmount,
            c.VatAmount,
            c.TotalAmount,
            c.AppliedToPayableAmount,
            c.AuthorizedAtUtc,
            c.CancelledAtUtc,
            c.CancellationReason,
            c.Lines.Select(l => new PurchaseCreditNoteDetailDto(
                    l.Id,
                    l.Description,
                    l.Subtotal,
                    l.VatCode,
                    l.VatRate,
                    l.VatAmount,
                    l.TotalAmount
                ))
                .ToList(),
            c.TaxSummaries.Select(s => new PurchaseCreditNoteTaxSummaryDto(
                    s.Id,
                    s.SourcePurchaseInvoiceTaxSummaryId,
                    s.VatCode,
                    s.VatRate,
                    s.VatName,
                    s.IceCode,
                    s.IceRate,
                    s.IceName,
                    s.TaxableBase,
                    s.IceAmount,
                    s.VatAmount,
                    s.TotalAmount
                ))
                .ToList(),
            c.CreatedAt,
            c.UpdatedAt,
            invoiceNumber,
            supplierName,
            invoiceBalanceDue,
            receptionDocumentAccessKey
        );

    public static PurchaseCreditNoteListItemDto ToListItemDto(PurchaseCreditNote c) =>
        new(
            c.Id,
            c.PurchaseInvoiceId,
            c.SupplierId,
            c.ApplicationType.ToString(),
            c.Status.ToString(),
            c.CreditNoteNumber,
            c.TotalAmount,
            c.IssueDate,
            c.AuthorizedAtUtc,
            c.CreatedAt
        );
}
