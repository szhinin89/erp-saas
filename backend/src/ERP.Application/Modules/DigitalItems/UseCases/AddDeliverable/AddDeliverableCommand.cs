using MediatR;
using ERP.Application.Common;
using ERP.Application.DigitalItems.DTOs;
using ERP.Domain.Modules.DigitalItems.Interfaces;
using FluentValidation;

namespace ERP.Application.DigitalItems.UseCases.AddDeliverable;

public sealed record AddDeliverableCommand(
    Guid    DigitalItemId,
    string  Name,
    Guid    StorageObjectId,
    string? Version = null
) : IRequest<Result<DeliverableDto>>, ICompanyScopedRequest;

public sealed class AddDeliverableCommandValidator : AbstractValidator<AddDeliverableCommand>
{
    public AddDeliverableCommandValidator()
    {
        RuleFor(x => x.DigitalItemId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StorageObjectId).NotEmpty();
    }
}

public sealed class AddDeliverableCommandHandler
    : IRequestHandler<AddDeliverableCommand, Result<DeliverableDto>>
{
    private readonly IDigitalItemRepository _repository;
    private readonly ICurrentUser _user;

    public AddDeliverableCommandHandler(IDigitalItemRepository repository, ICurrentUser user)
    {
        _repository = repository;
        _user       = user;
    }

    public async Task<Result<DeliverableDto>> Handle(AddDeliverableCommand cmd, CancellationToken ct)
    {
        var digitalItem = await _repository.GetByIdAsync(cmd.DigitalItemId, ct);
        if (digitalItem is null)
            return Result<DeliverableDto>.NotFound("Ítem digital no encontrado.");

        var deliverable = digitalItem.AddDeliverable(
            cmd.Name, cmd.StorageObjectId, _user.UserId, cmd.Version);

        await _repository.TrackDeliverableAsync(deliverable, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<DeliverableDto>.Success(new DeliverableDto(
            deliverable.Id, deliverable.Name, deliverable.StorageObjectId,
            deliverable.Version, deliverable.IsActive));
    }
}
