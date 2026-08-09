using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// FLOW-READY-02C-R1.1 — vincula una <c>PurchaseCreditNote</c> (tipo <c>Return</c>) al
/// <c>PurchaseReturn</c> que realmente la aplica. Puramente documental/de trazabilidad: nunca mueve
/// inventario, CxP ni contabilidad — eso ya ocurrió (o va a ocurrir) exclusivamente a través de
/// <c>PurchaseReturn.Authorize()</c>. No reimplementa ni toca la lógica de <c>PurchaseReturn</c>,
/// solo consulta <see cref="IPurchaseReturnRepository.GetByIdAsync"/> (ya existente) para validar
/// consistencia (misma factura/proveedor/empresa/sucursal).
/// </summary>
public sealed record LinkPurchaseCreditNoteToReturnCommand(
    Guid PurchaseCreditNoteId,
    Guid PurchaseReturnId,
    Guid ClientRequestId
) : IRequest<Result<PurchaseCreditNoteDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class LinkPurchaseCreditNoteToReturnValidator
    : AbstractValidator<LinkPurchaseCreditNoteToReturnCommand>
{
    public LinkPurchaseCreditNoteToReturnValidator()
    {
        RuleFor(x => x.PurchaseCreditNoteId).NotEmpty();
        RuleFor(x => x.PurchaseReturnId).NotEmpty();
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class LinkPurchaseCreditNoteToReturnHandler
    : IRequestHandler<LinkPurchaseCreditNoteToReturnCommand, Result<PurchaseCreditNoteDto>>
{
    private readonly IPurchaseCreditNoteRepository _creditNoteRepo;
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public LinkPurchaseCreditNoteToReturnHandler(
        IPurchaseCreditNoteRepository creditNoteRepo,
        IPurchaseReturnRepository returnRepo,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _creditNoteRepo = creditNoteRepo;
        _returnRepo = returnRepo;
        _dbEx = dbEx;
        _t = t;
        _u = u;
    }

    public async Task<Result<PurchaseCreditNoteDto>> Handle(
        LinkPurchaseCreditNoteToReturnCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;

        var creditNote = await _creditNoteRepo.GetByIdAsync(tid, cmd.PurchaseCreditNoteId, ct);
        if (creditNote is null)
            return Result<PurchaseCreditNoteDto>.NotFound("Nota de crédito no encontrada.");

        if (creditNote.ApplicationType != PurchaseCreditNoteApplicationType.Return)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Solo una nota de crédito de tipo Devolución se puede vincular a una devolución de compra."
            );

        // Idempotencia: un reintento con la misma devolución ya vinculada retorna el snapshot actual
        // sin reejecutar nada (LinkPurchaseReturn es set-once en el dominio).
        if (creditNote.LinkedPurchaseReturnId == cmd.PurchaseReturnId)
            return Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(creditNote));

        if (creditNote.LinkedPurchaseReturnId is not null)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Esta nota de crédito ya está vinculada a otra devolución de compra."
            );

        if (creditNote.Status != PurchaseCreditNoteStatus.Draft)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Esta nota de crédito ya no está en borrador, por lo que no se puede vincular."
            );

        var purchaseReturn = await _returnRepo.GetByIdAsync(tid, cmd.PurchaseReturnId, ct);
        if (purchaseReturn is null)
            return Result<PurchaseCreditNoteDto>.NotFound("Devolución de compra no encontrada.");

        if (purchaseReturn.CompanyId != creditNote.CompanyId)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "La devolución de compra no pertenece a la misma empresa que la nota de crédito."
            );
        if (purchaseReturn.BranchId != creditNote.BranchId)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "La devolución de compra no pertenece a la misma sucursal que la nota de crédito."
            );
        if (purchaseReturn.PurchaseInvoiceId != creditNote.PurchaseInvoiceId)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "La devolución de compra no corresponde a la misma factura afectada que la nota de crédito."
            );
        if (purchaseReturn.SupplierId != creditNote.SupplierId)
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "La devolución de compra no corresponde al mismo proveedor que la nota de crédito."
            );

        if (
            await _creditNoteRepo.ExistsByLinkedPurchaseReturnIdAsync(
                tid,
                cmd.PurchaseReturnId,
                creditNote.Id,
                ct
            )
        )
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                "Esta devolución de compra ya está vinculada a otra nota de crédito."
            );

        try
        {
            creditNote.LinkPurchaseReturn(cmd.PurchaseReturnId, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PurchaseCreditNoteDto>.ValidationFailure(ex.Message);
        }

        try
        {
            await _creditNoteRepo.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
        {
            var mapped = CreateDraftPurchaseCreditNoteHandler.MapUniqueViolation(info.ConstraintName);
            return Result<PurchaseCreditNoteDto>.ValidationFailure(
                mapped ?? "Conflicto de datos duplicados al vincular la devolución de compra."
            );
        }

        return Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(creditNote));
    }
}
