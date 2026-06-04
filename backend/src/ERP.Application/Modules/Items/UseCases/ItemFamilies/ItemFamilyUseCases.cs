using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.ItemFamilies;

public sealed record CreateItemFamilyCommand(string Code, string Name)
    : IRequest<Result<ItemFamilyDto>>, ICompanyScopedRequest;
public sealed record UpdateItemFamilyCommand(Guid Id, string Code, string Name)
    : IRequest<Result<ItemFamilyDto>>, ICompanyScopedRequest;
public sealed record EnableItemFamilyCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
public sealed record DisableItemFamilyCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
public sealed record GetItemFamilyByIdQuery(Guid Id) : IRequest<Result<ItemFamilyDto>>, ICompanyScopedRequest;
public sealed record GetItemFamiliesQuery(bool? IsActive = null) : IRequest<Result<IReadOnlyList<ItemFamilyDto>>>, ICompanyScopedRequest;

public sealed class CreateItemFamilyCommandValidator : AbstractValidator<CreateItemFamilyCommand>
{
    public CreateItemFamilyCommandValidator() { RuleFor(x => x.Code).NotEmpty().MaximumLength(20); RuleFor(x => x.Name).NotEmpty().MaximumLength(120); }
}
public sealed class UpdateItemFamilyCommandValidator : AbstractValidator<UpdateItemFamilyCommand>
{
    public UpdateItemFamilyCommandValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(20); RuleFor(x => x.Name).NotEmpty().MaximumLength(120); }
}

public sealed class CreateItemFamilyCommandHandler : IRequestHandler<CreateItemFamilyCommand, Result<ItemFamilyDto>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentSubscriber _s; private readonly ICurrentUser _u;
    public CreateItemFamilyCommandHandler(IItemCatalogRepository c, ICurrentSubscriber s, ICurrentUser u) { _c=c;_s=s;_u=u; }
    public async Task<Result<ItemFamilyDto>> Handle(CreateItemFamilyCommand cmd, CancellationToken ct)
    {
        var sid = _s.SubscriberId;
        if (await _c.FamilyCodeExistsAsync(cmd.Code, sid, ct)) return Result<ItemFamilyDto>.Conflict($"Código '{cmd.Code}' duplicado.");
        ItemFamily f; try { f = ItemFamily.Create(sid, cmd.Code, cmd.Name, _u.UserId); } catch (ArgumentException ex) { return Result<ItemFamilyDto>.ValidationFailure(ex.Message); }
        await _c.AddFamilyAsync(f, ct); await _c.SaveChangesAsync(ct);
        return Result<ItemFamilyDto>.Success(new(f.Id, f.Code, f.Name, f.IsActive, f.CreatedAt, f.UpdatedAt));
    }
}

public sealed class UpdateItemFamilyCommandHandler : IRequestHandler<UpdateItemFamilyCommand, Result<ItemFamilyDto>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentSubscriber _s; private readonly ICurrentUser _u;
    public UpdateItemFamilyCommandHandler(IItemCatalogRepository c, ICurrentSubscriber s, ICurrentUser u) { _c=c;_s=s;_u=u; }
    public async Task<Result<ItemFamilyDto>> Handle(UpdateItemFamilyCommand cmd, CancellationToken ct)
    {
        var f = await _c.GetFamilyByIdAsync(cmd.Id, ct);
        if (f is null) return Result<ItemFamilyDto>.NotFound("Familia no encontrada.");
        var nc = cmd.Code.Trim().ToUpperInvariant();
        if (nc != f.Code && await _c.FamilyCodeExistsAsync(nc, f.SubscriberId, ct)) return Result<ItemFamilyDto>.Conflict($"Código '{nc}' duplicado.");
        try { f.Update(cmd.Code, cmd.Name, _u.UserId); } catch (ArgumentException ex) { return Result<ItemFamilyDto>.ValidationFailure(ex.Message); }
        await _c.SaveChangesAsync(ct);
        return Result<ItemFamilyDto>.Success(new(f.Id, f.Code, f.Name, f.IsActive, f.CreatedAt, f.UpdatedAt));
    }
}

public sealed class EnableItemFamilyCommandHandler : IRequestHandler<EnableItemFamilyCommand, Result<bool>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentUser _u;
    public EnableItemFamilyCommandHandler(IItemCatalogRepository c, ICurrentUser u) { _c=c;_u=u; }
    public async Task<Result<bool>> Handle(EnableItemFamilyCommand cmd, CancellationToken ct)
    {
        var f = await _c.GetFamilyByIdAsync(cmd.Id, ct); if (f is null) return Result<bool>.NotFound("No encontrada.");
        try { f.Enable(_u.UserId); } catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }
        await _c.SaveChangesAsync(ct); return Result<bool>.Success(true);
    }
}

public sealed class DisableItemFamilyCommandHandler : IRequestHandler<DisableItemFamilyCommand, Result<bool>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentUser _u;
    public DisableItemFamilyCommandHandler(IItemCatalogRepository c, ICurrentUser u) { _c=c;_u=u; }
    public async Task<Result<bool>> Handle(DisableItemFamilyCommand cmd, CancellationToken ct)
    {
        var f = await _c.GetFamilyByIdAsync(cmd.Id, ct); if (f is null) return Result<bool>.NotFound("No encontrada.");
        try { f.Disable(_u.UserId); } catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }
        await _c.SaveChangesAsync(ct); return Result<bool>.Success(true);
    }
}

public sealed class GetItemFamilyByIdQueryHandler : IRequestHandler<GetItemFamilyByIdQuery, Result<ItemFamilyDto>>
{
    private readonly IItemCatalogRepository _c;
    public GetItemFamilyByIdQueryHandler(IItemCatalogRepository c) => _c = c;
    public async Task<Result<ItemFamilyDto>> Handle(GetItemFamilyByIdQuery q, CancellationToken ct)
    {
        var f = await _c.GetFamilyByIdAsync(q.Id, ct);
        return f is null ? Result<ItemFamilyDto>.NotFound("No encontrada.") : Result<ItemFamilyDto>.Success(new(f.Id, f.Code, f.Name, f.IsActive, f.CreatedAt, f.UpdatedAt));
    }
}

public sealed class GetItemFamiliesQueryHandler : IRequestHandler<GetItemFamiliesQuery, Result<IReadOnlyList<ItemFamilyDto>>>
{
    private readonly IItemCatalogRepository _c; private readonly ICurrentSubscriber _s;
    public GetItemFamiliesQueryHandler(IItemCatalogRepository c, ICurrentSubscriber s) { _c=c;_s=s; }
    public async Task<Result<IReadOnlyList<ItemFamilyDto>>> Handle(GetItemFamiliesQuery q, CancellationToken ct)
    {
        var all = await _c.GetFamiliesAsync(_s.SubscriberId, ct);
        var items = (q.IsActive.HasValue ? all.Where(f => f.IsActive == q.IsActive.Value) : all)
            .Select(f => new ItemFamilyDto(f.Id, f.Code, f.Name, f.IsActive, f.CreatedAt, f.UpdatedAt)).ToList();
        return Result<IReadOnlyList<ItemFamilyDto>>.Success(items);
    }
}
