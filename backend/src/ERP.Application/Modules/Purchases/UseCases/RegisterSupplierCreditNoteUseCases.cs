using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── DTO ─────────────────────────────────────────────────────────────────

public sealed record SupplierCreditNoteLinkDto(
    Guid PurchaseReturnId,
    string FiscalStatus,
    Guid SupplierCreditNoteDocumentId,
    string AccessKey,
    string InvoiceNumber,
    decimal TotalAmount,
    string CurrencyCode
);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 9 — registra (o reutiliza por <c>AccessKey</c>) la Nota de Crédito del proveedor y
/// la vincula 1:1 a una <c>PurchaseReturn</c> autorizada, con la validación cuantitativa
/// obligatoria de §18.4bis. Sin locks (§16.1: no compite con inventario/CxP/crédito) — la única
/// protección de carrera es la unicidad a nivel de BD ya existente desde Fase 1/2
/// (<c>uq_purchase_returns_tenant_supplier_credit_note_document_id</c>,
/// <c>uq_purchase_reception_documents_tenant_access_key</c>,
/// <c>uq_purchase_returns_tenant_link_credit_note_client_request_id</c>) + el <c>xmin</c> de
/// <c>PurchaseReturn</c>.
/// </summary>
public sealed record RegisterAndLinkSupplierCreditNoteCommand(
    Guid PurchaseReturnId,
    string AccessKey,
    string SupplierRuc,
    string SupplierName,
    string InvoiceNumber,
    DateOnly IssueDate,
    decimal Subtotal,
    decimal VatAmount,
    decimal TotalAmount,
    string CurrencyCode,
    Guid ClientRequestId
) : IRequest<Result<SupplierCreditNoteLinkDto>>, ICompanyScopedRequest;

