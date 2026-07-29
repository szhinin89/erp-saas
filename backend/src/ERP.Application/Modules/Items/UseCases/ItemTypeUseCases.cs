using ERP.Application.Common;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Items.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

public sealed record ItemTypeDto(Guid Id, string Code, string Name, bool IsActive, int SortOrder);

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetItemTypesQuery(bool OnlyActive = true)
    : IRequest<Result<IReadOnlyList<ItemTypeDto>>>,
        ICompanyScopedRequest;

public sealed record GetItemTypeByIdQuery(Guid Id)
    : IRequest<Result<ItemTypeDto>>,
        ICompanyScopedRequest;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record CreateItemTypeCommand(string Code, string Name, int SortOrder = 0)
    : IRequest<Result<ItemTypeDto>>,
        ICompanyScopedRequest;

public sealed record UpdateItemTypeCommand(Guid Id, string Name, int SortOrder)
    : IRequest<Result<ItemTypeDto>>,
        ICompanyScopedRequest;

public sealed record ToggleItemTypeCommand(Guid Id)
    : IRequest<Result<ItemTypeDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateItemTypeValidator : AbstractValidator<CreateItemTypeCommand>
{
    public CreateItemTypeValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(ItemTypeDefinition.MaxCodeLength)
            .WithMessage("El código es obligatorio (máx 30 caracteres).");
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(ItemTypeDefinition.MaxNameLength)
            .WithMessage("El nombre es obligatorio (máx 100 caracteres).");
    }
}

public sealed class UpdateItemTypeValidator : AbstractValidator<UpdateItemTypeCommand>
{
    public UpdateItemTypeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ItemTypeDefinition.MaxNameLength);
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetItemTypesHandler
    : IRequestHandler<GetItemTypesQuery, Result<IReadOnlyList<ItemTypeDto>>>
{
    private readonly IItemTypeRepository _repo;
    private readonly ICurrentTenant _t;

    public GetItemTypesHandler(IItemTypeRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<ItemTypeDto>>> Handle(
        GetItemTypesQuery q,
        CancellationToken ct
    )
    {
        var list = await _repo.ListAsync(_t.TenantId, q.OnlyActive, ct);
        var dtos = list.Select(ToDto).ToList();
        return Result<IReadOnlyList<ItemTypeDto>>.Success(dtos);
    }

    internal static ItemTypeDto ToDto(ItemTypeDefinition x) =>
        new(x.Id, x.Code, x.Name, x.IsActive, x.SortOrder);
}

public sealed class GetItemTypeByIdHandler
    : IRequestHandler<GetItemTypeByIdQuery, Result<ItemTypeDto>>
{
    private readonly IItemTypeRepository _repo;
    private readonly ICurrentTenant _t;

    public GetItemTypeByIdHandler(IItemTypeRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<ItemTypeDto>> Handle(GetItemTypeByIdQuery q, CancellationToken ct)
    {
        var x = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return x is null
            ? Result<ItemTypeDto>.NotFound("Tipo de ítem no encontrado.")
            : Result<ItemTypeDto>.Success(GetItemTypesHandler.ToDto(x));
    }
}

public sealed class CreateItemTypeHandler
    : IRequestHandler<CreateItemTypeCommand, Result<ItemTypeDto>>
{
    private readonly IItemTypeRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CreateItemTypeHandler(IItemTypeRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<ItemTypeDto>> Handle(CreateItemTypeCommand cmd, CancellationToken ct)
    {
        var tid = _t.TenantId;
        if (await _repo.ExistsByCodeAsync(tid, cmd.Code, null, ct))
            return Result<ItemTypeDto>.ValidationFailure(
                $"Ya existe un tipo de ítem con código '{cmd.Code}'."
            );

        var entity = ItemTypeDefinition.Create(tid, cmd.Code, cmd.Name, cmd.SortOrder, _u.UserId);

        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ItemTypeDto>.Success(GetItemTypesHandler.ToDto(entity));
    }
}

public sealed class UpdateItemTypeHandler
    : IRequestHandler<UpdateItemTypeCommand, Result<ItemTypeDto>>
{
    private readonly IItemTypeRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdateItemTypeHandler(IItemTypeRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<ItemTypeDto>> Handle(UpdateItemTypeCommand cmd, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (entity is null)
            return Result<ItemTypeDto>.NotFound("Tipo de ítem no encontrado.");

        entity.Update(cmd.Name, cmd.SortOrder, _u.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<ItemTypeDto>.Success(GetItemTypesHandler.ToDto(entity));
    }
}

public sealed class ToggleItemTypeHandler
    : IRequestHandler<ToggleItemTypeCommand, Result<ItemTypeDto>>
{
    private readonly IItemTypeRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public ToggleItemTypeHandler(IItemTypeRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<ItemTypeDto>> Handle(ToggleItemTypeCommand cmd, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (entity is null)
            return Result<ItemTypeDto>.NotFound("Tipo de ítem no encontrado.");

        if (entity.IsActive)
        {
            if (await _repo.HasActiveItemsAsync(_t.TenantId, entity.Code, ct))
                return Result<ItemTypeDto>.ValidationFailure(
                    "No se puede desactivar: existen ítems activos con este tipo."
                );

            entity.Disable(_u.UserId);
        }
        else
        {
            entity.Enable(_u.UserId);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<ItemTypeDto>.Success(GetItemTypesHandler.ToDto(entity));
    }
}
