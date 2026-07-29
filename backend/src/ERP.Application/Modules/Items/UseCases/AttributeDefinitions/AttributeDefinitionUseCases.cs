using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.AttributeDefinitions;

// ══════════════════════════════════════════════════════════════════════════
// COMMANDS
// ══════════════════════════════════════════════════════════════════════════

public sealed record CreateAttributeDefinitionCommand(
    Guid GroupId,
    string Code,
    string Name,
    AttributeDataType DataType,
    bool IsVariantAxis = false,
    string? AllowedValues = null,
    bool IsRequired = false,
    int SortOrder = 0
) : IRequest<Result<AttributeDefinitionDto>>, ICompanyScopedRequest;

public sealed record UpdateAttributeDefinitionCommand(
    Guid Id,
    string Name,
    bool IsVariantAxis,
    string? AllowedValues,
    bool IsRequired,
    int SortOrder
) : IRequest<Result<AttributeDefinitionDto>>, ICompanyScopedRequest;

public sealed record EnableAttributeDefinitionCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed record DisableAttributeDefinitionCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

// ══════════════════════════════════════════════════════════════════════════
// QUERIES
// ══════════════════════════════════════════════════════════════════════════

public sealed record GetAttributeDefinitionsQuery(Guid? GroupId = null, bool? IsActive = null)
    : IRequest<Result<IReadOnlyList<AttributeDefinitionDto>>>,
        ICompanyScopedRequest;

public sealed record GetAttributeDefinitionByIdQuery(Guid Id)
    : IRequest<Result<AttributeDefinitionDto>>,
        ICompanyScopedRequest;

// ══════════════════════════════════════════════════════════════════════════
// VALIDATORS
// ══════════════════════════════════════════════════════════════════════════

