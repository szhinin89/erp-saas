using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.Mapping;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
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
    private readonly ICompanyClock _companyClock;
    private readonly ICompanyRepository _companyRepo;
    private readonly IDatabaseExceptionTranslator _dbEx;

    /// <summary>Índice tenant + clave de acceso (H2): única violación que se traduce aquí.</summary>
    public const string AccessKeyUniqueConstraint =
        "uq_purchase_reception_documents_tenant_access_key";

    public const string AccessKeyConflictMessage =
        "No se importó ningún documento: uno o más comprobantes del archivo ya están registrados en el sistema y no pertenecen a la empresa activa o se registraron al mismo tiempo. Vuelva a importar el archivo; si persiste, revise el origen del TXT.";

    public ImportPurchaseReceptionHandler(
        IPurchaseReceptionParser parser,
        IPurchaseReceptionVerifier verifier,
        IPurchaseReceptionDocumentRepository documentRepo,
        IPurchaseCreditNoteRepository creditNoteRepo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user,
        ICompanyClock companyClock,
        ICompanyRepository companyRepo,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _companyRepo = companyRepo;
        _dbEx = dbEx;
        _companyClock = companyClock;
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

        // PURCHASES-RECEPTION-RECEIVER-RUC-01 (H1): todas las filas deben pertenecer a la empresa
        // operativa antes de consultar o escribir nada; una sola fila ajena rechaza el archivo.
        if (parseResult.Records.Count > 0)
        {
            var receiverError = await ValidateReceiverAsync(parseResult.Records, cancellationToken);
            if (receiverError is not null)
                return Result<PurchaseReceptionImportResultDto>.ValidationFailure(receiverError);
        }

        var verifiedItems = await _verifier.VerifyAsync(parseResult.Records, cancellationToken);

        var itemDtos = new List<PurchaseReceptionItemDto>(verifiedItems.Count);

        var hasChanges = false;

        // Documentos agregados en este batch pero todavía no guardados
        // evita duplicar por AccessKey dentro del mismo archivo.
        var pendingByAccessKey = new Dictionary<string, PurchaseReceptionDocument>();

        foreach (var item in verifiedItems)
        {
            PurchaseReceptionDocument? document;

            // ZH-TEMPORAL-CONTRACT-02: FECHA_AUTORIZACION (hora Ecuador/empresa) → UTC real, una vez.
            var authorizationDateUtc = await _companyClock.CompanyLocalToUtcAsync(
                _company.CompanyId,
                _tenant.TenantId,
                item.Record.AuthorizationLocalDateTime,
                cancellationToken
            );

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
                    authorizationDateUtc,
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

            // FASE 5 PURCHASE-UX — mismo armado de fila que el refresco de estados.
            itemDtos.Add(
                await PurchaseReceptionItemAssembler.BuildAsync(
                    _creditNoteRepo,
                    _tenant.TenantId,
                    item,
                    document,
                    authorizationDateUtc,
                    cancellationToken
                )
            );
        }

        if (hasChanges)
        {
            // PURCHASES-RECEPTION-CROSS-COMPANY-KEY-01 (H2): la clave de acceso es única por tenant
            // pero las búsquedas se acotan a la empresa. Un único SaveChanges (transacción implícita)
            // hace que el conflicto revierta el archivo completo; la respuesta no revela la empresa.
            try
            {
                await _documentRepo.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
                when (_dbEx.TryGetUniqueViolation(ex, out var info)
                    && info.ConstraintName == AccessKeyUniqueConstraint
                )
            {
                return Result<PurchaseReceptionImportResultDto>.Conflict(AccessKeyConflictMessage);
            }
        }

        var dto = new PurchaseReceptionImportResultDto(
            itemDtos,
            parseResult.Records.Count,
            parseResult.Errors.Count,
            parseResult.SkippedUnsupportedCount
        );
        return Result<PurchaseReceptionImportResultDto>.Success(dto);
    }

    private async Task<string?> ValidateReceiverAsync(
        IReadOnlyList<PurchaseReceptionRecord> records,
        CancellationToken cancellationToken
    )
    {
        var company = await _companyRepo.GetByIdAsync(_company.CompanyId, cancellationToken);
        if (
            company is null
            || company.TenantId != _tenant.TenantId
            || !PurchaseReceptionReceiverRule.IsCompanyRucUsable(
                company.TaxIdentificationNumber,
                company.IsTemporaryTaxIdentification
            )
        )
            return "No se importó ningún documento: la empresa activa no tiene un RUC definitivo válido para verificar el receptor de los comprobantes.";

        return PurchaseReceptionReceiverRule.FindRejection(company.TaxIdentificationNumber, records);
    }
}
