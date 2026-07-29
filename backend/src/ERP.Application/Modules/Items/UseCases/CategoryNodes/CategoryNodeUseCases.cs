using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.CategoryNodes;

// ══════════════════════════════════════════════════════════════════════════
// COMMANDS
// ══════════════════════════════════════════════════════════════════════════

public sealed record CreateCategoryNodeCommand(
    Guid? ParentId,
    string Code,
    string Name,
    string? Description,
    string Level,
    int SortOrder = 0
) : IRequest<Result<CategoryNodeDto>>, ICompanyScopedRequest;

public sealed record UpdateCategoryNodeCommand(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int? SortOrder
) : IRequest<Result<CategoryNodeDto>>, ICompanyScopedRequest;

public sealed record DisableCategoryNodeCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed record EnableCategoryNodeCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

// ══════════════════════════════════════════════════════════════════════════
// QUERIES
// ══════════════════════════════════════════════════════════════════════════

public sealed record GetCategoryTreeQuery(bool IncludeInactive = false)
    : IRequest<Result<CategoryTreeDto>>,
        ICompanyScopedRequest;

public sealed record GetCategoryNodeByIdQuery(Guid Id)
    : IRequest<Result<CategoryNodeDto>>,
        ICompanyScopedRequest;

// ══════════════════════════════════════════════════════════════════════════
// VALIDATORS
// ══════════════════════════════════════════════════════════════════════════

public sealed class CreateCategoryNodeCommandValidator
    : AbstractValidator<CreateCategoryNodeCommand>
{
    public CreateCategoryNodeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Level).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}

