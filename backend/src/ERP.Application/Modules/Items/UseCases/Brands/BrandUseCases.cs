using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.Brands;

public sealed record CreateBrandCommand(
    string Code,
    string Name,
    string? Manufacturer = null,
    string? CountryOfOrigin = null
) : IRequest<Result<BrandDto>>, ICompanyScopedRequest;

public sealed record UpdateBrandCommand(
    Guid Id,
    string Code,
    string Name,
    string? Manufacturer = null,
    string? CountryOfOrigin = null
) : IRequest<Result<BrandDto>>, ICompanyScopedRequest;

public sealed record EnableBrandCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed record DisableBrandCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed record GetBrandByIdQuery(Guid Id) : IRequest<Result<BrandDto>>, ICompanyScopedRequest;

public sealed record GetBrandsQuery(bool? IsActive = null)
    : IRequest<Result<IReadOnlyList<BrandDto>>>,
        ICompanyScopedRequest;

public sealed class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(Brand.MaxCodeLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Brand.MaxNameLength);
    }
}

public sealed class UpdateBrandCommandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(Brand.MaxCodeLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Brand.MaxNameLength);
    }
}

public sealed class CreateBrandCommandHandler
    : IRequestHandler<CreateBrandCommand, Result<BrandDto>>
{
    private readonly IItemCatalogRepository _c;
    private readonly ICurrentTenant _s;
    private readonly ICurrentUser _u;

    public CreateBrandCommandHandler(IItemCatalogRepository c, ICurrentTenant s, ICurrentUser u)
    {
        _c = c;
        _s = s;
        _u = u;
    }

    public async Task<Result<BrandDto>> Handle(
        CreateBrandCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var sid = _s.TenantId;
        if (await _c.BrandCodeExistsAsync(cmd.Code, sid, cancellationToken))
            return Result<BrandDto>.Conflict($"Código '{cmd.Code}' duplicado.");
        var b = Brand.Create(
            sid,
            cmd.Code,
            cmd.Name,
            _u.UserId,
            cmd.Manufacturer,
            cmd.CountryOfOrigin
        );
        await _c.AddBrandAsync(b, cancellationToken);
        await _c.SaveChangesAsync(cancellationToken);
        return Result<BrandDto>.Success(
            new(
                b.Id,
                b.Code,
                b.Name,
                b.Manufacturer,
                b.CountryOfOrigin,
                b.IsActive,
                b.CreatedAt,
                b.UpdatedAt
            )
        );
    }
}

public sealed class UpdateBrandCommandHandler
    : IRequestHandler<UpdateBrandCommand, Result<BrandDto>>
{
    private readonly IItemCatalogRepository _c;
    private readonly ICurrentUser _u;

    public UpdateBrandCommandHandler(IItemCatalogRepository c, ICurrentUser u)
    {
        _c = c;
        _u = u;
    }

    public async Task<Result<BrandDto>> Handle(
        UpdateBrandCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var b = await _c.GetBrandByIdAsync(cmd.Id, cancellationToken);
        if (b is null)
            return Result<BrandDto>.NotFound("Marca no encontrada.");
        var nc = cmd.Code.Trim().ToUpperInvariant();
        if (nc != b.Code && await _c.BrandCodeExistsAsync(nc, b.TenantId, cancellationToken))
            return Result<BrandDto>.Conflict($"Código '{nc}' duplicado.");
        b.Update(cmd.Code, cmd.Name, _u.UserId, cmd.Manufacturer, cmd.CountryOfOrigin);
        await _c.SaveChangesAsync(cancellationToken);
        return Result<BrandDto>.Success(
            new(
                b.Id,
                b.Code,
                b.Name,
                b.Manufacturer,
                b.CountryOfOrigin,
                b.IsActive,
                b.CreatedAt,
                b.UpdatedAt
            )
        );
    }
}

public sealed class EnableBrandCommandHandler : IRequestHandler<EnableBrandCommand, Result<bool>>
{
    private readonly IItemCatalogRepository _c;
    private readonly ICurrentUser _u;

    public EnableBrandCommandHandler(IItemCatalogRepository c, ICurrentUser u)
    {
        _c = c;
        _u = u;
    }

    public async Task<Result<bool>> Handle(
        EnableBrandCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var b = await _c.GetBrandByIdAsync(cmd.Id, cancellationToken);
        if (b is null)
            return Result<bool>.NotFound("No encontrada.");
        try
        {
            b.Enable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        await _c.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class DisableBrandCommandHandler : IRequestHandler<DisableBrandCommand, Result<bool>>
{
    private readonly IItemCatalogRepository _c;
    private readonly ICurrentUser _u;

    public DisableBrandCommandHandler(IItemCatalogRepository c, ICurrentUser u)
    {
        _c = c;
        _u = u;
    }

    public async Task<Result<bool>> Handle(
        DisableBrandCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var b = await _c.GetBrandByIdAsync(cmd.Id, cancellationToken);
        if (b is null)
            return Result<bool>.NotFound("No encontrada.");
        try
        {
            b.Disable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        await _c.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class GetBrandByIdQueryHandler : IRequestHandler<GetBrandByIdQuery, Result<BrandDto>>
{
    private readonly IItemCatalogRepository _c;

    public GetBrandByIdQueryHandler(IItemCatalogRepository c) => _c = c;

    public async Task<Result<BrandDto>> Handle(
        GetBrandByIdQuery q,
        CancellationToken cancellationToken
    )
    {
        var b = await _c.GetBrandByIdAsync(q.Id, cancellationToken);
        return b is null
            ? Result<BrandDto>.NotFound("No encontrada.")
            : Result<BrandDto>.Success(
                new(
                    b.Id,
                    b.Code,
                    b.Name,
                    b.Manufacturer,
                    b.CountryOfOrigin,
                    b.IsActive,
                    b.CreatedAt,
                    b.UpdatedAt
                )
            );
    }
}

public sealed class GetBrandsQueryHandler
    : IRequestHandler<GetBrandsQuery, Result<IReadOnlyList<BrandDto>>>
{
    private readonly IItemCatalogRepository _c;
    private readonly ICurrentTenant _s;

    public GetBrandsQueryHandler(IItemCatalogRepository c, ICurrentTenant s)
    {
        _c = c;
        _s = s;
    }

    public async Task<Result<IReadOnlyList<BrandDto>>> Handle(
        GetBrandsQuery q,
        CancellationToken cancellationToken
    )
    {
        var all = await _c.GetBrandsAsync(_s.TenantId, cancellationToken);
        var items = (q.IsActive.HasValue ? all.Where(b => b.IsActive == q.IsActive.Value) : all)
            .Select(b => new BrandDto(
                b.Id,
                b.Code,
                b.Name,
                b.Manufacturer,
                b.CountryOfOrigin,
                b.IsActive,
                b.CreatedAt,
                b.UpdatedAt
            ))
            .ToList();
        return Result<IReadOnlyList<BrandDto>>.Success(items);
    }
}
