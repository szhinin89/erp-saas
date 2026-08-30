using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.DocTypes.Services;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Expenses.UseCases.Documents;

public sealed record ConfirmExpenseDocumentCommand(Guid Id)
    : IRequest<Result<ExpenseDocumentDetailDto>>,
        IBranchScopedRequest;

public sealed class ConfirmExpenseDocumentValidator : AbstractValidator<ConfirmExpenseDocumentCommand>
{
    public ConfirmExpenseDocumentValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ConfirmExpenseDocumentHandler
    : IRequestHandler<ConfirmExpenseDocumentCommand, Result<ExpenseDocumentDetailDto>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly IExpenseCategoryRepository _categories;
    private readonly IAccountRepository _accounts;
    private readonly IAccountsPayableService _payables;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;
    private readonly ILogger<ConfirmExpenseDocumentHandler> _logger;

    public ConfirmExpenseDocumentHandler(
        IExpenseDocumentRepository repo,
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        IAccountsPayableService payables,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user,
        ILogger<ConfirmExpenseDocumentHandler> logger
    )
    {
        _repo = repo;
        _categories = categories;
        _accounts = accounts;
        _payables = payables;
        _tenant = tenant;
        _company = company;
        _logger = logger;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<ExpenseDocumentDetailDto>> Handle(
        ConfirmExpenseDocumentCommand cmd,
        CancellationToken ct
    )
    {
        var document = await _repo.GetByIdAsync(_tenant.TenantId, cmd.Id, ct);
        if (document is null || document.BranchId != _branch.BranchId)
            return Result<ExpenseDocumentDetailDto>.NotFound("Gasto no encontrado.");
        if (document.Status != ExpenseStatus.Draft)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "Solo se pueden confirmar gastos en estado borrador."
            );
        if (document.SupplierId == Guid.Empty)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "El gasto debe tener un proveedor para confirmarse."
            );
        if (document.BranchId == Guid.Empty)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "El gasto debe tener una sucursal valida para confirmarse."
            );
        if (document.Lines.Count == 0)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "El gasto debe tener al menos una linea para confirmarse."
            );

        var snapshots = new Dictionary<Guid, (Guid AccountId, string? Code, string? Name)>();
        foreach (var line in document.Lines)
        {
            var resolution = await ExpenseConfirmRules.ResolveLineAccountAsync(
                _categories,
                _accounts,
                _tenant.TenantId,
                _company.CompanyId,
                line,
                ct
            );
            if (resolution.Error is not null)
                return resolution.Error.ToResult<ExpenseDocumentDetailDto>();

            snapshots[line.Id] = (
                resolution.Account!.Id,
                resolution.Account.Code.Value,
                resolution.Account.Name
            );
        }

        try
        {
            document.Confirm(snapshots, _user.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }

        try
        {
            // EXPENSES-CONFIRM-07: a diferencia de Purchases/Sales, el posting de Gastos es
            // estricto — ExpenseDocumentConfirmedPostingTranslator lanza ExpensePostingFailedException
            // (en vez de solo loguear un warning) si IPostingEngine.PostAsync falla. La excepción se
            // propaga desde el Publish() interno de ErpDbContext.SaveChangesAsync, que hace rollback
            // completo de la transacción ANTES de este catch — el documento queda en Draft en BD,
            // nada de lo mutado en memoria (Confirm() de arriba) llegó a persistirse.
            await _repo.SaveChangesAsync(ct);
        }
        catch (ExpensePostingFailedException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message, ex.Code);
        }

        // PAYABLES-GENERIC-FOUNDATION-09: "al confirmar gasto, después de posting contable
        // exitoso, crear AccountsPayable" — el posting ya se confirmó y persistió arriba (si
        // hubiera fallado, ya habríamos retornado). A diferencia del posting, un fallo aquí NO
        // debe revertir la confirmación ya persistida (el gasto ya tiene asiento contable real) —
        // se registra para seguimiento manual, mismo criterio que Purchases usa para gaps de
        // configuración que no bloquean el documento de origen. CreateFromOriginAsync es
        // idempotente, así que un reintento manual posterior es seguro.
        try
        {
            await _payables.CreateFromOriginAsync(
                new CreateAccountsPayableFromOriginRequest(
                    _tenant.TenantId,
                    _company.CompanyId,
                    document.BranchId,
                    document.SupplierId,
                    AccountsPayableOriginType.ExpenseDocument,
                    document.Id,
                    document.DocumentType,
                    document.DocumentNumber,
                    document.IssueDate,
                    document.AccountingDate,
                    new[]
                    {
                        new AccountsPayableInstallmentInput(
                            1,
                            document.DueDate ?? document.AccountingDate,
                            document.GrandTotal
                        ),
                    }
                ),
                _user.UserId,
                ct
            );
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "No se pudo crear la cuenta por pagar para el gasto {ExpenseDocumentId} ({DocumentNumber}) tras confirmar.",
                document.Id,
                document.DocumentNumber
            );
        }

        return Result<ExpenseDocumentDetailDto>.Success(ExpenseDocumentMapper.ToDetail(document));
    }
}