public sealed class UpdateCategoryNodeCommandValidator
    : AbstractValidator<UpdateCategoryNodeCommand>
{
    public UpdateCategoryNodeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// HANDLERS
// ══════════════════════════════════════════════════════════════════════════

public sealed class CreateCategoryNodeCommandHandler
    : IRequestHandler<CreateCategoryNodeCommand, Result<CategoryNodeDto>>
{
    private readonly ICategoryNodeRepository _repo;
    private readonly IOrgSettingsRepository _orgRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public CreateCategoryNodeCommandHandler(
        ICategoryNodeRepository repo,
        IOrgSettingsRepository orgRepo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _repo = repo;
        _orgRepo = orgRepo;
        _tenant = tenant;
        _company = company;
        _user = user;
    }

    public async Task<Result<CategoryNodeDto>> Handle(
        CreateCategoryNodeCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _tenant.TenantId;
        var userId = _user.UserId;

        if (await _repo.CodeExistsAsync(cmd.Code.Trim().ToUpperInvariant(), tenantId, ct: ct))
            return Result<CategoryNodeDto>.Conflict($"Código '{cmd.Code}' ya existe.");

        if (!Enum.TryParse<CategoryNodeLevel>(cmd.Level, true, out var level))
            return Result<CategoryNodeDto>.ValidationFailure($"Nivel '{cmd.Level}' no es válido.");

        ItemCategoryNode? parent = null;
        if (cmd.ParentId.HasValue)
        {
            parent = await _repo.GetByIdAsync(cmd.ParentId.Value, ct);
            if (parent is null)
                return Result<CategoryNodeDto>.NotFound("Nodo padre no encontrado.");
            if (!parent.IsActive)
                return Result<CategoryNodeDto>.ValidationFailure(
                    "No se puede crear un hijo en un nodo inactivo."
                );
        }

        var newDepth = CategoryDepthResolver.DepthOf(parent?.Path) + 1;
        var maxDepth = await CategoryDepthResolver.ResolveMaxDepthAsync(
            _orgRepo,
            tenantId,
            _company.CompanyId,
            ct
        );
        if (newDepth > maxDepth)
            return Result<CategoryNodeDto>.ValidationFailure(
                $"Se alcanzó la profundidad máxima de categorías configurada para esta empresa ({maxDepth} niveles)."
            );

        var node = ItemCategoryNode.Create(
            tenantId,
            cmd.Code,
            cmd.Name,
            level,
            userId,
            cmd.ParentId,
            cmd.Description,
            cmd.SortOrder
        );

        var path = await BuildPathAsync(node, ct);
        node.SetPath(path);

        await _repo.AddAsync(node, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<CategoryNodeDto>.Success(CategoryNodeMapping.ToDto(node));
    }

    private async Task<string> BuildPathAsync(ItemCategoryNode node, CancellationToken ct)
    {
        if (node.ParentId is null)
            return $"/{node.Id}";

        var parent = await _repo.GetByIdAsync(node.ParentId.Value, ct);
        return parent is null ? $"/{node.Id}" : $"{parent.Path}/{node.Id}";
    }
}

/// <summary>
/// Resuelve la profundidad de un nodo a partir de su Path materializado
/// y el límite máximo configurado por empresa (OrgSettings, default 3).
/// </summary>
internal static class CategoryDepthResolver
{
    public const int DefaultMaxDepth = 3;

    public static int DepthOf(string? path) =>
        string.IsNullOrEmpty(path)
            ? 0
            : path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

    public static async Task<int> ResolveMaxDepthAsync(
        IOrgSettingsRepository orgRepo,
        Guid tenantId,
        Guid companyId,
        CancellationToken ct
    )
    {
        var setting = await orgRepo.GetAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            OrgSettingKeys.Catalog.MaxCategoryDepth,
            ct
        );

        return setting is not null && int.TryParse(setting.Value, out var value) && value > 0
            ? value
            : DefaultMaxDepth;
    }
}

public sealed class UpdateCategoryNodeCommandHandler
    : IRequestHandler<UpdateCategoryNodeCommand, Result<CategoryNodeDto>>
{
    private readonly ICategoryNodeRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public UpdateCategoryNodeCommandHandler(
        ICategoryNodeRepository repo,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<CategoryNodeDto>> Handle(
        UpdateCategoryNodeCommand cmd,
        CancellationToken ct
    )
    {
        var node = await _repo.GetByIdAsync(cmd.Id, ct);
        if (node is null)
            return Result<CategoryNodeDto>.NotFound("Nodo no encontrado.");

        var nc = cmd.Code.Trim().ToUpperInvariant();
        if (nc != node.Code && await _repo.CodeExistsAsync(nc, _tenant.TenantId, cmd.Id, ct))
            return Result<CategoryNodeDto>.Conflict($"Código '{nc}' ya existe.");

        node.Update(cmd.Code, cmd.Name, _user.UserId, cmd.Description, cmd.SortOrder);
        await _repo.SaveChangesAsync(ct);

        return Result<CategoryNodeDto>.Success(CategoryNodeMapping.ToDto(node));
    }
}

public sealed class DisableCategoryNodeCommandHandler
    : IRequestHandler<DisableCategoryNodeCommand, Result<bool>>
{
    private readonly ICategoryNodeRepository _repo;
    private readonly ICurrentUser _user;

    public DisableCategoryNodeCommandHandler(ICategoryNodeRepository repo, ICurrentUser user)
    {
        _repo = repo;
        _user = user;
    }

    public async Task<Result<bool>> Handle(DisableCategoryNodeCommand cmd, CancellationToken ct)
    {
        var node = await _repo.GetByIdAsync(cmd.Id, ct);
        if (node is null)
            return Result<bool>.NotFound("Nodo no encontrado.");

        var items = await _repo.CountItemsByNodeAsync(cmd.Id, ct);
        if (items > 0)
            return Result<bool>.ValidationFailure(
                $"Tiene {items} ítem(s) asignado(s). Reasígnelos primero."
            );

        try
        {
            node.Disable(_user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class EnableCategoryNodeCommandHandler
    : IRequestHandler<EnableCategoryNodeCommand, Result<bool>>
{
    private readonly ICategoryNodeRepository _repo;
    private readonly ICurrentUser _user;

    public EnableCategoryNodeCommandHandler(ICategoryNodeRepository repo, ICurrentUser user)
    {
        _repo = repo;
        _user = user;
    }

    public async Task<Result<bool>> Handle(EnableCategoryNodeCommand cmd, CancellationToken ct)
    {
        var node = await _repo.GetByIdAsync(cmd.Id, ct);
        if (node is null)
            return Result<bool>.NotFound("Nodo no encontrado.");

        if (node.ParentId.HasValue)
        {
            var parent = await _repo.GetByIdAsync(node.ParentId.Value, ct);
            if (parent is not null && !parent.IsActive)
                return Result<bool>.ValidationFailure(
                    "No se puede activar un nodo cuyo padre está inactivo."
                );
        }

        try
        {
            node.Enable(_user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetCategoryTreeQueryHandler
    : IRequestHandler<GetCategoryTreeQuery, Result<CategoryTreeDto>>
{
    private readonly ICategoryNodeRepository _repo;
    private readonly IOrgSettingsRepository _orgRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public GetCategoryTreeQueryHandler(
        ICategoryNodeRepository repo,
        IOrgSettingsRepository orgRepo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _repo = repo;
        _orgRepo = orgRepo;
        _tenant = tenant;
        _company = company;
    }

    public async Task<Result<CategoryTreeDto>> Handle(
        GetCategoryTreeQuery query,
        CancellationToken ct
    )
    {
        var tenantId = _tenant.TenantId;
        var nodes = await _repo.GetAllAsync(tenantId, query.IncludeInactive, ct);
        var dtos = nodes.Select(CategoryNodeMapping.ToDto).ToList();
        var maxDepth = await CategoryDepthResolver.ResolveMaxDepthAsync(
            _orgRepo,
            tenantId,
            _company.CompanyId,
            ct
        );
        return Result<CategoryTreeDto>.Success(new CategoryTreeDto(dtos, maxDepth));
    }
}

public sealed class GetCategoryNodeByIdQueryHandler
    : IRequestHandler<GetCategoryNodeByIdQuery, Result<CategoryNodeDto>>
{
    private readonly ICategoryNodeRepository _repo;

    public GetCategoryNodeByIdQueryHandler(ICategoryNodeRepository repo) => _repo = repo;

    public async Task<Result<CategoryNodeDto>> Handle(
        GetCategoryNodeByIdQuery query,
        CancellationToken ct
    )
    {
        var node = await _repo.GetByIdAsync(query.Id, ct);
        return node is null
            ? Result<CategoryNodeDto>.NotFound("Nodo no encontrado.")
            : Result<CategoryNodeDto>.Success(CategoryNodeMapping.ToDto(node));
    }
}

// ══════════════════════════════════════════════════════════════════════════
// MAPPING
// ══════════════════════════════════════════════════════════════════════════

internal static class CategoryNodeMapping
{
    public static CategoryNodeDto ToDto(ItemCategoryNode n) =>
        new(
            n.Id,
            n.ParentId,
            n.Code,
            n.Name,
            n.Description,
            n.Level.ToString(),
            n.Path,
            CategoryDepthResolver.DepthOf(n.Path),
            n.SortOrder,
            n.IsActive,
            n.CreatedAt,
            n.UpdatedAt
        );
}
