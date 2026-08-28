using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Expenses.UseCases.Categories;

// -- Queries -----------------------------------------------------------------

public sealed record ListExpenseCategoryTreeQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<ExpenseCategoryTreeNodeDto>>>,
        ICompanyScopedRequest;

public sealed record GetExpenseCategoryNodeByIdQuery(Guid Id)
    : IRequest<Result<ExpenseCategoryNodeDto>>,
        ICompanyScopedRequest;

// -- Commands ----------------------------------------------------------------

public sealed record CreateExpenseCategoryNodeCommand(
    string Code,
    string Name,
    ExpenseCategoryNodeLevel Level,
    Guid? ParentId,
    Guid? AccountingAccountId,
    string? Description = null,
    bool IsDeductible = true,
    bool RequiresInvoice = true
) : IRequest<Result<ExpenseCategoryNodeDto>>, ICompanyScopedRequest;

public sealed record UpdateExpenseCategoryNodeCommand(
    Guid Id,
    string Code,
    string Name,
    Guid? AccountingAccountId,
    string? Description = null,
    bool IsDeductible = true,
    bool RequiresInvoice = true
) : IRequest<Result<ExpenseCategoryNodeDto>>, ICompanyScopedRequest;

public sealed record ActivateExpenseCategoryNodeCommand(Guid Id)
    : IRequest<Result<ExpenseCategoryNodeDto>>,
        ICompanyScopedRequest;

public sealed record DeactivateExpenseCategoryNodeCommand(Guid Id)
    : IRequest<Result<ExpenseCategoryNodeDto>>,
        ICompanyScopedRequest;

// -- Validators --------------------------------------------------------------

public sealed class ListExpenseCategoryTreeValidator
    : AbstractValidator<ListExpenseCategoryTreeQuery>
{
}

public sealed class GetExpenseCategoryNodeByIdValidator
    : AbstractValidator<GetExpenseCategoryNodeByIdQuery>
{
    public GetExpenseCategoryNodeByIdValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class CreateExpenseCategoryNodeValidator
    : AbstractValidator<CreateExpenseCategoryNodeCommand>
{
    public CreateExpenseCategoryNodeValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(ExpenseCategoryNode.CodeMaxLen)
            .WithMessage("El codigo es obligatorio (maximo 30 caracteres).");
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(ExpenseCategoryNode.NameMaxLen)
            .WithMessage("El nombre es obligatorio (maximo 150 caracteres).");
        RuleFor(x => x.Description)
            .MaximumLength(ExpenseCategoryNode.DescriptionMaxLen)
            .When(x => x.Description is not null)
            .WithMessage("La descripcion no puede superar 500 caracteres.");
        RuleFor(x => x.Level).IsInEnum();

        When(
            x => x.Level == ExpenseCategoryNodeLevel.Type,
            () =>
            {
                RuleFor(x => x.ParentId)
                    .Null()
                    .WithMessage("El tipo de gasto no puede tener padre.");
                RuleFor(x => x.AccountingAccountId)
                    .Null()
                    .WithMessage("El tipo de gasto no puede tener cuenta contable.");
            }
        );

        When(
            x => x.Level == ExpenseCategoryNodeLevel.Category,
            () =>
            {
                RuleFor(x => x.ParentId)
                    .NotEmpty()
                    .WithMessage("La categoria requiere un tipo de gasto padre.");
                RuleFor(x => x.AccountingAccountId)
                    .Null()
                    .WithMessage("La categoria no puede tener cuenta contable.");
            }
        );

        When(
            x => x.Level == ExpenseCategoryNodeLevel.Subcategory,
            () =>
            {
                RuleFor(x => x.ParentId)
                    .NotEmpty()
                    .WithMessage("La subcategoria requiere una categoria padre.");
                RuleFor(x => x.AccountingAccountId)
                    .NotEmpty()
                    .WithMessage("La subcategoria requiere cuenta contable.");
            }
        );
    }
}

public sealed class UpdateExpenseCategoryNodeValidator
    : AbstractValidator<UpdateExpenseCategoryNodeCommand>
{
    public UpdateExpenseCategoryNodeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(ExpenseCategoryNode.CodeMaxLen)
            .WithMessage("El codigo es obligatorio (maximo 30 caracteres).");
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(ExpenseCategoryNode.NameMaxLen)
            .WithMessage("El nombre es obligatorio (maximo 150 caracteres).");
        RuleFor(x => x.Description)
            .MaximumLength(ExpenseCategoryNode.DescriptionMaxLen)
            .When(x => x.Description is not null)
            .WithMessage("La descripcion no puede superar 500 caracteres.");
    }
}

