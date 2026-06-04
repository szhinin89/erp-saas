using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.ItemSubcategories;

public sealed record CreateItemSubcategoryCommand(Guid CategoryId, string Code, string Name) : IRequest<Result<ItemSubcategoryDto>>, ICompanyScopedRequest;
public sealed record UpdateItemSubcategoryCommand(Guid Id, string Code, string Name) : IRequest<Result<ItemSubcategoryDto>>, ICompanyScopedRequest;
public sealed record EnableItemSubcategoryCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
public sealed record DisableItemSubcategoryCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
public sealed record GetItemSubcategoryByIdQuery(Guid Id) : IRequest<Result<ItemSubcategoryDto>>, ICompanyScopedRequest;
public sealed record GetItemSubcategoriesQuery(Guid? CategoryId = null, bool? IsActive = null) : IRequest<Result<IReadOnlyList<ItemSubcategoryDto>>>, ICompanyScopedRequest;

public sealed class CreateItemSubcategoryCommandValidator : AbstractValidator<CreateItemSubcategoryCommand>
{
    public CreateItemSubcategoryCommandValidator() { RuleFor(x => x.CategoryId).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(20); RuleFor(x => x.Name).NotEmpty().MaximumLength(120); }
}
public sealed class UpdateItemSubcategoryCommandValidator : AbstractValidator<UpdateItemSubcategoryCommand>
{
    public UpdateItemSubcategoryCommandValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(20); RuleFor(x => x.Name).NotEmpty().MaximumLength(120); }
}

public sealed class CreateItemSubcategoryCommandHandler : IRequestHandler<CreateItemSubcategoryCommand, Result<ItemSubcategoryDto>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentSubscriber _s; private readonly ICurrentUser _u;
    public CreateItemSubcategoryCommandHandler(IItemCatalogRepository c, ICurrentSubscriber s, ICurrentUser u) { _c=c;_s=s;_u=u; }
    public async Task<Result<ItemSubcategoryDto>> Handle(CreateItemSubcategoryCommand cmd, CancellationToken ct)
    {
        var sid = _s.SubscriberId;
        var cat = await _c.GetCategoryByIdAsync(cmd.CategoryId, ct);
        if (cat is null) return Result<ItemSubcategoryDto>.NotFound("Categoría no encontrada.");
        if (!cat.IsActive) return Result<ItemSubcategoryDto>.ValidationFailure("La categoría está deshabilitada.");
        if (await _c.SubcategoryCodeExistsAsync(cmd.Code, cmd.CategoryId, sid, ct)) return Result<ItemSubcategoryDto>.Conflict($"Código '{cmd.Code}' duplicado.");
        ItemSubcategory sub; try { sub = ItemSubcategory.Create(sid, cmd.Code, cmd.Name, cmd.CategoryId, _u.UserId); } catch (ArgumentException ex) { return Result<ItemSubcategoryDto>.ValidationFailure(ex.Message); }
        await _c.AddSubcategoryAsync(sub, ct); await _c.SaveChangesAsync(ct);
        return Result<ItemSubcategoryDto>.Success(new(sub.Id, sub.CategoryId, sub.Code, sub.Name, sub.IsActive, sub.CreatedAt, sub.UpdatedAt));
    }
}

