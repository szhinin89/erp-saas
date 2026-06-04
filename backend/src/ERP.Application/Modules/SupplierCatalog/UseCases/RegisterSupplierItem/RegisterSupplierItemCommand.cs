using MediatR;
using ERP.Application.Common;
using ERP.Application.SupplierCatalog.DTOs;
using ERP.Domain.Modules.SupplierCatalog.Entities;
using ERP.Domain.Modules.SupplierCatalog.Interfaces;
using FluentValidation;

namespace ERP.Application.SupplierCatalog.UseCases.RegisterSupplierItem;

public sealed record RegisterSupplierItemCommand(
    Guid    SupplierId,
    Guid    ItemId,
    string  SupplierCode,
    string  DefaultUomCode,
    Guid?   VariantId      = null,
    string? SupplierName   = null,
    int     LeadTimeDays   = 0,
    decimal MinOrderQty    = 1,
    bool    IsDefault      = false
) : IRequest<Result<SupplierItemDto>>, ICompanyScopedRequest;

public sealed class RegisterSupplierItemCommandValidator : AbstractValidator<RegisterSupplierItemCommand>
{
    public RegisterSupplierItemCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.SupplierCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultUomCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinOrderQty).GreaterThan(0);
    }
}

public sealed class RegisterSupplierItemCommandHandler
    : IRequestHandler<RegisterSupplierItemCommand, Result<SupplierItemDto>>
{
    private readonly ISupplierItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public RegisterSupplierItemCommandHandler(
        ISupplierItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repository = repository;
        _subscriber = subscriber;
        _user       = user;
    }

    public async Task<Result<SupplierItemDto>> Handle(RegisterSupplierItemCommand cmd, CancellationToken ct)
    {
        if (await _repository.ExistsAsync(cmd.SupplierId, cmd.ItemId, cmd.VariantId, ct))
            return Result<SupplierItemDto>.Conflict(
                "Ya existe un registro de proveedor para este ítem/variante.");

        var supplierItem = SupplierItem.Create(
            _subscriber.SubscriberId, cmd.SupplierId, cmd.ItemId,
            cmd.SupplierCode, cmd.DefaultUomCode, _user.UserId,
            cmd.VariantId, cmd.SupplierName, cmd.LeadTimeDays, cmd.MinOrderQty, cmd.IsDefault);

        await _repository.AddAsync(supplierItem, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<SupplierItemDto>.Success(new SupplierItemDto(
            supplierItem.Id, supplierItem.SupplierId, supplierItem.ItemId, supplierItem.VariantId,
            supplierItem.SupplierCode, supplierItem.SupplierName, supplierItem.DefaultUomCode,
            supplierItem.LeadTimeDays, supplierItem.MinOrderQty, supplierItem.IsDefault,
            supplierItem.IsActive, []));
    }
}