/// <summary>
/// EXPENSES-WORKFLOW-INTEGRATION-01: crea un gasto directamente en estado Confirmado (sin pasar
/// por un borrador previo) — mismos datos de entrada que <see cref="CreateExpenseDraftCommand"/>
/// (comparte <see cref="ExpenseDraftHeaderRules{T}"/> vía <see cref="IExpenseDraftInput"/> y la
/// resolución de proveedor/condición de pago/líneas de <see cref="ExpenseDraftRules"/>), pero
/// además ejecuta la misma confirmación/posting/AP que <see cref="ConfirmExpenseDocumentCommand"/>
/// en la misma operación. Bloqueado por <c>IDocWorkflowPolicyService.ValidateCreateConfirmedAsync</c>
/// cuando la política de la empresa exige borrador (<see cref="DraftMode.Required"/>) para GASDOC.
/// </summary>
public sealed record CreateConfirmedExpenseCommand(
    Guid SupplierId,
    DateOnly IssueDate,
    DateOnly AccountingDate,
    string DocumentType,
    string DocumentNumber,
    Guid? PaymentTermId,
    DateOnly? DueDate,
    IReadOnlyList<ExpenseDraftLineRequest> Lines,
    string? AuthorizationNumber = null,
    DateTime? AuthorizationDate = null,
    string? Notes = null
) : IRequest<Result<ExpenseDocumentDetailDto>>, IBranchScopedRequest, IExpenseDraftInput;

public sealed class CreateConfirmedExpenseValidator : AbstractValidator<CreateConfirmedExpenseCommand>
{
    public CreateConfirmedExpenseValidator() =>
        Include(new ExpenseDraftHeaderRules<CreateConfirmedExpenseCommand>());
}