public sealed class UpdateItemSubcategoryCommandHandler : IRequestHandler<UpdateItemSubcategoryCommand, Result<ItemSubcategoryDto>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentUser _u;
    public UpdateItemSubcategoryCommandHandler(IItemCatalogRepository c, ICurrentUser u) { _c=c;_u=u; }
    public async Task<Result<ItemSubcategoryDto>> Handle(UpdateItemSubcategoryCommand cmd, CancellationToken ct)
    {
        var sub = await _c.GetSubcategoryByIdAsync(cmd.Id, ct); if (sub is null) return Result<ItemSubcategoryDto>.NotFound("No encontrada.");
        var nc = cmd.Code.Trim().ToUpperInvariant();
        if (nc != sub.Code && await _c.SubcategoryCodeExistsAsync(nc, sub.CategoryId, sub.SubscriberId, ct)) return Result<ItemSubcategoryDto>.Conflict($"Código '{nc}' duplicado.");
        try { sub.Update(cmd.Code, cmd.Name, _u.UserId); } catch (ArgumentException ex) { return Result<ItemSubcategoryDto>.ValidationFailure(ex.Message); }
        await _c.SaveChangesAsync(ct);
        return Result<ItemSubcategoryDto>.Success(new(sub.Id, sub.CategoryId, sub.Code, sub.Name, sub.IsActive, sub.CreatedAt, sub.UpdatedAt));
    }
}

public sealed class EnableItemSubcategoryCommandHandler : IRequestHandler<EnableItemSubcategoryCommand, Result<bool>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentUser _u;
    public EnableItemSubcategoryCommandHandler(IItemCatalogRepository c, ICurrentUser u) { _c=c;_u=u; }
    public async Task<Result<bool>> Handle(EnableItemSubcategoryCommand cmd, CancellationToken ct)
    {
        var x = await _c.GetSubcategoryByIdAsync(cmd.Id, ct); if (x is null) return Result<bool>.NotFound("No encontrada.");
        try { x.Enable(_u.UserId); } catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }
        await _c.SaveChangesAsync(ct); return Result<bool>.Success(true);
    }
}

public sealed class DisableItemSubcategoryCommandHandler : IRequestHandler<DisableItemSubcategoryCommand, Result<bool>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentUser _u;
    public DisableItemSubcategoryCommandHandler(IItemCatalogRepository c, ICurrentUser u) { _c=c;_u=u; }
    public async Task<Result<bool>> Handle(DisableItemSubcategoryCommand cmd, CancellationToken ct)
    {
        var x = await _c.GetSubcategoryByIdAsync(cmd.Id, ct); if (x is null) return Result<bool>.NotFound("No encontrada.");
        try { x.Disable(_u.UserId); } catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }
        await _c.SaveChangesAsync(ct); return Result<bool>.Success(true);
    }
}

public sealed class GetItemSubcategoryByIdQueryHandler : IRequestHandler<GetItemSubcategoryByIdQuery, Result<ItemSubcategoryDto>>
{
    private readonly IItemCatalogRepository _c;
    public GetItemSubcategoryByIdQueryHandler(IItemCatalogRepository c) => _c = c;
    public async Task<Result<ItemSubcategoryDto>> Handle(GetItemSubcategoryByIdQuery q, CancellationToken ct)
    {
        var x = await _c.GetSubcategoryByIdAsync(q.Id, ct);
        return x is null ? Result<ItemSubcategoryDto>.NotFound("No encontrada.") : Result<ItemSubcategoryDto>.Success(new(x.Id, x.CategoryId, x.Code, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt));
    }
}

public sealed class GetItemSubcategoriesQueryHandler : IRequestHandler<GetItemSubcategoriesQuery, Result<IReadOnlyList<ItemSubcategoryDto>>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentSubscriber _s;
    public GetItemSubcategoriesQueryHandler(IItemCatalogRepository c, ICurrentSubscriber s) { _c=c;_s=s; }
    public async Task<Result<IReadOnlyList<ItemSubcategoryDto>>> Handle(GetItemSubcategoriesQuery q, CancellationToken ct)
    {
        var all = await _c.GetSubcategoriesAsync(_s.SubscriberId, q.CategoryId, ct);
        var items = (q.IsActive.HasValue ? all.Where(x => x.IsActive == q.IsActive.Value) : all)
            .Select(x => new ItemSubcategoryDto(x.Id, x.CategoryId, x.Code, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToList();
        return Result<IReadOnlyList<ItemSubcategoryDto>>.Success(items);
    }
}
