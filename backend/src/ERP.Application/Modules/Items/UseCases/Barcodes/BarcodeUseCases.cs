using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.Barcodes;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record AddBarcodeCommand(Guid ItemId, Guid VariantId, string Code, string BarcodeType)
    : IRequest<Result<VariantBarcodeDto>>,
        ICompanyScopedRequest;

public sealed record DisableBarcodeCommand(Guid ItemId, Guid VariantId, Guid BarcodeId)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class AddBarcodeCommandValidator : AbstractValidator<AddBarcodeCommand>
{
    public AddBarcodeCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("El código de barras es obligatorio (máximo 100 caracteres).");
        // NOTA: la existencia + estado activo del código en el catálogo global
        // barcode_types se verifica en el Handler (IItemCatalogRepository).
        RuleFor(x => x.BarcodeType)
            .NotEmpty()
            .WithMessage("El tipo de código de barras es obligatorio.");
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class AddBarcodeHandler
    : IRequestHandler<AddBarcodeCommand, Result<VariantBarcodeDto>>
{
    private readonly IItemRepository _repo;
    private readonly IItemCatalogRepository _catalogRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public AddBarcodeHandler(
        IItemRepository repo,
        IItemCatalogRepository catalogRepo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _catalogRepo = catalogRepo;
        _t = t;
        _u = u;
    }

    public async Task<Result<VariantBarcodeDto>> Handle(AddBarcodeCommand cmd, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(cmd.ItemId, _t.TenantId, ct);
        if (item is null)
            return Result<VariantBarcodeDto>.NotFound("Ítem no encontrado.");

        var variant = item.Variants.FirstOrDefault(v => v.Id == cmd.VariantId);
        if (variant is null)
            return Result<VariantBarcodeDto>.NotFound("Variante no encontrada.");

        if (!await _catalogRepo.BarcodeTypeExistsAndActiveAsync(cmd.BarcodeType, ct))
            return Result<VariantBarcodeDto>.ValidationFailure(
                $"El tipo de código de barras '{cmd.BarcodeType}' no es válido."
            );

        var code = cmd.Code.Trim();
        if (await _repo.BarcodeExistsAsync(code, _t.TenantId, ct))
            return Result<VariantBarcodeDto>.Conflict(
                $"El código de barras '{code}' ya está asignado a otro ítem."
            );

        try
        {
            variant.AddBarcode(code, cmd.BarcodeType, _t.TenantId, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<VariantBarcodeDto>.Conflict(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        var bc = variant.Barcodes.Last();
        return Result<VariantBarcodeDto>.Success(
            new VariantBarcodeDto(bc.Id, bc.Code, bc.BarcodeType, bc.IsPrimary)
        );
    }
}

public sealed class DisableBarcodeHandler : IRequestHandler<DisableBarcodeCommand, Result<bool>>
{
    private readonly IItemRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public DisableBarcodeHandler(IItemRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<bool>> Handle(DisableBarcodeCommand cmd, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(cmd.ItemId, _t.TenantId, ct);
        if (item is null)
            return Result<bool>.NotFound("Ítem no encontrado.");

        var variant = item.Variants.FirstOrDefault(v => v.Id == cmd.VariantId);
        if (variant is null)
            return Result<bool>.NotFound("Variante no encontrada.");

        try
        {
            variant.DisableBarcode(cmd.BarcodeId, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.NotFound(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