public sealed class CreateConfirmedExpenseHandler
    : IRequestHandler<CreateConfirmedExpenseCommand, Result<ExpenseDocumentDetailDto>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly IExpenseCategoryRepository _categories;
    private readonly IAccountRepository _accounts;
    private readonly IBusinessPartnerRepository _businessPartners;
    private readonly IBusinessPartnerRoleRepository _roles;
    private readonly IPaymentTermRepository _paymentTerms;
    private readonly ISriTaxResolver _tax;
    private readonly IAccountsPayableService _payables;
    private readonly IDocWorkflowPolicyService _workflowPolicy;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;
    private readonly ILogger<CreateConfirmedExpenseHandler> _logger;

    public CreateConfirmedExpenseHandler(
        IExpenseDocumentRepository repo,
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        IBusinessPartnerRepository businessPartners,
        IBusinessPartnerRoleRepository roles,
        IPaymentTermRepository paymentTerms,
        ISriTaxResolver tax,
        IAccountsPayableService payables,
        IDocWorkflowPolicyService workflowPolicy,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user,
        ILogger<CreateConfirmedExpenseHandler> logger
    )
    {
        _repo = repo;
        _categories = categories;
        _accounts = accounts;
        _businessPartners = businessPartners;
        _roles = roles;
        _paymentTerms = paymentTerms;
        _tax = tax;
        _payables = payables;
        _workflowPolicy = workflowPolicy;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
        _logger = logger;
    }

    public async Task<Result<ExpenseDocumentDetailDto>> Handle(
        CreateConfirmedExpenseCommand cmd,
        CancellationToken ct
    )
    {
        try
        {
            await _workflowPolicy.ValidateCreateConfirmedAsync(
                _company.CompanyId,
                DocTypeCodes.ExpenseDocument,
                ct
            );
        }
        catch (DocWorkflowPolicyViolationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                ExpenseWorkflowPolicyMessages.Translate(ex)
            );
        }

        var supplier = await ExpenseDraftRules.ResolveSupplierAsync(
            _businessPartners,
            _roles,
            _tenant.TenantId,
            cmd.SupplierId,
            ct
        );
        if (supplier.Error is not null)
            return supplier.Error.ToResult<ExpenseDocumentDetailDto>();

        var paymentTerm = await ExpenseDraftRules.ResolvePaymentTermAsync(
            _paymentTerms,
            _tenant.TenantId,
            cmd.PaymentTermId,
            supplier.Role,
            ct
        );
        if (paymentTerm.Error is not null)
            return paymentTerm.Error.ToResult<ExpenseDocumentDetailDto>();

        var duplicate = await _repo.GetBySupplierAndDocumentNumberAsync(
            _tenant.TenantId,
            cmd.SupplierId,
            cmd.DocumentType.Trim(),
            cmd.DocumentNumber.Trim(),
            ct
        );
        if (duplicate is not null)
            return Result<ExpenseDocumentDetailDto>.Conflict(
                "Ya existe un gasto registrado para este proveedor, tipo y numero de documento."
            );

        var dueDate = ExpenseDraftRules.ResolveDueDate(
            cmd.IssueDate,
            cmd.DueDate,
            paymentTerm.PaymentTerm!
        );
        if (dueDate.Error is not null)
            return dueDate.Error.ToResult<ExpenseDocumentDetailDto>();

        ExpenseDocument document;
        try
        {
            document = ExpenseDocument.CreateDraft(
                _tenant.TenantId,
                _company.CompanyId,
                _branch.BranchId,
                cmd.SupplierId,
                supplier.BusinessPartner!.Name.LegalName,
                supplier.BusinessPartner.Identification.Number,
                cmd.IssueDate,
                cmd.AccountingDate,
                cmd.DocumentType,
                cmd.DocumentNumber,
                paymentTerm.PaymentTerm!.Id,
                paymentTerm.PaymentTerm.Name,
                paymentTerm.PaymentTerm.Installments,
                paymentTerm.PaymentTerm.DaysBetweenInstallments,
                _user.UserId,
                cmd.AuthorizationNumber,
                cmd.AuthorizationDate,
                dueDate.Value,
                cmd.Notes
            );

            var lines = await ExpenseDraftRules.BuildLinesAsync(
                _categories,
                _accounts,
                _tax,
                _tenant.TenantId,
                _company.CompanyId,
                document.Id,
                cmd.Lines,
                ct
            );
            if (lines.Error is not null)
                return lines.Error.ToResult<ExpenseDocumentDetailDto>();

            document.ReplaceLines(lines.Lines!, _user.UserId);

            // Las líneas se acaban de construir con datos vigentes (categoría/cuenta) — a
            // diferencia de ConfirmExpenseDocumentHandler (que confirma un borrador que pudo
            // quedar desactualizado), no hace falta re-resolver contra ExpenseConfirmRules: el
            // snapshot que cada línea ya trae de ExpenseDraftRules.BuildLinesAsync es el mismo dato
            // fresco que Confirm() espera.
            var snapshots = document.Lines.ToDictionary(
                l => l.Id,
                l => (
                    l.SnapshotAccountingAccountId,
                    (string?)l.SnapshotAccountingAccountCode,
                    (string?)l.SnapshotAccountingAccountName
                )
            );
            document.Confirm(snapshots, _user.UserId);

            await _repo.AddAsync(document, ct);
        }
        catch (ArgumentException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }

        try
        {
            // Mismo criterio que ConfirmExpenseDocumentHandler (EXPENSES-CONFIRM-07): posting de
            // Gastos es estricto — ExpensePostingFailedException aborta la transacción completa,
            // nada de lo construido arriba llega a persistirse.
            await _repo.SaveChangesAsync(ct);
        }
        catch (ExpensePostingFailedException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message, ex.Code);
        }

        // PAYABLES-GENERIC-FOUNDATION-09, mismo criterio que ConfirmExpenseDocumentHandler: el
        // posting ya se confirmó y persistió arriba; un fallo aquí no revierte la confirmación ya
        // persistida — se registra para seguimiento manual. CreateFromOriginAsync es idempotente.
        try
        {
            await _payables.CreateFromOriginAsync(
                new CreateAccountsPayableFromOriginRequest(
                    _tenant.TenantId,
                    _company.CompanyId,
                    document.BranchId,
                    document.SupplierId,
                    AccountsPayableOriginType.ExpenseDocument,
                    document.Id,
                    document.DocumentType,
                    document.DocumentNumber,
                    document.IssueDate,
                    document.AccountingDate,
                    new[]
                    {
                        new AccountsPayableInstallmentInput(
                            1,
                            document.DueDate ?? document.AccountingDate,
                            document.GrandTotal
                        ),
                    }
                ),
                _user.UserId,
                ct
            );
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "No se pudo crear la cuenta por pagar para el gasto {ExpenseDocumentId} ({DocumentNumber}) tras confirmar.",
                document.Id,
                document.DocumentNumber
            );
        }

        return Result<ExpenseDocumentDetailDto>.Success(ExpenseDocumentMapper.ToDetail(document));
    }
}

