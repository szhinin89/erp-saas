using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.DocTypes.Services;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Expenses.UseCases.Documents;

public sealed record ListExpenseDocumentsQuery(
    string? Search = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<ExpenseDocumentListResponse>>, IBranchScopedRequest;

public sealed record GetExpenseDocumentByIdQuery(Guid Id)
    : IRequest<Result<ExpenseDocumentDetailDto>>,
        IBranchScopedRequest;

public sealed record CreateExpenseDraftCommand(
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
    string? Notes = null,
    string? TaxSupportCode = null
) : IRequest<Result<ExpenseDocumentDetailDto>>, IBranchScopedRequest, IExpenseDraftInput;

public sealed record UpdateExpenseDraftCommand(
    Guid Id,
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
    string? Notes = null,
    string? TaxSupportCode = null
) : IRequest<Result<ExpenseDocumentDetailDto>>, IBranchScopedRequest, IExpenseDraftInput;

public sealed class ListExpenseDocumentsValidator
    : AbstractValidator<ListExpenseDocumentsQuery>
{
    public ListExpenseDocumentsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status)
            .Must(s =>
                string.IsNullOrWhiteSpace(s)
                || Enum.TryParse<ExpenseStatus>(s.Trim(), ignoreCase: true, out _)
            )
            .WithMessage("El estado de gasto no es valido.");
    }
}

public sealed class GetExpenseDocumentByIdValidator
    : AbstractValidator<GetExpenseDocumentByIdQuery>
{
    public GetExpenseDocumentByIdValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class CreateExpenseDraftValidator : AbstractValidator<CreateExpenseDraftCommand>
{
    public CreateExpenseDraftValidator() => Include(new ExpenseDraftHeaderRules<CreateExpenseDraftCommand>());
}

public sealed class UpdateExpenseDraftValidator : AbstractValidator<UpdateExpenseDraftCommand>
{
    public UpdateExpenseDraftValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new ExpenseDraftHeaderRules<UpdateExpenseDraftCommand>());
    }
}

// EXPENSES-WORKFLOW-INTEGRATION-01: internal (no file-scoped) para que
// ExpenseDocumentConfirmUseCases.cs reutilice las mismas reglas de encabezado/líneas en el
// validator de CreateConfirmedExpenseCommand — mismo criterio que ExpenseDocumentMapper.
internal sealed class ExpenseDraftHeaderRules<T> : AbstractValidator<T>
    where T : IExpenseDraftInput
{
    public ExpenseDraftHeaderRules()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("El proveedor es obligatorio.");
        RuleFor(x => x.IssueDate).NotEmpty().WithMessage("La fecha de emision es obligatoria.");
        RuleFor(x => x.AccountingDate)
            .NotEmpty()
            .WithMessage("La fecha contable es obligatoria.");
        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .MaximumLength(ExpenseDocument.DocumentTypeMaxLen)
            .WithMessage("El tipo de documento es obligatorio.");
        RuleFor(x => x.DocumentNumber)
            .NotEmpty()
            .MaximumLength(ExpenseDocument.DocumentNumberMaxLen)
            .WithMessage("El numero de documento es obligatorio.");
        RuleFor(x => x.AuthorizationNumber)
            .MaximumLength(ExpenseDocument.AuthorizationNumberMaxLen);
        RuleFor(x => x.Notes).MaximumLength(ExpenseDocument.NotesMaxLen);
        RuleFor(x => x.TaxSupportCode).MaximumLength(ExpenseDocument.TaxSupportCodeMaxLen);
        RuleFor(x => x.DueDate)
            .Must((x, dueDate) => !dueDate.HasValue || dueDate.Value >= x.IssueDate)
            .WithMessage("La fecha de vencimiento no puede ser anterior a la fecha de emision.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una linea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.ExpenseSubcategoryId)
                    .NotEmpty()
                    .WithMessage("La subcategoria de gasto es obligatoria por linea.");
                line.RuleFor(l => l.Description)
                    .MaximumLength(ExpenseLine.DescriptionMaxLen)
                    .When(l => !string.IsNullOrWhiteSpace(l.Description));
                line.RuleFor(l => l.Quantity)
                    .GreaterThan(0)
                    .WithMessage("La cantidad debe ser mayor a cero.");
                line.RuleFor(l => l.UnitPrice)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("El valor unitario no puede ser negativo.");
                line.RuleFor(l => l.DiscountValue)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("El descuento no puede ser negativo.");
                line.RuleFor(l => l)
                    .Must(l => l.DiscountValue <= l.Quantity * l.UnitPrice)
                    .WithMessage("El descuento no puede superar el subtotal de la linea.");
                line.RuleFor(l => l.VatCode)
                    .NotEmpty()
                    .MaximumLength(ExpenseLine.VatCodeMaxLen)
                    .WithMessage("El codigo IVA es obligatorio por linea.");
                line.RuleFor(l => l.Notes).MaximumLength(ExpenseLine.NotesMaxLen);
            });
    }
}

