using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Enums;
using ERP.Domain.Modules.Employees.Interfaces;
using ERP.Domain.Modules.Employees.Services;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Employees.UseCases;

// ── Commands & Queries (un único conjunto para los tres catálogos; Type lo fija la ruta) ──

public sealed record ListEmployeeCatalogQuery(
    EmployeeCatalogType Type,
    bool? IsActive = null,
    string? Search = null,
    int Skip = 0,
    int Take = 50
) : IRequest<Result<PagedResult<EmployeeCatalogEntryDto>>>, ICompanyScopedRequest;

public sealed record GetEmployeeCatalogEntryQuery(EmployeeCatalogType Type, Guid Id)
    : IRequest<Result<EmployeeCatalogEntryDto>>,
        ICompanyScopedRequest;

public sealed record CreateEmployeeCatalogEntryCommand(EmployeeCatalogType Type, string Name)
    : IRequest<Result<EmployeeCatalogEntryDto>>,
        ICompanyScopedRequest;

public sealed record RenameEmployeeCatalogEntryCommand(
    EmployeeCatalogType Type,
    Guid Id,
    uint RowVersion,
    string Name
)
    : IRequest<Result<EmployeeCatalogEntryDto>>,
        ICompanyScopedRequest;

public sealed record SetEmployeeCatalogEntryActiveCommand(
    EmployeeCatalogType Type,
    Guid Id,
    uint RowVersion,
    bool Active
)
    : IRequest<Result<EmployeeCatalogEntryDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class ListEmployeeCatalogQueryValidator : AbstractValidator<ListEmployeeCatalogQuery>
{
    public ListEmployeeCatalogQueryValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, 200);
        RuleFor(x => x.Search).MaximumLength(EmployeeCatalogEntry.MaxNameLength);
    }
}

public sealed class CreateEmployeeCatalogEntryCommandValidator
    : AbstractValidator<CreateEmployeeCatalogEntryCommand>
{
    public CreateEmployeeCatalogEntryCommandValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(EmployeeCatalogEntry.MaxNameLength);
    }
}

