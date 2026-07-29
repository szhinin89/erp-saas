using ERP.Application.Common;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Integration.UseCases;

public sealed record CreateIntegrationTenantCommand(
    IntegrationTenantCreateRequest Request,
    Guid ActorId
) : IRequest<Result<IntegrationTenantStatusDto>>;

public sealed record GetIntegrationTenantStatusQuery(Guid Id)
    : IRequest<Result<IntegrationTenantStatusDto>>;

public sealed record ActivateIntegrationTenantCommand(Guid Id, Guid ActorId)
    : IRequest<Result<IntegrationTenantStatusDto>>;

public sealed record SuspendIntegrationTenantCommand(Guid Id, Guid ActorId)
    : IRequest<Result<IntegrationTenantStatusDto>>;

public sealed class CreateIntegrationTenantHandler
    : IRequestHandler<CreateIntegrationTenantCommand, Result<IntegrationTenantStatusDto>>
{
    private readonly ITenantRepository _tenants;

    public CreateIntegrationTenantHandler(ITenantRepository tenants) => _tenants = tenants;

    public async Task<Result<IntegrationTenantStatusDto>> Handle(
        CreateIntegrationTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
            return Result<IntegrationTenantStatusDto>.ValidationFailure(
                "Name y Slug son obligatorios."
            );

        var existing = await _tenants.GetBySlugAsync(
            request.Slug.Trim().ToLowerInvariant(),
            cancellationToken
        );
        if (existing is not null)
            return Result<IntegrationTenantStatusDto>.Conflict("Ya existe un tenant con ese slug.");

        var tenant = Tenant.Create(
            request.Name.Trim(),
            request.Slug.Trim(),
            command.ActorId,
            request.PreferredLanguage ?? "es"
        );

        await _tenants.AddAsync(tenant, cancellationToken);
        await _tenants.SaveChangesAsync(cancellationToken);

        return Result<IntegrationTenantStatusDto>.Success(ToDto(tenant));
    }

    internal static IntegrationTenantStatusDto ToDto(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.IsActive,
            tenant.PreferredLanguage,
            tenant.CreatedAt,
            tenant.UpdatedAt
        );
}

public sealed class GetIntegrationTenantStatusHandler
    : IRequestHandler<GetIntegrationTenantStatusQuery, Result<IntegrationTenantStatusDto>>
{
    private readonly ITenantRepository _tenants;

    public GetIntegrationTenantStatusHandler(ITenantRepository tenants) => _tenants = tenants;

    public async Task<Result<IntegrationTenantStatusDto>> Handle(
        GetIntegrationTenantStatusQuery query,
        CancellationToken cancellationToken
    )
    {
        var tenant = await _tenants.GetByIdAsync(query.Id, cancellationToken);
        return tenant is null
            ? Result<IntegrationTenantStatusDto>.NotFound("Tenant no encontrado.")
            : Result<IntegrationTenantStatusDto>.Success(
                CreateIntegrationTenantHandler.ToDto(tenant)
            );
    }
}

public sealed class ActivateIntegrationTenantHandler
    : IRequestHandler<ActivateIntegrationTenantCommand, Result<IntegrationTenantStatusDto>>
{
    private readonly ITenantRepository _tenants;

    public ActivateIntegrationTenantHandler(ITenantRepository tenants) => _tenants = tenants;

    public async Task<Result<IntegrationTenantStatusDto>> Handle(
        ActivateIntegrationTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = await _tenants.GetByIdAsync(command.Id, cancellationToken);
        if (tenant is null)
            return Result<IntegrationTenantStatusDto>.NotFound("Tenant no encontrado.");

        tenant.Activate(command.ActorId);
        await _tenants.SaveChangesAsync(cancellationToken);
        return Result<IntegrationTenantStatusDto>.Success(
            CreateIntegrationTenantHandler.ToDto(tenant)
        );
    }
}

public sealed class SuspendIntegrationTenantHandler
    : IRequestHandler<SuspendIntegrationTenantCommand, Result<IntegrationTenantStatusDto>>
{
    private readonly ITenantRepository _tenants;

    public SuspendIntegrationTenantHandler(ITenantRepository tenants) => _tenants = tenants;

    public async Task<Result<IntegrationTenantStatusDto>> Handle(
        SuspendIntegrationTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = await _tenants.GetByIdAsync(command.Id, cancellationToken);
        if (tenant is null)
            return Result<IntegrationTenantStatusDto>.NotFound("Tenant no encontrado.");

        tenant.Deactivate(command.ActorId);
        await _tenants.SaveChangesAsync(cancellationToken);
        return Result<IntegrationTenantStatusDto>.Success(
            CreateIntegrationTenantHandler.ToDto(tenant)
        );
    }
}