public interface IExpenseDraftInput
{
    Guid SupplierId { get; }
    DateOnly IssueDate { get; }
    DateOnly AccountingDate { get; }
    string DocumentType { get; }
    string DocumentNumber { get; }
    DateOnly? DueDate { get; }
    IReadOnlyList<ExpenseDraftLineRequest> Lines { get; }
    string? AuthorizationNumber { get; }
    string? Notes { get; }
    /// <summary>
    /// RETENTIONS-SOURCE-DOCUMENT-TAX-SUPPORT-02G — override explícito del código de sustento
    /// tributario SRI para este documento. <c>null</c> deja que
    /// <see cref="ExpenseDraftRules.ResolveTaxSupportCode"/> use el default configurable del
    /// proveedor (<c>SupplierRoleConfig.DefaultTaxSupportCode</c>) — nunca un valor inventado aquí.
    /// </summary>
    string? TaxSupportCode { get; }
}

public sealed class CreateExpenseDraftHandler
    : IRequestHandler<CreateExpenseDraftCommand, Result<ExpenseDocumentDetailDto>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly IExpenseCategoryRepository _categories;
    private readonly IAccountRepository _accounts;
    private readonly IBusinessPartnerRepository _businessPartners;
    private readonly IBusinessPartnerRoleRepository _roles;
    private readonly IPaymentTermRepository _paymentTerms;
    private readonly ISriTaxResolver _tax;
    private readonly IDocumentFlowPolicyService _workflowPolicy;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public CreateExpenseDraftHandler(
        IExpenseDocumentRepository repo,
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        IBusinessPartnerRepository businessPartners,
        IBusinessPartnerRoleRepository roles,
        IPaymentTermRepository paymentTerms,
        ISriTaxResolver tax,
        IDocumentFlowPolicyService workflowPolicy,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _repo = repo;
        _categories = categories;
        _accounts = accounts;
        _businessPartners = businessPartners;
        _roles = roles;
        _paymentTerms = paymentTerms;
        _tax = tax;
        _workflowPolicy = workflowPolicy;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<ExpenseDocumentDetailDto>> Handle(
        CreateExpenseDraftCommand cmd,
        CancellationToken ct
    )
    {
        try
        {
            await _workflowPolicy.EnsureDraftCreationAllowedAsync(
                _company.CompanyId,
                DocTypeCodes.ExpenseDocument,
                ct
            );
        }
        catch (DocumentFlowPolicyViolationException ex)
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

        try
        {
            var document = ExpenseDocument.CreateDraft(
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
                cmd.Notes,
                ExpenseDraftRules.ResolveTaxSupportCode(cmd.TaxSupportCode, supplier.Role)
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
            await _repo.AddAsync(document, ct);
            await _repo.SaveChangesAsync(ct);

            return Result<ExpenseDocumentDetailDto>.Success(
                ExpenseDocumentMapper.ToDetail(document)
            );
        }
        catch (ArgumentException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }
    }
}

public sealed class UpdateExpenseDraftHandler
    : IRequestHandler<UpdateExpenseDraftCommand, Result<ExpenseDocumentDetailDto>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly IExpenseCategoryRepository _categories;
    private readonly IAccountRepository _accounts;
    private readonly IBusinessPartnerRepository _businessPartners;
    private readonly IBusinessPartnerRoleRepository _roles;
    private readonly IPaymentTermRepository _paymentTerms;
    private readonly ISriTaxResolver _tax;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public UpdateExpenseDraftHandler(
        IExpenseDocumentRepository repo,
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        IBusinessPartnerRepository businessPartners,
        IBusinessPartnerRoleRepository roles,
        IPaymentTermRepository paymentTerms,
        ISriTaxResolver tax,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _repo = repo;
        _categories = categories;
        _accounts = accounts;
        _businessPartners = businessPartners;
        _roles = roles;
        _paymentTerms = paymentTerms;
        _tax = tax;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<ExpenseDocumentDetailDto>> Handle(
        UpdateExpenseDraftCommand cmd,
        CancellationToken ct
    )
    {
        var document = await _repo.GetByIdAsync(_tenant.TenantId, cmd.Id, ct);
        if (document is null || document.BranchId != _branch.BranchId)
            return Result<ExpenseDocumentDetailDto>.NotFound("Gasto no encontrado.");
        if (document.Status != ExpenseStatus.Draft)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "Solo se pueden editar gastos en estado borrador."
            );

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
        if (duplicate is not null && duplicate.Id != document.Id)
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

        try
        {
            document.UpdateDraft(
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
                cmd.Notes,
                ExpenseDraftRules.ResolveTaxSupportCode(cmd.TaxSupportCode, supplier.Role)
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
            await _repo.SaveChangesAsync(ct);

            return Result<ExpenseDocumentDetailDto>.Success(
                ExpenseDocumentMapper.ToDetail(document)
            );
        }
        catch (ArgumentException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }
    }
}

public sealed class ListExpenseDocumentsHandler
    : IRequestHandler<ListExpenseDocumentsQuery, Result<ExpenseDocumentListResponse>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;

    public ListExpenseDocumentsHandler(
        IExpenseDocumentRepository repo,
        ICurrentTenant tenant,
        ICurrentBranch branch
    )
    {
        _repo = repo;
        _tenant = tenant;
        _branch = branch;
    }

    public async Task<Result<ExpenseDocumentListResponse>> Handle(
        ListExpenseDocumentsQuery q,
        CancellationToken ct
    )
    {
        var (items, lineCounts, total) = await _repo.GetPagedAsync(
            _tenant.TenantId,
            _branch.BranchId,
            q.Search,
            q.Status,
            q.PageNumber,
            q.PageSize,
            ct
        );
        return Result<ExpenseDocumentListResponse>.Success(
            new ExpenseDocumentListResponse(
                items.Select(x =>
                        ExpenseDocumentMapper.ToListItem(x, lineCounts.GetValueOrDefault(x.Id))
                    )
                    .ToList(),
                total,
                q.PageNumber,
                q.PageSize
            )
        );
    }
}

