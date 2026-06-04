using MediatR;
using ERP.Application.Common;
using ERP.Application.Kits.DTOs;
using ERP.Domain.Modules.Kits.Interfaces;
using FluentValidation;

namespace ERP.Application.Kits.UseCases.AddKitLine;

public sealed record AddKitLineCommand(
    Guid    KitId,
    Guid    ComponentItemId,
    string  UomCode,
    decimal Quantity,
    Guid?   ComponentVariantId = null,
    bool    IsOptional         = false,
    int     SortOrder          = 0
) : IRequest<Result<KitLineDto>>, ICompanyScopedRequest;

public sealed class AddKitLineCommandValidator : AbstractValidator<AddKitLineCommand>
{
    public AddKitLineCommandValidator()
    {
        RuleFor(x => x.KitId).NotEmpty();
        RuleFor(x => x.ComponentItemId).NotEmpty();
        RuleFor(x => x.UomCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor que cero.");
    }
}

public sealed class AddKitLineCommandHandler
    : IRequestHandler<AddKitLineCommand, Result<KitLineDto>>
{
    private readonly IKitRepository _repository;
    private readonly ICurrentUser _user;

    public AddKitLineCommandHandler(IKitRepository repository, ICurrentUser user)
    {
        _repository = repository;
        _user       = user;
    }

    public async Task<Result<KitLineDto>> Handle(AddKitLineCommand cmd, CancellationToken ct)
    {
        var kit = await _repository.GetByIdAsync(cmd.KitId, ct);
        if (kit is null)
            return Result<KitLineDto>.NotFound("Kit no encontrado.");

        try
        {
            var line = kit.AddLine(
                cmd.ComponentItemId, cmd.ComponentVariantId,
                cmd.Quantity, cmd.UomCode, _user.UserId,
                cmd.IsOptional, cmd.SortOrder);

            await _repository.TrackLineAsync(line, ct);
            await _repository.SaveChangesAsync(ct);

            return Result<KitLineDto>.Success(new KitLineDto(
                line.Id, line.ComponentItemId, line.ComponentVariantId,
                line.Quantity, line.UomCode, line.IsOptional, line.SortOrder, line.IsActive));
        }
        catch (InvalidOperationException ex)
        {
            return Result<KitLineDto>.Conflict(ex.Message);
        }
    }
}
