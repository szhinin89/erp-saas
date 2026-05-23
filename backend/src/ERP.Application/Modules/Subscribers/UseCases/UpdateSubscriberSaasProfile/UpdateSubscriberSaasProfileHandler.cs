using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberSaasProfile;

public sealed class UpdateSubscriberSaasProfileHandler : IRequestHandler<UpdateSubscriberSaasProfileCommand, Result<SubscriberDto>>
{
    private readonly ISubscriberRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentSubscriber _currentSubscriber;

    public UpdateSubscriberSaasProfileHandler(
        ISubscriberRepository repository,
        ICurrentUser currentUser,
        ICurrentSubscriber currentSubscriber)
    {
        _repository = repository;
        _currentUser = currentUser;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<SubscriberDto>> Handle(UpdateSubscriberSaasProfileCommand command, CancellationToken ct)
    {
        var tenant = await _repository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null)
            return Result<SubscriberDto>.Failure("Empresa no encontrada.");

        if (_currentUser.Role == "Admin" && _currentSubscriber.SubscriberId != command.SubscriberId)
            return Result<SubscriberDto>.Failure("No tiene permiso para modificar esta empresa.");

        var slug = command.Slug.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            return Result<SubscriberDto>.Failure("Slug inválido.");

        if (!string.Equals(slug, tenant.Slug, StringComparison.Ordinal))
        {
            var other = await _repository.GetBySlugAsync(slug, ct);
            if (other is not null && other.Id != tenant.Id)
                return Result<SubscriberDto>.Failure("El slug ya está en uso.");
        }

        var name = command.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<SubscriberDto>.Failure("El nombre de la empresa es obligatorio.");

        tenant.UpdateSaasProfile(
            name,
            slug,
            command.Ruc,
            command.ShortName,
            command.TradeName,
            command.Dinardap,
            command.LogoUrl,
            command.DisplayOrder,
            command.Priority,
            _currentUser.UserId);

        await _repository.SaveChangesAsync(ct);

        return Result<SubscriberDto>.Success(SubscriberDto.FromTenant(tenant));
    }
}