public sealed class GetExpenseDocumentByIdHandler
    : IRequestHandler<GetExpenseDocumentByIdQuery, Result<ExpenseDocumentDetailDto>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;

    public GetExpenseDocumentByIdHandler(
        IExpenseDocumentRepository repo,
        ICurrentTenant tenant,
        ICurrentBranch branch
    )
    {
        _repo = repo;
        _tenant = tenant;
        _branch = branch;
    }

    public async Task<Result<ExpenseDocumentDetailDto>> Handle(
        GetExpenseDocumentByIdQuery q,
        CancellationToken ct
    )
    {
        var document = await _repo.GetByIdAsync(_tenant.TenantId, q.Id, ct);
        return document is null || document.BranchId != _branch.BranchId
            ? Result<ExpenseDocumentDetailDto>.NotFound("Gasto no encontrado.")
            : Result<ExpenseDocumentDetailDto>.Success(ExpenseDocumentMapper.ToDetail(document));
    }
}

// EXPENSES-WORKFLOW-INTEGRATION-01: internal (no file-scoped) para que
// ExpenseDocumentConfirmUseCases.cs reutilice las mismas reglas de resolución al construir un
// gasto ya confirmado (CreateConfirmedExpenseCommand) — mismo criterio que ExpenseDocumentMapper.
internal sealed record ExpenseDraftError(string Message, string Code)
{
    public Result<T> ToResult<T>() =>
        Code == ApiResponseCodes.Common.NotFound
            ? Result<T>.NotFound(Message)
            : Code == ApiResponseCodes.Common.Conflict
                ? Result<T>.Conflict(Message)
                : Result<T>.ValidationFailure(Message);
}

