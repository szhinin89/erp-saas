using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.ItemCategories;

public sealed record CreateItemCategoryCommand(Guid FamilyId, string Code, string Name) : IRequest<Result<ItemCategoryDto>>, ICompanyScopedRequest;
public sealed record UpdateItemCategoryCommand(Guid Id, string Code, string Name) : IRequest<Result<ItemCategoryDto>>, ICompanyScopedRequest;
public sealed record EnableItemCategoryCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
public sealed record DisableItemCategoryCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
public sealed record GetItemCategoryByIdQuery(Guid Id) : IRequest<Result<ItemCategoryDto>>, ICompanyScopedRequest;
public sealed record GetItemCategoriesQuery(Guid? FamilyId = null, bool? IsActive = null) : IRequest<Result<IReadOnlyList<ItemCategoryDto>>>, ICompanyScopedRequest;

public sealed class CreateItemCategoryCommandValidator : AbstractValidator<CreateItemCategoryCommand>
{
    public CreateItemCategoryCommandValidator() { RuleFor(x => x.FamilyId).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(20); RuleFor(x => x.Name).NotEmpty().MaximumLength(120); }
}
public sealed class UpdateItemCategoryCommandValidator : AbstractValidator<UpdateItemCategoryCommand>
{
    public UpdateItemCategoryCommandValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(20); RuleFor(x => x.Name).NotEmpty().MaximumLength(120); }
}

public sealed class CreateItemCategoryCommandHandler : IRequestHandler<CreateItemCategoryCommand, Result<ItemCategoryDto>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentSubscriber _s; private readonly ICurrentUser _u;
    public CreateItemCategoryCommandHandler(IItemCatalogRepository c, ICurrentSubscriber s, ICurrentUser u) { _c=c;_s=s;_u=u; }
    public async Task<Result<ItemCategoryDto>> Handle(CreateItemCategoryCommand cmd, CancellationToken ct)
    {
        var sid = _s.SubscriberId;
        var fam = await _c.GetFamilyByIdAsync(cmd.FamilyId, ct);
        if (fam is null) return Result<ItemCategoryDto>.NotFound("Familia no encontrada.");
        if (!fam.IsActive) return Result<ItemCategoryDto>.ValidationFailure("La familia está deshabilitada.");
        if (await _c.CategoryCodeExistsAsync(cmd.Code, cmd.FamilyId, sid, ct)) return Result<ItemCategoryDto>.Conflict($"Código '{cmd.Code}' duplicado.");
        ItemCategory cat; try { cat = ItemCategory.Create(sid, cmd.Code, cmd.Name, cmd.FamilyId, _u.UserId); } catch (ArgumentException ex) { return Result<ItemCategoryDto>.ValidationFailure(ex.Message); }
        await _c.AddCategoryAsync(cat, ct); await _c.SaveChangesAsync(ct);
        return Result<ItemCategoryDto>.Success(new(cat.Id, cat.FamilyId, cat.Code, cat.Name, cat.IsActive, cat.CreatedAt, cat.UpdatedAt));
    }
}

public sealed class UpdateItemCategoryCommandHandler : IRequestHandler<UpdateItemCategoryCommand, Result<ItemCategoryDto>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentUser _u;
    public UpdateItemCategoryCommandHandler(IItemCatalogRepository c, ICurrentUser u) { _c=c;_u=u; }
    public async Task<Result<ItemCategoryDto>> Handle(UpdateItemCategoryCommand cmd, CancellationToken ct)
    {
        var cat = await _c.GetCategoryByIdAsync(cmd.Id, ct); if (cat is null) return Result<ItemCategoryDto>.NotFound("Categoría no encontrada.");
        var nc = cmd.Code.Trim().ToUpperInvariant();
        if (nc != cat.Code && await _c.CategoryCodeExistsAsync(nc, cat.FamilyId, cat.SubscriberId, ct)) return Result<ItemCategoryDto>.Conflict($"Código '{nc}' duplicado.");
        try { cat.Update(cmd.Code, cmd.Name, _u.UserId); } catch (ArgumentException ex) { return Result<ItemCategoryDto>.ValidationFailure(ex.Message); }
        await _c.SaveChangesAsync(ct);
        return Result<ItemCategoryDto>.Success(new(cat.Id, cat.FamilyId, cat.Code, cat.Name, cat.IsActive, cat.CreatedAt, cat.UpdatedAt));
    }
}

public sealed class EnableItemCategoryCommandHandler : IRequestHandler<EnableItemCategoryCommand, Result<bool>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentUser _u;
    public EnableItemCategoryCommandHandler(IItemCatalogRepository c, ICurrentUser u) { _c=c;_u=u; }
    public async Task<Result<bool>> Handle(EnableItemCategoryCommand cmd, CancellationToken ct)
    {
        var x = await _c.GetCategoryByIdAsync(cmd.Id, ct); if (x is null) return Result<bool>.NotFound("No encontrada.");
        try { x.Enable(_u.UserId); } catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }
        await _c.SaveChangesAsync(ct); return Result<bool>.Success(true);
    }
}

public sealed class DisableItemCategoryCommandHandler : IRequestHandler<DisableItemCategoryCommand, Result<bool>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentUser _u;
    public DisableItemCategoryCommandHandler(IItemCatalogRepository c, ICurrentUser u) { _c=c;_u=u; }
    public async Task<Result<bool>> Handle(DisableItemCategoryCommand cmd, CancellationToken ct)
    {
        var x = await _c.GetCategoryByIdAsync(cmd.Id, ct); if (x is null) return Result<bool>.NotFound("No encontrada.");
        var n = await _c.CountActiveSubcategoriesByCategoryAsync(cmd.Id, ct);
        if (n > 0) return Result<bool>.ValidationFailure($"Tiene {n} subcategoría(s) activa(s).");
        try { x.Disable(_u.UserId); } catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }
        await _c.SaveChangesAsync(ct); return Result<bool>.Success(true);
    }
}

public sealed class GetItemCategoryByIdQueryHandler : IRequestHandler<GetItemCategoryByIdQuery, Result<ItemCategoryDto>>
{
    private readonly IItemCatalogRepository _c;
    public GetItemCategoryByIdQueryHandler(IItemCatalogRepository c) => _c = c;
    public async Task<Result<ItemCategoryDto>> Handle(GetItemCategoryByIdQuery q, CancellationToken ct)
    {
        var x = await _c.GetCategoryByIdAsync(q.Id, ct);
        return x is null ? Result<ItemCategoryDto>.NotFound("No encontrada.") : Result<ItemCategoryDto>.Success(new(x.Id, x.FamilyId, x.Code, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt));
    }
}

public sealed class GetItemCategoriesQueryHandler : IRequestHandler<GetItemCategoriesQuery, Result<IReadOnlyList<ItemCategoryDto>>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentSubscriber _s;
    public GetItemCategoriesQueryHandler(IItemCatalogRepository c, ICurrentSubscriber s) { _c=c;_s=s; }
    public async Task<Result<IReadOnlyList<ItemCategoryDto>>> Handle(GetItemCategoriesQuery q, CancellationToken ct)
    {
        var all = await _c.GetCategoriesAsync(_s.SubscriberId, q.FamilyId, ct);
        var items = (q.IsActive.HasValue ? all.Where(x => x.IsActive == q.IsActive.Value) : all)
            .Select(x => new ItemCategoryDto(x.Id, x.FamilyId, x.Code, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToList();
        return Result<IReadOnlyList<ItemCategoryDto>>.Success(items);
    }
}
