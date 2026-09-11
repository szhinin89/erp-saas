using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.Mapping;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;

public sealed class ImportPurchaseReceptionHandler
    : IRequestHandler<ImportPurchaseReceptionCommand, Result<PurchaseReceptionImportResultDto>>
{
    private readonly IPurchaseReceptionParser _parser;
    private readonly IPurchaseReceptionVerifier _verifier;
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IPurchaseCreditNoteRepository _creditNoteRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public ImportPurchaseReceptionHandler(
        IPurchaseReceptionParser parser,
        IPurchaseReceptionVerifier verifier,
        IPurchaseReceptionDocumentRepository documentRepo,
        IPurchaseCreditNoteRepository creditNoteRepo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _parser = parser;
        _verifier = verifier;
        _documentRepo = documentRepo;
        _creditNoteRepo = creditNoteRepo;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<PurchaseReceptionImportResultDto>> Handle(
        ImportPurchaseReceptionCommand request,
        CancellationToken cancellationToken
    )
    {
        var parseResult = await _parser.ParseAsync(request.File.Content, cancellationToken);

        var verifiedItems = await _verifier.VerifyAsync(parseResult.Records, cancellationToken);

        var itemDtos = new List<PurchaseReceptionItemDto>(verifiedItems.Count);

        var hasChanges = false;

        // Documentos agregados en este batch pero todavía no guardados
        // evita duplicar por AccessKey dentro del mismo archivo.
        var pendingByAccessKey = new Dictionary<string, PurchaseReceptionDocument>();

        foreach (var item in verifiedItems)
        {
            PurchaseReceptionDocument? document;

            if (!pendingByAccessKey.TryGetValue(item.Record.AccessKey, out document))
            {
                document = await _documentRepo.GetByAccessKeyAsync(
                    _tenant.TenantId,
                    item.Record.AccessKey,
                    cancellationToken
                );
            }

            if (document is null)
            {
                document = PurchaseReceptionDocument.Create(
                    _tenant.TenantId,
                    _company.CompanyId,
                    _branch.BranchId,
                    item.Record.SourceDocType,
                    item.Record.SupplierRuc,
                    item.Record.SupplierName,
                    item.SupplierId,
                    item.Record.AccessKey,
                    item.Record.InvoiceNumber,
                    item.Record.IssueDate,
                    item.Record.AuthorizationDate,
                    item.Record.Subtotal,
                    item.Record.VatAmount,
                    item.Record.Total,
                    _user.UserId,
                    item.PurchaseId,
                    modifiedDocumentNumber: item.Record.ModifiedDocumentNumber
                );

                await _documentRepo.AddAsync(document, cancellationToken);

                pendingByAccessKey[item.Record.AccessKey] = document;

                hasChanges = true;
            }
            else
            {
                // Documento existente creado anteriormente sin proveedor.
                // Si ahora el proveedor ya fue resuelto, completamos SupplierId.
                if (document.SupplierId is null && item.SupplierId.HasValue)
                {
                    document.AssignSupplier(item.SupplierId.Value, _user.UserId);

                    hasChanges = true;
                }
            }

            // PURCHASE-CREDIT-NOTE-RECEPTION-IDEMPOTENCY-UI-01 — solo aplica a notas de crédito;
            // GetIdByReceptionDocumentIdAsync ya excluye Cancelled (a lo sumo una NC ACTIVA por
            // receptionDocumentId, ver PURCHASE-RECEPTION-CREDIT-NOTE-CANCELLED-REPROCESS-01).
            Guid? creditNoteId = null;
            Guid? cancelledCreditNoteId = null;
            if (item.Record.SourceDocType == PurchaseReceptionSourceDocType.CreditNote)
            {
                creditNoteId = await _creditNoteRepo.GetIdByReceptionDocumentIdAsync(
                    _tenant.TenantId,
                    document.Id,
                    cancellationToken
                );
                // Historial ("Ver NC anulada") — solo tiene sentido consultarlo cuando no hay una
                // activa bloqueando el reintento; con una activa, la UI ya muestra "NC ya procesada".
                if (creditNoteId is null)
                    cancelledCreditNoteId = await _creditNoteRepo.GetLatestCancelledIdByReceptionDocumentIdAsync(
                        _tenant.TenantId,
                        document.Id,
                        cancellationToken
                    );
            }

            itemDtos.Add(
                PurchaseReceptionMapper.ToDto(
                    item,
                    document,
                    creditNoteExists: creditNoteId is not null,
                    creditNoteId: creditNoteId,
                    cancelledCreditNoteId: cancelledCreditNoteId
                )
            );
        }

        if (hasChanges)
        {
            await _documentRepo.SaveChangesAsync(cancellationToken);
        }

        var dto = new PurchaseReceptionImportResultDto(
            itemDtos,
            parseResult.Records.Count,
            parseResult.Errors.Count,
            parseResult.SkippedUnsupportedCount
        );
        return Result<PurchaseReceptionImportResultDto>.Success(dto);
    }
}