internal sealed record SupplierResolution(
    BusinessPartner? BusinessPartner,
    BusinessPartnerRole? Role,
    ExpenseDraftError? Error
);

internal sealed record PaymentTermResolution(PaymentTerm? PaymentTerm, ExpenseDraftError? Error);

internal sealed record DueDateResolution(DateOnly? Value, ExpenseDraftError? Error);

internal sealed record LineResolution(IReadOnlyList<ExpenseLine>? Lines, ExpenseDraftError? Error);

internal static class ExpenseDraftRules
{
    public static async Task<SupplierResolution> ResolveSupplierAsync(
        IBusinessPartnerRepository businessPartners,
        IBusinessPartnerRoleRepository roles,
        Guid tenantId,
        Guid supplierId,
        CancellationToken ct
    )
    {
        var supplier = await businessPartners.GetByIdAsync(supplierId, ct);
        if (supplier is null || supplier.TenantId != tenantId)
            return NotFoundSupplier();
        if (!supplier.IsActive)
            return ValidationSupplier("El proveedor esta inactivo.");

        var role = await roles.GetByTypeAsync(supplierId, RoleType.Supplier, ct);
        if (role is null || role.TenantId != tenantId || !role.IsActive)
            return ValidationSupplier("El tercero seleccionado no tiene rol de proveedor activo.");

        return new SupplierResolution(supplier, role, null);
    }

    public static async Task<PaymentTermResolution> ResolvePaymentTermAsync(
        IPaymentTermRepository paymentTerms,
        Guid tenantId,
        Guid? paymentTermId,
        BusinessPartnerRole? supplierRole,
        CancellationToken ct
    )
    {
        var resolvedId = paymentTermId ?? supplierRole?.SupplierConfig?.PaymentTermId;
        if (!resolvedId.HasValue || resolvedId.Value == Guid.Empty)
            return new PaymentTermResolution(
                null,
                Validation("La condicion de pago es obligatoria para crear el borrador.")
            );

        var paymentTerm = await paymentTerms.GetByIdAsync(tenantId, resolvedId.Value, ct);
        if (paymentTerm is null)
            return new PaymentTermResolution(
                null,
                new ExpenseDraftError(
                    "La condicion de pago no existe.",
                    ApiResponseCodes.Common.NotFound
                )
            );
        if (!paymentTerm.IsActive)
            return new PaymentTermResolution(
                null,
                Validation("La condicion de pago esta inactiva.")
            );

        return new PaymentTermResolution(paymentTerm, null);
    }

    /// <summary>
    /// RETENTIONS-SOURCE-DOCUMENT-TAX-SUPPORT-02G — mismo patrón de resolución que
    /// <see cref="ResolvePaymentTermAsync"/>: el valor explícito del command (si lo hay) prevalece;
    /// si no llega, cae al default configurable del proveedor
    /// (<c>SupplierRoleConfig.DefaultTaxSupportCode</c> — mismo campo que ya usan
    /// <c>PurchaseInvoice</c>/Compras como sugerencia de pre-llenado, nunca un valor inventado
    /// aquí). <c>null</c> es un resultado válido cuando ninguna de las dos fuentes lo tiene — se
    /// documenta como gap conocido, no bloquea la creación del gasto.
    /// </summary>
    public static string? ResolveTaxSupportCode(string? explicitCode, BusinessPartnerRole? supplierRole)
    {
        var trimmed = explicitCode?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? supplierRole?.SupplierConfig?.DefaultTaxSupportCode
            : trimmed;
    }

