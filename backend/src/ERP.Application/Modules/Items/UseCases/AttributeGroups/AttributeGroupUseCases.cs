using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;

namespace ERP.Application.Items.UseCases.AttributeGroups;

public sealed record CreateAttributeGroupCommand(string Code, string Name, int SortOrder = 0) : IRequest<Result<AttributeGroupDto>>, ICompanyScopedRequest;
public sealed record UpdateAttributeGroupCommand(Guid Id, string Name, int SortOrder = 0) : IRequest<Result<AttributeGroupDto>>, ICompanyScopedRequest;
public sealed record EnableAttributeGroupCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
public sealed record DisableAttributeGroupCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
public sealed record GetAttributeGroupsQuery(bool? IsActive = null) : IRequest<Result<IReadOnlyList<AttributeGroupDto>>>, ICompanyScopedRequest;
public sealed record GetAttributeGroupByIdQuery(Guid Id) : IRequest<Result<AttributeGroupDto>>, ICompanyScopedRequest;

public sealed class CreateAttributeGroupCommandValidator : AbstractValidator<CreateAttributeGroupCommand>
{
    public CreateAttributeGroupCommandValidator() { RuleFor(x => x.Code).NotEmpty().MaximumLength(50); RuleFor(x => x.Name).NotEmpty().MaximumLength(100); RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0); }
}
public sealed class UpdateAttributeGroupCommandValidator : AbstractValidator<UpdateAttributeGroupCommand>
{
    public UpdateAttributeGroupCommandValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(100); RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0); }
}

public interface IAttributeGroupRepository
{
    Task<IReadOnlyList<AttributeGroup>> GetAllAsync(Guid subscriberId, CancellationToken ct = default);
    Task<AttributeGroup?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid subscriberId, CancellationToken ct = default);
    Task AddAsync(AttributeGroup group, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed class CreateAttributeGroupCommandHandler : IRequestHandler<CreateAttributeGroupCommand, Result<AttributeGroupDto>>
{
    private readonly IAttributeGroupRepository _r; private readonly ICurrentSubscriber _s; private readonly ICurrentUser _u;
    public CreateAttributeGroupCommandHandler(IAttributeGroupRepository r, ICurrentSubscriber s, ICurrentUser u) { _r=r;_s=s;_u=u; }
    public async Task<Result<AttributeGroupDto>> Handle(CreateAttributeGroupCommand cmd, CancellationToken ct)
    {
        var sid = _s.SubscriberId;
        if (await _r.ExistsByCodeAsync(cmd.Code, sid, ct)) return Result<AttributeGroupDto>.Conflict($"Código '{cmd.Code}' duplicado.");
        var g = AttributeGroup.Create(sid, cmd.Code, cmd.Name, cmd.SortOrder);
        await _r.AddAsync(g, ct); await _r.SaveChangesAsync(ct);
        return Result<AttributeGroupDto>.Success(new(g.Id, g.Code, g.Name, g.SortOrder, g.IsActive));
    }
}

public sealed class UpdateAttributeGroupCommandHandler : IRequestHandler<UpdateAttributeGroupCommand, Result<AttributeGroupDto>>
{
    private readonly IAttributeGroupRepository _r; private readonly ICurrentSubscriber _s; private readonly ICurrentUser _u;
    public UpdateAttributeGroupCommandHandler(IAttributeGroupRepository r, ICurrentSubscriber s, ICurrentUser u) { _r=r;_s=s;_u=u; }
    public async Task<Result<AttributeGroupDto>> Handle(UpdateAttributeGroupCommand cmd, CancellationToken ct)
    {
        var g = await _r.GetByIdAsync(cmd.Id, _s.SubscriberId, ct); if (g is null) return Result<AttributeGroupDto>.NotFound("No encontrado.");
        g.Update(cmd.Name, cmd.SortOrder, _u.UserId); await _r.SaveChangesAsync(ct);
        return Result<AttributeGroupDto>.Success(new(g.Id, g.Code, g.Name, g.SortOrder, g.IsActive));
    }
}

public sealed class EnableAttributeGroupCommandHandler : IRequestHandler<EnableAttributeGroupCommand, Result<bool>>
{
    private readonly IAttributeGroupRepository _r; private readonly ICurrentSubscriber _s; private readonly ICurrentUser _u;
    public EnableAttributeGroupCommandHandler(IAttributeGroupRepository r, ICurrentSubscriber s, ICurrentUser u) { _r=r;_s=s;_u=u; }
    public async Task<Result<bool>> Handle(EnableAttributeGroupCommand cmd, CancellationToken ct)
    {
        var g = await _r.GetByIdAsync(cmd.Id, _s.SubscriberId, ct); if (g is null) return Result<bool>.NotFound("No encontrado.");
        try { g.Enable(_u.UserId); } catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }
        await _r.SaveChangesAsync(ct); return Result<bool>.Success(true);
    }
}

public sealed class DisableAttributeGroupCommandHandler : IRequestHandler<DisableAttributeGroupCommand, Result<bool>>
{
    private readonly IAttributeGroupRepository _r; private readonly ICurrentSubscriber _s; private readonly ICurrentUser _u;
    public DisableAttributeGroupCommandHandler(IAttributeGroupRepository r, ICurrentSubscriber s, ICurrentUser u) { _r=r;_s=s;_u=u; }
    public async Task<Result<bool>> Handle(DisableAttributeGroupCommand cmd, CancellationToken ct)
    {
        var g = await _r.GetByIdAsync(cmd.Id, _s.SubscriberId, ct); if (g is null) return Result<bool>.NotFound("No encontrado.");
        try { g.Disable(_u.UserId); } catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }
        await _r.SaveChangesAsync(ct); return Result<bool>.Success(true);
    }
}

public sealed class GetAttributeGroupsQueryHandler : IRequestHandler<GetAttributeGroupsQuery, Result<IReadOnlyList<AttributeGroupDto>>>
{
    private readonly IAttributeGroupRepository _r; private readonly ICurrentSubscriber _s;
    public GetAttributeGroupsQueryHandler(IAttributeGroupRepository r, ICurrentSubscriber s) { _r=r;_s=s; }
    public async Task<Result<IReadOnlyList<AttributeGroupDto>>> Handle(GetAttributeGroupsQuery q, CancellationToken ct)
    {
        var all = await _r.GetAllAsync(_s.SubscriberId, ct);
        var items = (q.IsActive.HasValue ? all.Where(g => g.IsActive == q.IsActive.Value) : all)
            .Select(g => new AttributeGroupDto(g.Id, g.Code, g.Name, g.SortOrder, g.IsActive)).ToList();
        return Result<IReadOnlyList<AttributeGroupDto>>.Success(items);
    }
}

public sealed class GetAttributeGroupByIdQueryHandler : IRequestHandler<GetAttributeGroupByIdQuery, Result<AttributeGroupDto>>
{
    private readonly IAttributeGroupRepository _r; private readonly ICurrentSubscriber _s;
    public GetAttributeGroupByIdQueryHandler(IAttributeGroupRepository r, ICurrentSubscriber s) { _r=r;_s=s; }
    public async Task<Result<AttributeGroupDto>> Handle(GetAttributeGroupByIdQuery q, CancellationToken ct)
    {
        var g = await _r.GetByIdAsync(q.Id, _s.SubscriberId, ct);
        return g is null ? Result<AttributeGroupDto>.NotFound("No encontrado.") : Result<AttributeGroupDto>.Success(new(g.Id, g.Code, g.Name, g.SortOrder, g.IsActive));
    }
}
