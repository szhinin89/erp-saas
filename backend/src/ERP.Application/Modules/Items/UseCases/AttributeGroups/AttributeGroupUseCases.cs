using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.AttributeGroups;

public sealed record CreateAttributeGroupCommand(string Code, string Name, int SortOrder = 0)
    : IRequest<Result<AttributeGroupDto>>,
        ICompanyScopedRequest;

public sealed record UpdateAttributeGroupCommand(Guid Id, string Name, int SortOrder = 0)
    : IRequest<Result<AttributeGroupDto>>,
        ICompanyScopedRequest;

public sealed record EnableAttributeGroupCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed record DisableAttributeGroupCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed record GetAttributeGroupsQuery(bool? IsActive = null)
    : IRequest<Result<IReadOnlyList<AttributeGroupDto>>>,
        ICompanyScopedRequest;

public sealed record GetAttributeGroupByIdQuery(Guid Id)
    : IRequest<Result<AttributeGroupDto>>,
        ICompanyScopedRequest;

public sealed class CreateAttributeGroupCommandValidator
    : AbstractValidator<CreateAttributeGroupCommand>
{
    public CreateAttributeGroupCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateAttributeGroupCommandValidator
    : AbstractValidator<UpdateAttributeGroupCommand>
{
    public UpdateAttributeGroupCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public interface IAttributeGroupRepository
{
    Task<IReadOnlyList<AttributeGroup>> GetAllAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    );
    Task<AttributeGroup?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );
    Task<bool> ExistsByCodeAsync(
        string code,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(AttributeGroup group, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class CreateAttributeGroupCommandHandler
    : IRequestHandler<CreateAttributeGroupCommand, Result<AttributeGroupDto>>
{
    private readonly IAttributeGroupRepository _r;
    private readonly ICurrentTenant _s;
    private readonly ICurrentUser _u;

    public CreateAttributeGroupCommandHandler(
        IAttributeGroupRepository r,
        ICurrentTenant s,
        ICurrentUser u
    )
    {
        _r = r;
        _s = s;
        _u = u;
    }

    public async Task<Result<AttributeGroupDto>> Handle(
        CreateAttributeGroupCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var sid = _s.TenantId;
        if (await _r.ExistsByCodeAsync(cmd.Code, sid, cancellationToken))
            return Result<AttributeGroupDto>.Conflict($"Código '{cmd.Code}' duplicado.");
        var g = AttributeGroup.Create(sid, cmd.Code, cmd.Name, cmd.SortOrder);
        await _r.AddAsync(g, cancellationToken);
        await _r.SaveChangesAsync(cancellationToken);
        return Result<AttributeGroupDto>.Success(
            new(g.Id, g.Code, g.Name, g.SortOrder, g.IsActive)
        );
    }
}

public sealed class UpdateAttributeGroupCommandHandler
    : IRequestHandler<UpdateAttributeGroupCommand, Result<AttributeGroupDto>>
{
    private readonly IAttributeGroupRepository _r;
    private readonly ICurrentTenant _s;
    private readonly ICurrentUser _u;

    public UpdateAttributeGroupCommandHandler(
        IAttributeGroupRepository r,
        ICurrentTenant s,
        ICurrentUser u
    )
    {
        _r = r;
        _s = s;
        _u = u;
    }

    public async Task<Result<AttributeGroupDto>> Handle(
        UpdateAttributeGroupCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var g = await _r.GetByIdAsync(cmd.Id, _s.TenantId, cancellationToken);
        if (g is null)
            return Result<AttributeGroupDto>.NotFound("No encontrado.");
        g.Update(cmd.Name, cmd.SortOrder, _u.UserId);
        await _r.SaveChangesAsync(cancellationToken);
        return Result<AttributeGroupDto>.Success(
            new(g.Id, g.Code, g.Name, g.SortOrder, g.IsActive)
        );
    }
}

public sealed class EnableAttributeGroupCommandHandler
    : IRequestHandler<EnableAttributeGroupCommand, Result<bool>>
{
    private readonly IAttributeGroupRepository _r;
    private readonly ICurrentTenant _s;
    private readonly ICurrentUser _u;

    public EnableAttributeGroupCommandHandler(
        IAttributeGroupRepository r,
        ICurrentTenant s,
        ICurrentUser u
    )
    {
        _r = r;
        _s = s;
        _u = u;
    }

    public async Task<Result<bool>> Handle(
        EnableAttributeGroupCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var g = await _r.GetByIdAsync(cmd.Id, _s.TenantId, cancellationToken);
        if (g is null)
            return Result<bool>.NotFound("No encontrado.");
        try
        {
            g.Enable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        await _r.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class DisableAttributeGroupCommandHandler
    : IRequestHandler<DisableAttributeGroupCommand, Result<bool>>
{
    private readonly IAttributeGroupRepository _r;
    private readonly ICurrentTenant _s;
    private readonly ICurrentUser _u;

    public DisableAttributeGroupCommandHandler(
        IAttributeGroupRepository r,
        ICurrentTenant s,
        ICurrentUser u
    )
    {
        _r = r;
        _s = s;
        _u = u;
    }

    public async Task<Result<bool>> Handle(
        DisableAttributeGroupCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var g = await _r.GetByIdAsync(cmd.Id, _s.TenantId, cancellationToken);
        if (g is null)
            return Result<bool>.NotFound("No encontrado.");
        try
        {
            g.Disable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        await _r.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class GetAttributeGroupsQueryHandler
    : IRequestHandler<GetAttributeGroupsQuery, Result<IReadOnlyList<AttributeGroupDto>>>
{
    private readonly IAttributeGroupRepository _r;
    private readonly ICurrentTenant _s;

    public GetAttributeGroupsQueryHandler(IAttributeGroupRepository r, ICurrentTenant s)
    {
        _r = r;
        _s = s;
    }

    public async Task<Result<IReadOnlyList<AttributeGroupDto>>> Handle(
        GetAttributeGroupsQuery q,
        CancellationToken cancellationToken
    )
    {
        var all = await _r.GetAllAsync(_s.TenantId, cancellationToken);
        var items = (q.IsActive.HasValue ? all.Where(g => g.IsActive == q.IsActive.Value) : all)
            .Select(g => new AttributeGroupDto(g.Id, g.Code, g.Name, g.SortOrder, g.IsActive))
            .ToList();
        return Result<IReadOnlyList<AttributeGroupDto>>.Success(items);
    }
}

public sealed class GetAttributeGroupByIdQueryHandler
    : IRequestHandler<GetAttributeGroupByIdQuery, Result<AttributeGroupDto>>
{
    private readonly IAttributeGroupRepository _r;
    private readonly ICurrentTenant _s;

    public GetAttributeGroupByIdQueryHandler(IAttributeGroupRepository r, ICurrentTenant s)
    {
        _r = r;
        _s = s;
    }

    public async Task<Result<AttributeGroupDto>> Handle(
        GetAttributeGroupByIdQuery q,
        CancellationToken cancellationToken
    )
    {
        var g = await _r.GetByIdAsync(q.Id, _s.TenantId, cancellationToken);
        return g is null
            ? Result<AttributeGroupDto>.NotFound("No encontrado.")
            : Result<AttributeGroupDto>.Success(new(g.Id, g.Code, g.Name, g.SortOrder, g.IsActive));
    }
}