public sealed class RenameEmployeeCatalogEntryCommandValidator
    : AbstractValidator<RenameEmployeeCatalogEntryCommand>
{
    public RenameEmployeeCatalogEntryCommandValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(EmployeeCatalogEntry.MaxNameLength);
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

internal static class EmployeeCatalogMapping
{
    public static EmployeeCatalogEntryDto ToDto(EmployeeCatalogEntry e, uint rowVersion) =>
        new(e.Id, e.Name, e.IsActive, rowVersion);

    public static Result<T> Conflict<T>() =>
        Result<T>.Conflict(
            "El registro fue modificado por otro usuario. Recargue e intente de nuevo.",
            ApiResponseCodes.Common.ConcurrencyConflict
        );

    public static Result<T> NotFound<T>() =>
        Result<T>.NotFound("El registro del catálogo no existe.");

    public static Result<T> Duplicate<T>() =>
        Result<T>.Failure(
            "Ya existe un registro con ese nombre en el catálogo.",
            ApiResponseCodes.Employees.CatalogDuplicate
        );
}

public sealed class ListEmployeeCatalogHandler
    : IRequestHandler<ListEmployeeCatalogQuery, Result<PagedResult<EmployeeCatalogEntryDto>>>
{
    private readonly IEmployeeCatalogRepository _catalogs;
    private readonly IOperationalContext _ctx;

    public ListEmployeeCatalogHandler(IEmployeeCatalogRepository catalogs, IOperationalContext ctx) =>
        (_catalogs, _ctx) = (catalogs, ctx);

    public async Task<Result<PagedResult<EmployeeCatalogEntryDto>>> Handle(
        ListEmployeeCatalogQuery q,
        CancellationToken ct
    )
    {
        var take = Math.Clamp(q.Take, 1, 200);
        var skip = Math.Max(q.Skip, 0);
        var (items, total) = await _catalogs.ListAsync(
            q.Type,
            _ctx.TenantId,
            _ctx.CompanyId,
            q.IsActive,
            q.Search,
            skip,
            take,
            ct
        );
        return Result<PagedResult<EmployeeCatalogEntryDto>>.Success(
            new PagedResult<EmployeeCatalogEntryDto>(
                items.Select(r => EmployeeCatalogMapping.ToDto(r.Entry, r.RowVersion)).ToList(),
                (skip / take) + 1,
                take,
                total
            )
        );
    }
}

public sealed class GetEmployeeCatalogEntryHandler
    : IRequestHandler<GetEmployeeCatalogEntryQuery, Result<EmployeeCatalogEntryDto>>
{
    private readonly IEmployeeCatalogRepository _catalogs;
    private readonly IOperationalContext _ctx;

    public GetEmployeeCatalogEntryHandler(IEmployeeCatalogRepository catalogs, IOperationalContext ctx) =>
        (_catalogs, _ctx) = (catalogs, ctx);

    public async Task<Result<EmployeeCatalogEntryDto>> Handle(
        GetEmployeeCatalogEntryQuery q,
        CancellationToken ct
    )
    {
        var entry = await _catalogs.GetByIdAsync(q.Type, _ctx.TenantId, _ctx.CompanyId, q.Id, ct);
        return entry is null
            ? EmployeeCatalogMapping.NotFound<EmployeeCatalogEntryDto>()
            : Result<EmployeeCatalogEntryDto>.Success(
                EmployeeCatalogMapping.ToDto(entry, _catalogs.GetRowVersion(entry))
            );
    }
}

public sealed class CreateEmployeeCatalogEntryHandler
    : IRequestHandler<CreateEmployeeCatalogEntryCommand, Result<EmployeeCatalogEntryDto>>
{
    private readonly IEmployeeCatalogRepository _catalogs;
    private readonly IDatabaseExceptionTranslator _translator;
    private readonly IOperationalContext _ctx;

    public CreateEmployeeCatalogEntryHandler(
        IEmployeeCatalogRepository catalogs,
        IDatabaseExceptionTranslator translator,
        IOperationalContext ctx
    ) => (_catalogs, _translator, _ctx) = (catalogs, translator, ctx);

    public async Task<Result<EmployeeCatalogEntryDto>> Handle(
        CreateEmployeeCatalogEntryCommand cmd,
        CancellationToken ct
    )
    {
        var normalized = EmployeeNameNormalizer.Normalize(cmd.Name);
        if (
            await _catalogs.ExistsByNormalizedNameAsync(
                cmd.Type, _ctx.TenantId, _ctx.CompanyId, normalized, null, ct)
        )
            return EmployeeCatalogMapping.Duplicate<EmployeeCatalogEntryDto>();

        var entry = EmployeeCatalogFactory.Create(
            cmd.Type, _ctx.TenantId, _ctx.CompanyId, cmd.Name, _ctx.UserId);
        await _catalogs.AddAsync(entry, ct);
        try
        {
            if (!await _catalogs.SaveChangesAsync(ct))
                return EmployeeCatalogMapping.Conflict<EmployeeCatalogEntryDto>();
        }
        catch (Exception ex) when (_translator.TryGetUniqueViolation(ex, out _))
        {
            return EmployeeCatalogMapping.Duplicate<EmployeeCatalogEntryDto>();
        }
        return Result<EmployeeCatalogEntryDto>.Success(
            EmployeeCatalogMapping.ToDto(entry, _catalogs.GetRowVersion(entry))
        );
    }
}

public sealed class RenameEmployeeCatalogEntryHandler
    : IRequestHandler<RenameEmployeeCatalogEntryCommand, Result<EmployeeCatalogEntryDto>>
{
    private readonly IEmployeeCatalogRepository _catalogs;
    private readonly IDatabaseExceptionTranslator _translator;
    private readonly IOperationalContext _ctx;

    public RenameEmployeeCatalogEntryHandler(
        IEmployeeCatalogRepository catalogs,
        IDatabaseExceptionTranslator translator,
        IOperationalContext ctx
    ) => (_catalogs, _translator, _ctx) = (catalogs, translator, ctx);

    public async Task<Result<EmployeeCatalogEntryDto>> Handle(
        RenameEmployeeCatalogEntryCommand cmd,
        CancellationToken ct
    )
    {
        var entry = await _catalogs.GetByIdAsync(cmd.Type, _ctx.TenantId, _ctx.CompanyId, cmd.Id, ct);
        if (entry is null)
            return EmployeeCatalogMapping.NotFound<EmployeeCatalogEntryDto>();

        // Versión obsoleta: conflicto antes de cualquier otra regla; la escritura la vuelve a comprobar.
        if (_catalogs.GetRowVersion(entry) != cmd.RowVersion)
            return EmployeeCatalogMapping.Conflict<EmployeeCatalogEntryDto>();
        _catalogs.SetExpectedRowVersion(entry, cmd.RowVersion);

        var normalized = EmployeeNameNormalizer.Normalize(cmd.Name);
        if (
            await _catalogs.ExistsByNormalizedNameAsync(
                cmd.Type, _ctx.TenantId, _ctx.CompanyId, normalized, entry.Id, ct)
        )
            return EmployeeCatalogMapping.Duplicate<EmployeeCatalogEntryDto>();

        entry.Rename(cmd.Name, _ctx.UserId);
        entry.Touch(_ctx.UserId); // fuerza la comprobación de xmin aunque el nombre no cambie
        try
        {
            if (!await _catalogs.SaveChangesAsync(ct))
                return EmployeeCatalogMapping.Conflict<EmployeeCatalogEntryDto>();
        }
        catch (Exception ex) when (_translator.TryGetUniqueViolation(ex, out _))
        {
            return EmployeeCatalogMapping.Duplicate<EmployeeCatalogEntryDto>();
        }
        return Result<EmployeeCatalogEntryDto>.Success(
            EmployeeCatalogMapping.ToDto(entry, _catalogs.GetRowVersion(entry))
        );
    }
}

/// <summary>Activa o inactiva (nunca borra). Inactivar no altera las fichas que ya usan el valor.</summary>
public sealed class SetEmployeeCatalogEntryActiveHandler
    : IRequestHandler<SetEmployeeCatalogEntryActiveCommand, Result<EmployeeCatalogEntryDto>>
{
    private readonly IEmployeeCatalogRepository _catalogs;
    private readonly IOperationalContext _ctx;

    public SetEmployeeCatalogEntryActiveHandler(
        IEmployeeCatalogRepository catalogs,
        IOperationalContext ctx
    ) => (_catalogs, _ctx) = (catalogs, ctx);

    public async Task<Result<EmployeeCatalogEntryDto>> Handle(
        SetEmployeeCatalogEntryActiveCommand cmd,
        CancellationToken ct
    )
    {
        var entry = await _catalogs.GetByIdAsync(cmd.Type, _ctx.TenantId, _ctx.CompanyId, cmd.Id, ct);
        if (entry is null)
            return EmployeeCatalogMapping.NotFound<EmployeeCatalogEntryDto>();

        // Versión obsoleta: conflicto antes que la regla de estado; la escritura la vuelve a comprobar.
        if (_catalogs.GetRowVersion(entry) != cmd.RowVersion)
            return EmployeeCatalogMapping.Conflict<EmployeeCatalogEntryDto>();
        _catalogs.SetExpectedRowVersion(entry, cmd.RowVersion);

        // El dominio rechaza (DomainRuleViolationException → 422) un cambio al estado actual.
        if (cmd.Active)
            entry.Activate(_ctx.UserId);
        else
            entry.Deactivate(_ctx.UserId);
        // UPDATE sobre la fila: espera a las asignaciones que la tienen bloqueada FOR SHARE.
        if (!await _catalogs.SaveChangesAsync(ct))
            return EmployeeCatalogMapping.Conflict<EmployeeCatalogEntryDto>();
        return Result<EmployeeCatalogEntryDto>.Success(
            EmployeeCatalogMapping.ToDto(entry, _catalogs.GetRowVersion(entry))
        );
    }
}
