using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreateExpenseDraftFromReception;

/// <summary>
/// EXPENSES-FROM-RECEPTION-01 — mismo criterio de elegibilidad que
/// <c>CreatePurchaseReceptionDraftHandler</c> (Verified + XML), pero exclusivo de facturas: una
/// nota de crédito nunca genera un borrador de Gasto. El bloqueo cruzado Compra↔Gasto se verifica
/// dos veces por diseño (aquí, de solo lectura, para no ofrecer un botón que fallaría; y de nuevo
/// en <c>CreateExpenseDraftHandler</c> al guardar — "Backend valida todo, la UI no es suficiente").
/// </summary>
public sealed class CreateExpenseDraftFromReceptionHandler
    : IRequestHandler<CreateExpenseDraftFromReceptionQuery, Result<ExpenseReceptionDraftDto>>
{
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IPurchaseInvoiceRepository _purchaseRepo;
    private readonly IExpenseDocumentRepository _expenseRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roles;
    private readonly ICurrentTenant _tenant;

    public CreateExpenseDraftFromReceptionHandler(
        IPurchaseReceptionDocumentRepository documentRepo,
        IPurchaseInvoiceRepository purchaseRepo,
        IExpenseDocumentRepository expenseRepo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roles,
        ICurrentTenant tenant
    )
    {
        _documentRepo = documentRepo;
        _purchaseRepo = purchaseRepo;
        _expenseRepo = expenseRepo;
        _bpRepo = bpRepo;
        _roles = roles;
        _tenant = tenant;
    }

    public async Task<Result<ExpenseReceptionDraftDto>> Handle(
        CreateExpenseDraftFromReceptionQuery request,
        CancellationToken cancellationToken
    )
    {
        var document = await _documentRepo.GetByIdAsync(
            _tenant.TenantId,
            request.PurchaseReceptionDocumentId,
            cancellationToken
        );
        if (document is null)
            return Result<ExpenseReceptionDraftDto>.NotFound("El documento de recepción no existe.");

        if (document.SourceDocType != PurchaseReceptionSourceDocType.Invoice)
            return Result<ExpenseReceptionDraftDto>.ValidationFailure(
                "Solo una factura puede generar un borrador de gasto. Las notas de crédito no aplican."
            );

        if (
            document.Status != PurchaseReceptionDocumentStatus.Verified
            || string.IsNullOrWhiteSpace(document.XmlContent)
        )
        {
            return Result<ExpenseReceptionDraftDto>.ValidationFailure(
                "Solo se puede generar un borrador de gasto desde documentos con XML autorizado (estado Verificado)."
            );
        }

        var existingPurchase = await _purchaseRepo.GetByAccessKeyAsync(
            _tenant.TenantId,
            document.AccessKey,
            cancellationToken
        );
        if (existingPurchase is not null)
            return Result<ExpenseReceptionDraftDto>.Conflict(
                "Ya existe una compra registrada con esta clave de acceso SRI."
            );

        var existingExpense = await _expenseRepo.ExistsByAccessKeyAsync(
            _tenant.TenantId,
            document.AccessKey,
            cancellationToken
        );
        if (existingExpense || await _expenseRepo.ExistsByReceptionDocumentIdAsync(
            _tenant.TenantId, document.Id, cancellationToken))
            return Result<ExpenseReceptionDraftDto>.Conflict(
                "Ya existe un gasto registrado con esta clave de acceso SRI."
            );

        if (document.SupplierId is not { } supplierId)
            return Result<ExpenseReceptionDraftDto>.ValidationFailure(
                "Cree primero el proveedor antes de generar el borrador de gasto."
            );

        var supplier = await _bpRepo.GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null || supplier.TenantId != _tenant.TenantId)
            return Result<ExpenseReceptionDraftDto>.ValidationFailure(
                "Cree primero el proveedor antes de generar el borrador de gasto."
            );
        if (!supplier.IsActive)
            return Result<ExpenseReceptionDraftDto>.ValidationFailure(
                $"El proveedor '{supplier.Name.LegalName}' se encuentra inactivo."
            );

        var role = await _roles.GetByTypeAsync(supplierId, ERP.Domain.MasterData.Enums.RoleType.Supplier, cancellationToken);
        if (role is null || !role.IsActive || role.TenantId != _tenant.TenantId)
            return Result<ExpenseReceptionDraftDto>.ValidationFailure("El tercero no tiene un rol de proveedor activo.");

        if (document.DocTypeCode != "01")
            return Result<ExpenseReceptionDraftDto>.ValidationFailure(
                "No se pudo interpretar el tipo de comprobante SRI de este documento."
            );

        var dto = new ExpenseReceptionDraftDto(
            document.Id,
            document.AccessKey,
            supplier.Id,
            supplier.Name.LegalName,
            supplier.Identification.Number,
            document.IssueDate,
            document.DocTypeCode,
            document.InvoiceNumber,
            document.AuthorizationNumber,
            document.AuthorizationDate,
            document.Subtotal,
            document.VatAmount,
            document.TotalAmount
        );

        return Result<ExpenseReceptionDraftDto>.Success(dto);
    }
}