public sealed class CreateAttributeDefinitionCommandValidator
    : AbstractValidator<CreateAttributeDefinitionCommand>
{
    public CreateAttributeDefinitionCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("El grupo es obligatorio.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DataType).IsInEnum().WithMessage("Tipo de dato inválido.");
        RuleFor(x => x.AllowedValues)
            .NotEmpty()
            .WithMessage("Debe especificar los valores permitidos para atributos de tipo List.")
            .When(x => x.DataType == AttributeDataType.List);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateAttributeDefinitionCommandValidator
    : AbstractValidator<UpdateAttributeDefinitionCommand>
{
    public UpdateAttributeDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// REPOSITORY INTERFACE
// ══════════════════════════════════════════════════════════════════════════

public interface IAttributeDefinitionRepository
{
    Task<IReadOnlyList<AttributeDefinition>> GetAllAsync(
        Guid tenantId,
        Guid? groupId,
        CancellationToken cancellationToken = default
    );
    Task<AttributeDefinition?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );
    Task<bool> ExistsByCodeAsync(
        Guid groupId,
        string code,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(AttributeDefinition def, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

// ══════════════════════════════════════════════════════════════════════════
// HANDLERS
// ══════════════════════════════════════════════════════════════════════════

public sealed class CreateAttributeDefinitionCommandHandler
    : IRequestHandler<CreateAttributeDefinitionCommand, Result<AttributeDefinitionDto>>
{
    private readonly IAttributeDefinitionRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public CreateAttributeDefinitionCommandHandler(
        IAttributeDefinitionRepository repo,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<AttributeDefinitionDto>> Handle(
        CreateAttributeDefinitionCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var sid = _currentTenant.TenantId;
        if (await _repo.ExistsByCodeAsync(cmd.GroupId, cmd.Code, sid, cancellationToken))
            return Result<AttributeDefinitionDto>.Conflict(
                $"Ya existe un atributo con código '{cmd.Code}' en este grupo."
            );

        AttributeDefinition def;
        try
        {
            def = AttributeDefinition.Create(
                sid,
                cmd.GroupId,
                cmd.Code,
                cmd.Name,
                cmd.DataType,
                cmd.IsVariantAxis,
                cmd.AllowedValues,
                cmd.IsRequired,
                cmd.SortOrder
            );
        }
        catch (ArgumentException ex)
        {
            return Result<AttributeDefinitionDto>.ValidationFailure(ex.Message);
        }

        await _repo.AddAsync(def, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<AttributeDefinitionDto>.Success(AttributeDefinitionMapper.ToDto(def));
    }
}

public sealed class UpdateAttributeDefinitionCommandHandler
    : IRequestHandler<UpdateAttributeDefinitionCommand, Result<AttributeDefinitionDto>>
{
    private readonly IAttributeDefinitionRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public UpdateAttributeDefinitionCommandHandler(
        IAttributeDefinitionRepository repo,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<AttributeDefinitionDto>> Handle(
        UpdateAttributeDefinitionCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var def = await _repo.GetByIdAsync(cmd.Id, _currentTenant.TenantId, cancellationToken);
        if (def is null)
            return Result<AttributeDefinitionDto>.NotFound("Atributo no encontrado.");

        try
        {
            def.Update(
                cmd.Name,
                cmd.IsVariantAxis,
                cmd.AllowedValues,
                cmd.IsRequired,
                cmd.SortOrder,
                _user.UserId
            );
        }
        catch (ArgumentException ex)
        {
            return Result<AttributeDefinitionDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(cancellationToken);
        return Result<AttributeDefinitionDto>.Success(AttributeDefinitionMapper.ToDto(def));
    }
}

public sealed class EnableAttributeDefinitionCommandHandler
    : IRequestHandler<EnableAttributeDefinitionCommand, Result<bool>>
{
    private readonly IAttributeDefinitionRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public EnableAttributeDefinitionCommandHandler(
        IAttributeDefinitionRepository repo,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<bool>> Handle(
        EnableAttributeDefinitionCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var def = await _repo.GetByIdAsync(cmd.Id, _currentTenant.TenantId, cancellationToken);
        if (def is null)
            return Result<bool>.NotFound("Atributo no encontrado.");
        try
        {
            def.Enable(_user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class DisableAttributeDefinitionCommandHandler
    : IRequestHandler<DisableAttributeDefinitionCommand, Result<bool>>
{
    private readonly IAttributeDefinitionRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public DisableAttributeDefinitionCommandHandler(
        IAttributeDefinitionRepository repo,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<bool>> Handle(
        DisableAttributeDefinitionCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var def = await _repo.GetByIdAsync(cmd.Id, _currentTenant.TenantId, cancellationToken);
        if (def is null)
            return Result<bool>.NotFound("Atributo no encontrado.");
        try
        {
            def.Disable(_user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class GetAttributeDefinitionsQueryHandler
    : IRequestHandler<GetAttributeDefinitionsQuery, Result<IReadOnlyList<AttributeDefinitionDto>>>
{
    private readonly IAttributeDefinitionRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetAttributeDefinitionsQueryHandler(
        IAttributeDefinitionRepository repo,
        ICurrentTenant tenant
    )
    {
        _repo = repo;
        _currentTenant = tenant;
    }

    public async Task<Result<IReadOnlyList<AttributeDefinitionDto>>> Handle(
        GetAttributeDefinitionsQuery q,
        CancellationToken cancellationToken
    )
    {
        var defs = await _repo.GetAllAsync(_currentTenant.TenantId, q.GroupId, cancellationToken);
        var filtered = q.IsActive.HasValue
            ? defs.Where(d => d.IsActive == q.IsActive.Value).ToList()
            : defs;
        return Result<IReadOnlyList<AttributeDefinitionDto>>.Success(
            filtered.Select(AttributeDefinitionMapper.ToDto).ToList()
        );
    }
}

public sealed class GetAttributeDefinitionByIdQueryHandler
    : IRequestHandler<GetAttributeDefinitionByIdQuery, Result<AttributeDefinitionDto>>
{
    private readonly IAttributeDefinitionRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetAttributeDefinitionByIdQueryHandler(
        IAttributeDefinitionRepository repo,
        ICurrentTenant tenant
    )
    {
        _repo = repo;
        _currentTenant = tenant;
    }

    public async Task<Result<AttributeDefinitionDto>> Handle(
        GetAttributeDefinitionByIdQuery q,
        CancellationToken cancellationToken
    )
    {
        var def = await _repo.GetByIdAsync(q.Id, _currentTenant.TenantId, cancellationToken);
        return def is null
            ? Result<AttributeDefinitionDto>.NotFound("Atributo no encontrado.")
            : Result<AttributeDefinitionDto>.Success(AttributeDefinitionMapper.ToDto(def));
    }
}

internal static class AttributeDefinitionMapper
{
    internal static AttributeDefinitionDto ToDto(AttributeDefinition d) =>
        new(
            d.Id,
            d.GroupId,
            d.Code,
            d.Name,
            d.DataType.ToString(),
            d.IsVariantAxis,
            d.AllowedValues,
            d.IsRequired,
            d.SortOrder,
            d.IsActive
        );
}