public sealed class ActivateExpenseCategoryNodeValidator
    : AbstractValidator<ActivateExpenseCategoryNodeCommand>
{
    public ActivateExpenseCategoryNodeValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class DeactivateExpenseCategoryNodeValidator
    : AbstractValidator<DeactivateExpenseCategoryNodeCommand>
{
    public DeactivateExpenseCategoryNodeValidator() => RuleFor(x => x.Id).NotEmpty();
}

// -- Handlers ----------------------------------------------------------------

public sealed class ListExpenseCategoryTreeHandler
    : IRequestHandler<
        ListExpenseCategoryTreeQuery,
        Result<IReadOnlyList<ExpenseCategoryTreeNodeDto>>
    >
{
    private readonly IExpenseCategoryRepository _repo;
    private readonly ICurrentTenant _t;

    public ListExpenseCategoryTreeHandler(IExpenseCategoryRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<ExpenseCategoryTreeNodeDto>>> Handle(
        ListExpenseCategoryTreeQuery q,
        CancellationToken ct
    )
    {
        var nodes = await _repo.GetTreeAsync(_t.TenantId, q.IncludeInactive, ct);
        return Result<IReadOnlyList<ExpenseCategoryTreeNodeDto>>.Success(Map.ToTree(nodes));
    }
}

public sealed class GetExpenseCategoryNodeByIdHandler
    : IRequestHandler<GetExpenseCategoryNodeByIdQuery, Result<ExpenseCategoryNodeDto>>
{
    private readonly IExpenseCategoryRepository _repo;
    private readonly ICurrentTenant _t;

    public GetExpenseCategoryNodeByIdHandler(IExpenseCategoryRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<ExpenseCategoryNodeDto>> Handle(
        GetExpenseCategoryNodeByIdQuery q,
        CancellationToken ct
    )
    {
        var node = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return node is null
            ? Result<ExpenseCategoryNodeDto>.NotFound("Nodo de categoria de gasto no encontrado.")
            : Result<ExpenseCategoryNodeDto>.Success(Map.ToDto(node));
    }
}

public sealed class CreateExpenseCategoryNodeHandler
    : IRequestHandler<CreateExpenseCategoryNodeCommand, Result<ExpenseCategoryNodeDto>>
{
    private readonly IExpenseCategoryRepository _repo;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CreateExpenseCategoryNodeHandler(
        IExpenseCategoryRepository repo,
        IAccountRepository accounts,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _accounts = accounts;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<ExpenseCategoryNodeDto>> Handle(
        CreateExpenseCategoryNodeCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;

        var duplicate = await ExpenseCategoryRules.EnsureUniqueAsync(
            _repo,
            tid,
            cid,
            cmd.ParentId,
            cmd.Level,
            cmd.Code,
            cmd.Name,
            excludeId: null,
            ct
        );
        if (duplicate is not null)
            return duplicate;

        var parent = cmd.ParentId.HasValue
            ? await _repo.GetByIdAsync(tid, cmd.ParentId.Value, ct)
            : null;

        var parentValidation = ValidateParentForCreate(cmd.Level, parent, cmd.ParentId);
        if (parentValidation is not null)
            return parentValidation;

        var accountValidation = await ValidateAccountForLevelAsync(
            cmd.Level,
            cmd.AccountingAccountId,
            requireSubcategoryAccount: true,
            ct
        );
        if (accountValidation is not null)
            return accountValidation;

        ExpenseCategoryNode node;
        try
        {
            node = cmd.Level switch
            {
                ExpenseCategoryNodeLevel.Type => ExpenseCategoryNode.CreateType(
                    tid,
                    cid,
                    cmd.Code,
                    cmd.Name,
                    _u.UserId,
                    cmd.Description
                ),
                ExpenseCategoryNodeLevel.Category => ExpenseCategoryNode.CreateCategory(
                    tid,
                    cid,
                    parent!,
                    cmd.Code,
                    cmd.Name,
                    _u.UserId,
                    cmd.Description
                ),
                ExpenseCategoryNodeLevel.Subcategory => ExpenseCategoryNode.CreateSubcategory(
                    tid,
                    cid,
                    parent!,
                    cmd.Code,
                    cmd.Name,
                    cmd.AccountingAccountId!.Value,
                    _u.UserId,
                    cmd.Description,
                    cmd.IsDeductible,
                    cmd.RequiresInvoice
                ),
                _ => throw new ArgumentException("Nivel de categoria de gasto no soportado."),
            };
        }
        catch (ArgumentException ex)
        {
            return Result<ExpenseCategoryNodeDto>.ValidationFailure(ex.Message);
        }

        await _repo.AddAsync(node, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ExpenseCategoryNodeDto>.Success(Map.ToDto(node));
    }

    private async Task<Result<ExpenseCategoryNodeDto>?> ValidateAccountForLevelAsync(
        ExpenseCategoryNodeLevel level,
        Guid? accountId,
        bool requireSubcategoryAccount,
        CancellationToken ct
    )
    {
        if (level != ExpenseCategoryNodeLevel.Subcategory)
        {
            if (accountId.HasValue)
                return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                    "Solo una subcategoria puede tener cuenta contable."
                );

            return null;
        }

        if (!accountId.HasValue || accountId.Value == Guid.Empty)
        {
            if (requireSubcategoryAccount)
                return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                    "La subcategoria requiere cuenta contable."
                );

            return null;
        }

        var account = await _accounts.GetByIdAsync(_t.TenantId, _c.CompanyId, accountId.Value, ct);
        return ExpenseCategoryRules.ValidateExpenseAccount(account);
    }

    private static Result<ExpenseCategoryNodeDto>? ValidateParentForCreate(
        ExpenseCategoryNodeLevel level,
        ExpenseCategoryNode? parent,
        Guid? parentId
    )
    {
        if (level == ExpenseCategoryNodeLevel.Type)
        {
            if (parentId.HasValue)
                return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                    "El tipo de gasto no puede tener padre."
                );

            return null;
        }

        if (!parentId.HasValue || parentId.Value == Guid.Empty || parent is null)
            return Result<ExpenseCategoryNodeDto>.NotFound("El nodo padre indicado no existe.");

        if (!parent.IsActive)
            return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                "El nodo padre indicado esta inactivo."
            );

        if (level == ExpenseCategoryNodeLevel.Category)
            return parent.Level == ExpenseCategoryNodeLevel.Type
                ? null
                : Result<ExpenseCategoryNodeDto>.ValidationFailure(
                    "La categoria debe crearse bajo un tipo de gasto."
                );

        return parent.Level == ExpenseCategoryNodeLevel.Category
            ? null
            : Result<ExpenseCategoryNodeDto>.ValidationFailure(
                "La subcategoria debe crearse bajo una categoria de gasto."
            );
    }

}

public sealed class UpdateExpenseCategoryNodeHandler
    : IRequestHandler<UpdateExpenseCategoryNodeCommand, Result<ExpenseCategoryNodeDto>>
{
    private readonly IExpenseCategoryRepository _repo;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public UpdateExpenseCategoryNodeHandler(
        IExpenseCategoryRepository repo,
        IAccountRepository accounts,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _accounts = accounts;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<ExpenseCategoryNodeDto>> Handle(
        UpdateExpenseCategoryNodeCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;
        var node = await _repo.GetByIdAsync(tid, cmd.Id, ct);
        if (node is null)
            return Result<ExpenseCategoryNodeDto>.NotFound(
                "Nodo de categoria de gasto no encontrado."
            );

        var duplicate = await ExpenseCategoryRules.EnsureUniqueAsync(
            _repo,
            tid,
            cid,
            node.ParentId,
            node.Level,
            cmd.Code,
            cmd.Name,
            cmd.Id,
            ct
        );
        if (duplicate is not null)
            return duplicate;

        if (node.Level != ExpenseCategoryNodeLevel.Subcategory && cmd.AccountingAccountId.HasValue)
            return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                "Solo una subcategoria puede tener cuenta contable."
            );

        if (node.Level == ExpenseCategoryNodeLevel.Subcategory)
        {
            if (!cmd.AccountingAccountId.HasValue || cmd.AccountingAccountId.Value == Guid.Empty)
                return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                    "La subcategoria requiere cuenta contable."
                );

            var account = await _accounts.GetByIdAsync(
                tid,
                cid,
                cmd.AccountingAccountId.Value,
                ct
            );
            var accountValidation = ExpenseCategoryRules.ValidateExpenseAccount(account);
            if (accountValidation is not null)
                return accountValidation;

            node.ChangeSubcategoryAccount(cmd.AccountingAccountId.Value, _u.UserId);
            node.UpdateSubcategoryTaxRules(cmd.IsDeductible, cmd.RequiresInvoice, _u.UserId);
        }

        try
        {
            node.Rename(cmd.Code, cmd.Name, _u.UserId, cmd.Description);
        }
        catch (ArgumentException ex)
        {
            return Result<ExpenseCategoryNodeDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<ExpenseCategoryNodeDto>.Success(Map.ToDto(node));
    }
}

public sealed class ActivateExpenseCategoryNodeHandler
    : IRequestHandler<ActivateExpenseCategoryNodeCommand, Result<ExpenseCategoryNodeDto>>
{
    private readonly IExpenseCategoryRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public ActivateExpenseCategoryNodeHandler(
        IExpenseCategoryRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<ExpenseCategoryNodeDto>> Handle(
        ActivateExpenseCategoryNodeCommand cmd,
        CancellationToken ct
    )
    {
        var node = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (node is null)
            return Result<ExpenseCategoryNodeDto>.NotFound(
                "Nodo de categoria de gasto no encontrado."
            );

        if (node.ParentId.HasValue)
        {
            var parent = await _repo.GetByIdAsync(_t.TenantId, node.ParentId.Value, ct);
            if (parent is null)
                return Result<ExpenseCategoryNodeDto>.NotFound("El nodo padre indicado no existe.");

            if (!parent.IsActive)
                return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                    "No se puede activar el nodo porque su padre esta inactivo."
                );
        }

        node.SetActive(true, _u.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<ExpenseCategoryNodeDto>.Success(Map.ToDto(node));
    }
}

public sealed class DeactivateExpenseCategoryNodeHandler
    : IRequestHandler<DeactivateExpenseCategoryNodeCommand, Result<ExpenseCategoryNodeDto>>
{
    private readonly IExpenseCategoryRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public DeactivateExpenseCategoryNodeHandler(
        IExpenseCategoryRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<ExpenseCategoryNodeDto>> Handle(
        DeactivateExpenseCategoryNodeCommand cmd,
        CancellationToken ct
    )
    {
        var node = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (node is null)
            return Result<ExpenseCategoryNodeDto>.NotFound(
                "Nodo de categoria de gasto no encontrado."
            );

        if (await _repo.HasActiveChildrenAsync(_t.TenantId, node.Id, ct))
            return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                "No se puede desactivar el nodo porque tiene hijos activos."
            );

        node.SetActive(false, _u.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<ExpenseCategoryNodeDto>.Success(Map.ToDto(node));
    }
}

file static class Map
{
    public static ExpenseCategoryNodeDto ToDto(ExpenseCategoryNode node) =>
        new(
            node.Id,
            node.CompanyId,
            node.ParentId,
            node.Code,
            node.Name,
            node.Description,
            node.Level,
            node.AccountingAccountId,
            node.IsDeductible,
            node.RequiresInvoice,
            node.IsActive
        );

    public static IReadOnlyList<ExpenseCategoryTreeNodeDto> ToTree(
        IReadOnlyList<ExpenseCategoryNode> nodes
    )
    {
        var childrenByParent = nodes
            .GroupBy(x => x.ParentId ?? Guid.Empty)
            .ToDictionary(x => x.Key, x => x.OrderBy(n => n.Code).ThenBy(n => n.Name).ToList());

        return Build(Guid.Empty);

        IReadOnlyList<ExpenseCategoryTreeNodeDto> Build(Guid parentId) =>
            childrenByParent.TryGetValue(parentId, out var children)
                ? children.Select(ToTreeNode).ToList()
                : [];

        ExpenseCategoryTreeNodeDto ToTreeNode(ExpenseCategoryNode node) =>
            new(
                node.Id,
                node.CompanyId,
                node.ParentId,
                node.Code,
                node.Name,
                node.Description,
                node.Level,
                node.AccountingAccountId,
                node.IsDeductible,
                node.RequiresInvoice,
                node.IsActive,
                Build(node.Id)
            );
    }
}

file static class ExpenseCategoryRules
{
    public static async Task<Result<ExpenseCategoryNodeDto>?> EnsureUniqueAsync(
        IExpenseCategoryRepository repo,
        Guid tenantId,
        Guid companyId,
        Guid? parentId,
        ExpenseCategoryNodeLevel level,
        string code,
        string name,
        Guid? excludeId,
        CancellationToken ct
    )
    {
        if (
            await repo.CodeExistsAsync(
                tenantId,
                companyId,
                parentId,
                level,
                code,
                excludeId,
                ct
            )
        )
            return Result<ExpenseCategoryNodeDto>.Conflict(
                "Ya existe un nodo de gasto con el mismo codigo en este nivel."
            );

        if (
            await repo.NameExistsAsync(
                tenantId,
                companyId,
                parentId,
                level,
                name,
                excludeId,
                ct
            )
        )
            return Result<ExpenseCategoryNodeDto>.Conflict(
                "Ya existe un nodo de gasto con el mismo nombre en este nivel."
            );

        return null;
    }

    public static Result<ExpenseCategoryNodeDto>? ValidateExpenseAccount(Account? account)
    {
        if (account is null)
            return Result<ExpenseCategoryNodeDto>.NotFound(
                "La cuenta contable indicada no existe o no pertenece a esta empresa."
            );

        if (!account.IsActive)
            return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                "La cuenta contable indicada esta inactiva."
            );

        if (!account.AllowsPosting)
            return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                "La cuenta contable indicada no permite registros contables."
            );

        if (account.AccountType != AccountType.Expense)
            return Result<ExpenseCategoryNodeDto>.ValidationFailure(
                "La cuenta contable indicada debe ser de tipo gasto."
            );

        return null;
    }
}