file sealed record ExpenseConfirmError(string Message, string Code)
{
    public Result<T> ToResult<T>() =>
        Code == ApiResponseCodes.Common.NotFound
            ? Result<T>.NotFound(Message)
            : Result<T>.ValidationFailure(Message);
}

file sealed record LineAccountResolution(Account? Account, ExpenseConfirmError? Error);

/// <summary>
/// EXPENSES-CONFIRM-07 — re-valida, al confirmar, exactamente las mismas reglas que
/// <c>ExpenseDraftRules.BuildLinesAsync</c> exige al crear/editar el borrador (subcategoría activa,
/// nivel Subcategory, misma empresa, cuenta contable activa/postable/tipo Gasto/misma empresa).
/// Se revalida aquí porque ambas pudieron cambiar entre la creación del borrador y la confirmación
/// (la subcategoría puede desactivarse o cambiar de cuenta; la cuenta puede desactivarse o perder
/// AllowsPosting) — el snapshot tomado al crear la línea puede estar obsoleto.
/// </summary>
file static class ExpenseConfirmRules
{
    public static async Task<LineAccountResolution> ResolveLineAccountAsync(
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        Guid tenantId,
        Guid companyId,
        ExpenseLine line,
        CancellationToken ct
    )
    {
        if (line.ExpenseSubcategoryId == Guid.Empty)
            return new LineAccountResolution(
                null,
                Validation("Cada linea debe tener una subcategoria de gasto.")
            );

        var category = await categories.GetByIdAsync(tenantId, line.ExpenseSubcategoryId, ct);
        if (category is null || category.CompanyId != companyId)
            return new LineAccountResolution(
                null,
                new ExpenseConfirmError(
                    "La subcategoria de gasto no existe.",
                    ApiResponseCodes.Common.NotFound
                )
            );
        if (!category.IsActive)
            return new LineAccountResolution(
                null,
                Validation($"La subcategoria '{category.Name}' esta inactiva.")
            );
        if (category.Level != ExpenseCategoryNodeLevel.Subcategory)
            return new LineAccountResolution(
                null,
                Validation("Cada linea debe apuntar a una subcategoria de gasto.")
            );
        if (!category.AccountingAccountId.HasValue)
            return new LineAccountResolution(
                null,
                Validation(
                    $"La subcategoria '{category.Name}' no tiene cuenta contable configurada."
                )
            );

        var account = await accounts.GetByIdAsync(
            tenantId,
            companyId,
            category.AccountingAccountId.Value,
            ct
        );
        if (account is null)
            return new LineAccountResolution(
                null,
                new ExpenseConfirmError(
                    $"La cuenta contable de la subcategoria '{category.Name}' no existe.",
                    ApiResponseCodes.Common.NotFound
                )
            );
        if (!account.IsActive)
            return new LineAccountResolution(
                null,
                Validation($"La cuenta contable de la subcategoria '{category.Name}' esta inactiva.")
            );
        if (!account.AllowsPosting)
            return new LineAccountResolution(
                null,
                Validation(
                    $"La cuenta contable de la subcategoria '{category.Name}' no permite contabilizacion."
                )
            );
        if (account.AccountType != AccountType.Expense)
            return new LineAccountResolution(
                null,
                Validation($"La cuenta contable de la subcategoria '{category.Name}' debe ser de tipo gasto.")
            );

        return new LineAccountResolution(account, null);
    }

    private static ExpenseConfirmError Validation(string message) =>
        new(message, ApiResponseCodes.Common.ValidationError);
}