public sealed class RegisterAndLinkSupplierCreditNoteValidator
    : AbstractValidator<RegisterAndLinkSupplierCreditNoteCommand>
{
    public RegisterAndLinkSupplierCreditNoteValidator()
    {
        RuleFor(x => x.PurchaseReturnId).NotEmpty();
        RuleFor(x => x.AccessKey).NotEmpty();
        RuleFor(x => x.SupplierRuc).NotEmpty();
        RuleFor(x => x.SupplierName).NotEmpty();
        RuleFor(x => x.InvoiceNumber).NotEmpty();
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.CurrencyCode).NotEmpty();
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class RegisterAndLinkSupplierCreditNoteHandler
    : IRequestHandler<RegisterAndLinkSupplierCreditNoteCommand, Result<SupplierCreditNoteLinkDto>>
{
    private const decimal Tolerance = 0.01m;

    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public RegisterAndLinkSupplierCreditNoteHandler(
        IPurchaseReturnRepository returnRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _receptionRepo = receptionRepo;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _u = u;
    }

    public async Task<Result<SupplierCreditNoteLinkDto>> Handle(
        RegisterAndLinkSupplierCreditNoteCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        // §16.1 fila "Vínculo de NC": transacción documental, sin Lock A/B.
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var purchaseReturn = await _returnRepo.GetByIdAsync(tid, cmd.PurchaseReturnId, ct);
            if (purchaseReturn is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditNoteLinkDto>.NotFound("Devolución no encontrada.");
            }

            // ── Idempotencia (§16.2) — campo único sobre el propio agregado ya cargado. ──
            if (purchaseReturn.LinkCreditNoteClientRequestId == cmd.ClientRequestId)
            {
                await _uow.RollbackAsync(ct);
                var expectedHash = ComputeLinkPayloadHash(
                    cmd,
                    purchaseReturn.Id,
                    purchaseReturn.SupplierCreditNoteDocumentId
                );
                if (purchaseReturn.LinkCreditNoteRequestPayloadHash != expectedHash)
                    // PR-012
                    return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                        "Ya existe una solicitud de vínculo de NC con este identificador pero con datos distintos."
                    );

                var existingDoc = await _receptionRepo.GetByIdAsync(
                    tid,
                    purchaseReturn.SupplierCreditNoteDocumentId!.Value,
                    ct
                );
                return Result<SupplierCreditNoteLinkDto>.Success(Map(purchaseReturn, existingDoc!));
            }

            if (
                purchaseReturn.FiscalStatus
                != Domain
                    .Modules
                    .Purchases
                    .Enums
                    .PurchaseReturnFiscalStatus
                    .PendingSupplierCreditNote
            )
            {
                await _uow.RollbackAsync(ct);
                // SC-009
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "Esta devolución ya tiene una Nota de Crédito vinculada."
                );
            }

            var invoice = await _invoiceRepo.GetByIdAsync(
                tid,
                purchaseReturn.PurchaseInvoiceId,
                ct
            );
            if (invoice is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditNoteLinkDto>.NotFound(
                    "La factura de compra de origen no fue encontrada."
                );
            }

            // ── Registrar/reutilizar PurchaseReceptionDocument(CreditNote) por AccessKey ──
            var document = await _receptionRepo.GetByAccessKeyAsync(tid, cmd.AccessKey, ct);
            var isNewDocument = document is null;
            if (document is null)
            {
                document = PurchaseReceptionDocument.Create(
                    tid,
                    purchaseReturn.CompanyId,
                    purchaseReturn.BranchId,
                    PurchaseReceptionSourceDocType.CreditNote,
                    cmd.SupplierRuc,
                    cmd.SupplierName,
                    purchaseReturn.SupplierId,
                    cmd.AccessKey,
                    cmd.InvoiceNumber,
                    cmd.IssueDate,
                    authorizationDate: null,
                    cmd.Subtotal,
                    cmd.VatAmount,
                    cmd.TotalAmount,
                    uid,
                    currencyCode: cmd.CurrencyCode
                );
            }
            else if (document.SourceDocType != PurchaseReceptionSourceDocType.CreditNote)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "El documento indicado no es una Nota de Crédito."
                );
            }

            // ── SC-008: proveedor coincide con el de la factura original ──
            var supplierRucMatches = string.Equals(
                document.SupplierRuc.Trim(),
                invoice.SupplierTaxId.Trim(),
                StringComparison.OrdinalIgnoreCase
            );
            if (!supplierRucMatches)
            {
                await _uow.RollbackAsync(ct);
                // SC-008
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "La Nota de Crédito pertenece a un proveedor distinto al de la factura original."
                );
            }

            // ── SC-019: moneda no verificable ──
            if (string.IsNullOrWhiteSpace(document.CurrencyCode))
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "La moneda de la Nota de Crédito no es verificable."
                );
            }

            // ── SC-013: moneda coincide con la factura/devolución de origen ──
            if (
                !string.Equals(
                    document.CurrencyCode,
                    invoice.CurrencyCode,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                await _uow.RollbackAsync(ct);
                // SC-013
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "La moneda de la Nota de Crédito no coincide con la de la factura de compra de origen."
                );
            }

            // ── Fecha de emisión no anterior a la factura original ──
            if (document.IssueDate < invoice.IssueDate)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "La fecha de emisión de la Nota de Crédito no puede ser anterior a la de la factura original."
                );
            }

            // ── Validación cuantitativa obligatoria (§18.4bis) ──
            if (document.TotalAmount <= 0)
            {
                await _uow.RollbackAsync(ct);
                // SC-016
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "El monto total de la Nota de Crédito no es verificable."
                );
            }

            var expectedAmount = purchaseReturn.GrandTotal;
            var difference = Math.Abs(document.TotalAmount - expectedAmount);
            if (difference > Tolerance)
            {
                await _uow.RollbackAsync(ct);
                if (document.TotalAmount < expectedAmount)
                    // SC-017
                    return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                        $"El monto de la Nota de Crédito ({document.TotalAmount:F2}) es inferior al esperado ({expectedAmount:F2}) fuera de la tolerancia permitida."
                    );
                // SC-018
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    $"El monto de la Nota de Crédito ({document.TotalAmount:F2}) es superior al esperado ({expectedAmount:F2}) fuera de la tolerancia permitida."
                );
            }

            var hash = ComputeLinkPayloadHash(cmd, purchaseReturn.Id, document.Id);

            try
            {
                purchaseReturn.LinkSupplierCreditNote(document.Id, uid, cmd.ClientRequestId, hash);
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                // SC-009 (dominio) u otro guard
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(ex.Message);
            }

            if (isNewDocument)
                await _receptionRepo.AddAsync(document, ct);

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "La devolución fue modificada concurrentemente. Intente nuevamente."
                );
            }
            catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
            {
                await _uow.RollbackAsync(ct);
                if (info.ConstraintName == "uq_purchase_reception_documents_tenant_access_key")
                    // SC-007
                    return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                        "Ya existe una Nota de Crédito registrada con esta clave de acceso."
                    );
                if (
                    info.ConstraintName
                    == "uq_purchase_returns_tenant_supplier_credit_note_document_id"
                )
                    // SC-012
                    return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                        "Esta Nota de Crédito ya está vinculada a otra devolución."
                    );
                // PR-012 (colisión de idempotencia por otra causa)
                return Result<SupplierCreditNoteLinkDto>.ValidationFailure(
                    "Ya existe una solicitud con este identificador de idempotencia."
                );
            }

            await _uow.CommitAsync(ct);
            return Result<SupplierCreditNoteLinkDto>.Success(Map(purchaseReturn, document));
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<SupplierCreditNoteLinkDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    private static SupplierCreditNoteLinkDto Map(
        Domain.Modules.Purchases.Entities.PurchaseReturn purchaseReturn,
        PurchaseReceptionDocument document
    ) =>
        new(
            purchaseReturn.Id,
            purchaseReturn.FiscalStatus.ToString(),
            document.Id,
            document.AccessKey,
            document.InvoiceNumber,
            document.TotalAmount,
            document.CurrencyCode
        );

    /// <summary>Huella determinista (§16.2, diseño línea 870): PurchaseReturnId+PurchaseReceptionDocumentId+AccessKey+InvoiceNumber+IssueDate+CurrencyCode+TotalAmount.</summary>
    public static string ComputeLinkPayloadHash(
        RegisterAndLinkSupplierCreditNoteCommand cmd,
        Guid purchaseReturnId,
        Guid? purchaseReceptionDocumentId
    )
    {
        var canonical = string.Join(
            "",
            "RegisterAndLinkSupplierCreditNote",
            purchaseReturnId.ToString("D"),
            purchaseReceptionDocumentId?.ToString("D") ?? "",
            cmd.AccessKey.Trim(),
            cmd.InvoiceNumber.Trim(),
            cmd.IssueDate.ToString("yyyy-MM-dd"),
            cmd.CurrencyCode.Trim().ToUpperInvariant(),
            cmd.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