    public static DueDateResolution ResolveDueDate(
        DateOnly issueDate,
        DateOnly? dueDate,
        PaymentTerm paymentTerm
    )
    {
        var resolved = dueDate ?? issueDate.AddDays(Math.Max(paymentTerm.TotalDays, 0));
        return resolved < issueDate
            ? new DueDateResolution(
                null,
                Validation("La fecha de vencimiento no puede ser anterior a la fecha de emision.")
            )
            : new DueDateResolution(resolved, null);
    }

    public static async Task<LineResolution> BuildLinesAsync(
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        ISriTaxResolver tax,
        Guid tenantId,
        Guid companyId,
        Guid documentId,
        IReadOnlyList<ExpenseDraftLineRequest> inputs,
        CancellationToken ct
    )
    {
        if (inputs.Count == 0)
            return new LineResolution(null, Validation("Debe incluir al menos una linea."));

        var lines = new List<ExpenseLine>();
        foreach (var input in inputs)
        {
            var category = await categories.GetByIdAsync(
                tenantId,
                input.ExpenseSubcategoryId,
                ct
            );
            if (category is null || category.CompanyId != companyId)
                return new LineResolution(
                    null,
                    new ExpenseDraftError(
                        "La subcategoria de gasto no existe.",
                        ApiResponseCodes.Common.NotFound
                    )
                );
            if (!category.IsActive)
                return new LineResolution(
                    null,
                    Validation($"La subcategoria '{category.Name}' esta inactiva.")
                );
            if (category.Level != ExpenseCategoryNodeLevel.Subcategory)
                return new LineResolution(
                    null,
                    Validation("Cada linea debe apuntar a una subcategoria de gasto.")
                );
            if (!category.AccountingAccountId.HasValue)
                return new LineResolution(
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
            var accountError = ValidateAccount(account, category.Name);
            if (accountError is not null)
                return new LineResolution(null, accountError);

            var vatCode = input.VatCode.Trim();
            var vat = await tax.GetVatRateWithNameAsync(vatCode, ct);
            if (vat is null)
                return new LineResolution(
                    null,
                    Validation($"Codigo IVA '{vatCode}' no encontrado o inactivo.")
                );

            var description = string.IsNullOrWhiteSpace(input.Description)
                ? category.Name
                : input.Description.Trim();

            try
            {
                lines.Add(
                    ExpenseLine.Create(
                        documentId,
                        tenantId,
                        category.Id,
                        account!.Id,
                        description,
                        input.Quantity,
                        input.UnitPrice,
                        vatCode,
                        vat.Rate,
                        vat.Name,
                        discountAmount: input.DiscountValue,
                        snapshotAccountingAccountCode: account.Code.Value,
                        snapshotAccountingAccountName: account.Name,
                        notes: input.Notes
                    )
                );
            }
            catch (ArgumentException ex)
            {
                return new LineResolution(null, Validation(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return new LineResolution(null, Validation(ex.Message));
            }
        }

        return new LineResolution(lines, null);
    }

    private static ExpenseDraftError? ValidateAccount(Account? account, string categoryName)
    {
        if (account is null)
            return new ExpenseDraftError(
                $"La cuenta contable de la subcategoria '{categoryName}' no existe.",
                ApiResponseCodes.Common.NotFound
            );
        if (!account.IsActive)
            return Validation(
                $"La cuenta contable de la subcategoria '{categoryName}' esta inactiva."
            );
        if (!account.AllowsPosting)
            return Validation(
                $"La cuenta contable de la subcategoria '{categoryName}' no permite contabilizacion."
            );
        if (account.AccountType != AccountType.Expense)
            return Validation(
                $"La cuenta contable de la subcategoria '{categoryName}' debe ser de tipo gasto."
            );

        return null;
    }

    private static SupplierResolution NotFoundSupplier() =>
        new(
            null,
            null,
            new ExpenseDraftError("Proveedor no encontrado.", ApiResponseCodes.Common.NotFound)
        );

    private static SupplierResolution ValidationSupplier(string message) =>
        new(null, null, Validation(message));

    private static ExpenseDraftError Validation(string message) =>
        new(message, ApiResponseCodes.Common.ValidationError);
}

/// <summary>
/// EXPENSES-WORKFLOW-INTEGRATION-01: traduce <see cref="DocumentFlowPolicyViolationException"/> a
/// mensajes específicos del módulo de Gastos — el mensaje genérico de la excepción (SSOT
/// reutilizable por otros módulos) no es el texto que el usuario de Gastos debe ver. Sin
/// traducción conocida para el código (p. ej. tipo deshabilitado, caso no cubierto por los
/// mensajes fijos pedidos), cae al mensaje genérico de la excepción.
/// </summary>
internal static class ExpenseWorkflowPolicyMessages
{
    public static string Translate(DocumentFlowPolicyViolationException ex) =>
        ex.Code switch
        {
            "document_flow_policy.draft_not_allowed" =>
                "La política de la empresa no permite guardar borradores para documentos de gasto.",
            "document_flow_policy.draft_required" =>
                "La política de la empresa requiere guardar el gasto como borrador antes de confirmarlo.",
            _ => ex.Message,
        };
}

// EXPENSES-CONFIRM-07: internal (no file-scoped) para que ExpenseDocumentConfirmUseCases.cs
// reutilice el mismo mapeo — una sola fuente de verdad para ExpenseDocumentDetailDto.
internal static class ExpenseDocumentMapper
{
    public static ExpenseDocumentListItemDto ToListItem(ExpenseDocument document, int lineCount) =>
        new(
            document.Id,
            document.CompanyId,
            document.BranchId,
            document.SupplierId,
            document.SupplierName,
            document.SupplierTaxId,
            document.IssueDate,
            document.AccountingDate,
            document.DocumentType,
            document.DocumentNumber,
            document.DueDate,
            document.Status,
            lineCount,
            document.Subtotal,
            document.TotalDiscount,
            document.TotalTax,
            document.GrandTotal,
            document.CreatedAt
        );

    public static ExpenseDocumentDetailDto ToDetail(ExpenseDocument document) =>
        new(
            document.Id,
            document.CompanyId,
            document.BranchId,
            document.SupplierId,
            document.SupplierName,
            document.SupplierTaxId,
            document.IssueDate,
            document.AccountingDate,
            document.DocumentType,
            document.DocumentNumber,
            document.AuthorizationNumber,
            document.AuthorizationDate,
            document.PaymentTermId,
            document.PaymentTermName,
            document.DueDate,
            document.Subtotal,
            document.TotalDiscount,
            document.TotalTax,
            document.GrandTotal,
            document.Notes,
            document.TaxSupportCode,
            document.Status,
            document.Lines.OrderBy(x => x.SortOrder).Select(ToLine).ToList(),
            document.CancelReason,
            document.CancelledAt,
            document.CancelledBy
        );

    private static ExpenseLineDto ToLine(ExpenseLine line) =>
        new(
            line.Id,
            line.ExpenseSubcategoryId,
            line.SnapshotAccountingAccountId,
            line.SnapshotAccountingAccountCode,
            line.SnapshotAccountingAccountName,
            line.Description,
            line.Quantity,
            line.UnitAmount,
            line.DiscountAmount,
            line.VatCode,
            line.VatRate,
            line.VatAmount,
            line.TaxInclusiveTotal,
            line.SortOrder,
            line.Notes
        );
}
