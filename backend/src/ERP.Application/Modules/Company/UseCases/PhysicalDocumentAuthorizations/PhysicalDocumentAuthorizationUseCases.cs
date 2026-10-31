using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.PhysicalDocumentAuthorizations;

/// <summary>
/// PHYSICAL-FISCAL-F1 — autorización de impresión del SRI registrada para un punto de emisión
/// físico y un tipo de comprobante (ver <see cref="PhysicalDocumentAuthorization"/>).
/// </summary>
public sealed record PhysicalDocumentAuthorizationDto(
    Guid Id,
    Guid EmissionPointId,
    string DocTypeCode,
    string AuthorizationNumber,
    DateOnly AuthorizedOn,
    DateOnly ExpiresOn,
    int FirstSequential,
    int LastSequential,
    bool IsActive
)
{
    public static PhysicalDocumentAuthorizationDto From(PhysicalDocumentAuthorization a) =>
        new(
            a.Id,
            a.EmissionPointId,
            a.DocTypeCode,
            a.AuthorizationNumber,
            a.AuthorizedOn,
            a.ExpiresOn,
            a.FirstSequential,
            a.LastSequential,
            a.IsActive
        );
}

// ── Listar ────────────────────────────────────────────────────────────────

public sealed record GetPhysicalDocumentAuthorizationsQuery
    : IRequest<Result<IReadOnlyList<PhysicalDocumentAuthorizationDto>>>,
        ICompanyScopedRequest;

public sealed class GetPhysicalDocumentAuthorizationsQueryHandler
    : IRequestHandler<
        GetPhysicalDocumentAuthorizationsQuery,
        Result<IReadOnlyList<PhysicalDocumentAuthorizationDto>>
    >
{
    private readonly IPhysicalDocumentAuthorizationRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetPhysicalDocumentAuthorizationsQueryHandler(
        IPhysicalDocumentAuthorizationRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<IReadOnlyList<PhysicalDocumentAuthorizationDto>>> Handle(
        GetPhysicalDocumentAuthorizationsQuery request,
        CancellationToken cancellationToken
    )
    {
        var authorizations = await _repo.GetAllByCompanyAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            cancellationToken
        );
        return Result<IReadOnlyList<PhysicalDocumentAuthorizationDto>>.Success(
            authorizations.Select(PhysicalDocumentAuthorizationDto.From).ToList()
        );
    }
}

// ── Registrar ─────────────────────────────────────────────────────────────

/// <summary>
/// Registra la autorización tal como consta en el documento del SRI. Solo para puntos físicos
/// activos de la empresa activa y tipos habilitados; el rango no puede solaparse con otra
/// autorización activa del mismo punto y tipo. No captura numeración ni modifica la secuencia.
/// </summary>
public sealed record CreatePhysicalDocumentAuthorizationCommand(
    Guid EmissionPointId,
    string DocTypeCode,
    string AuthorizationNumber,
    DateOnly AuthorizedOn,
    DateOnly ExpiresOn,
    int FirstSequential,
    int LastSequential
) : IRequest<Result<PhysicalDocumentAuthorizationDto>>, ICompanyScopedRequest;

public sealed class CreatePhysicalDocumentAuthorizationCommandValidator
    : AbstractValidator<CreatePhysicalDocumentAuthorizationCommand>
{
    public CreatePhysicalDocumentAuthorizationCommandValidator()
    {
        RuleFor(x => x.EmissionPointId).NotEmpty();
        RuleFor(x => x.DocTypeCode)
            .NotEmpty()
            .WithMessage("El tipo de comprobante es obligatorio.")
            .MaximumLength(5);
        RuleFor(x => x.AuthorizationNumber)
            .NotEmpty()
            .WithMessage("El número de autorización del SRI es obligatorio.")
            .MaximumLength(PhysicalDocumentAuthorization.AuthorizationNumberMaxLen)
            .Matches("^[0-9]+$")
            .WithMessage("El número de autorización del SRI debe contener solo dígitos.");
        RuleFor(x => x.ExpiresOn)
            .GreaterThanOrEqualTo(x => x.AuthorizedOn)
            .WithMessage("La fecha de caducidad no puede ser anterior a la fecha de autorización.")
            .Must((cmd, expiresOn) => expiresOn <= cmd.AuthorizedOn.AddYears(1))
            .WithMessage(
                "La fecha de caducidad debe estar dentro del año siguiente a la fecha de autorización."
            );
        RuleFor(x => x.FirstSequential)
            .GreaterThan(0)
            .WithMessage("El primer secuencial debe ser mayor que 0.");
        RuleFor(x => x.LastSequential)
            .GreaterThanOrEqualTo(x => x.FirstSequential)
            .WithMessage("El último secuencial no puede ser menor que el primero.")
            .LessThanOrEqualTo(DocumentSequence.MaxSequentialValue)
            .WithMessage(
                $"El último secuencial no puede superar {DocumentSequence.MaxSequentialValue} (9 dígitos)."
            );
    }
}

public sealed class CreatePhysicalDocumentAuthorizationCommandHandler
    : IRequestHandler<
        CreatePhysicalDocumentAuthorizationCommand,
        Result<PhysicalDocumentAuthorizationDto>
    >
{
    private readonly IPhysicalDocumentAuthorizationRepository _repo;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly ISriDocTypeCatalogResolver _docTypeResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public CreatePhysicalDocumentAuthorizationCommandHandler(
        IPhysicalDocumentAuthorizationRepository repo,
        IEmissionPointRepository emissionPointRepo,
        ISriDocTypeCatalogResolver docTypeResolver,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _emissionPointRepo = emissionPointRepo;
        _docTypeResolver = docTypeResolver;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<PhysicalDocumentAuthorizationDto>> Handle(
        CreatePhysicalDocumentAuthorizationCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var docTypeCode = command.DocTypeCode.Trim();

        var emissionPoint = await _emissionPointRepo.GetByIdForCompanyAsync(
            tenantId,
            companyId,
            command.EmissionPointId,
            cancellationToken
        );
        if (emissionPoint is null)
            return Result<PhysicalDocumentAuthorizationDto>.NotFound(
                "Punto de emisión no encontrado."
            );
        if (emissionPoint is not { IsActive: true, EmissionType: EmissionType.Physical })
            return Result<PhysicalDocumentAuthorizationDto>.ValidationFailure(
                "Las autorizaciones de impresión solo se registran en puntos de emisión físicos activos."
            );

        if (!await _docTypeResolver.IsActiveDocTypeAsync(docTypeCode, cancellationToken))
            return Result<PhysicalDocumentAuthorizationDto>.ValidationFailure(
                $"El tipo de comprobante SRI '{docTypeCode}' no está habilitado en el ERP."
            );

        var active = await _repo.GetActiveAsync(
            tenantId,
            companyId,
            command.EmissionPointId,
            docTypeCode,
            cancellationToken
        );
        if (active.Any(a => a.OverlapsRange(command.FirstSequential, command.LastSequential)))
            return Result<PhysicalDocumentAuthorizationDto>.Conflict(
                "El rango de secuenciales se superpone con otra autorización activa del mismo punto y tipo de comprobante."
            );

        PhysicalDocumentAuthorization authorization;
        try
        {
            authorization = PhysicalDocumentAuthorization.Create(
                tenantId,
                companyId,
                command.EmissionPointId,
                docTypeCode,
                command.AuthorizationNumber,
                command.AuthorizedOn,
                command.ExpiresOn,
                command.FirstSequential,
                command.LastSequential,
                _currentUser.UserId
            );
        }
        catch (ArgumentException ex)
        {
            return Result<PhysicalDocumentAuthorizationDto>.ValidationFailure(ex.Message);
        }

        await _repo.AddAsync(authorization, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<PhysicalDocumentAuthorizationDto>.Success(
            PhysicalDocumentAuthorizationDto.From(authorization)
        );
    }
}

// ── Deshabilitar ──────────────────────────────────────────────────────────

/// <summary>Deshabilita (soft delete) una autorización; nunca se borra.</summary>
public sealed record DisablePhysicalDocumentAuthorizationCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed class DisablePhysicalDocumentAuthorizationCommandHandler
    : IRequestHandler<DisablePhysicalDocumentAuthorizationCommand, Result<bool>>
{
    private readonly IPhysicalDocumentAuthorizationRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public DisablePhysicalDocumentAuthorizationCommandHandler(
        IPhysicalDocumentAuthorizationRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(
        DisablePhysicalDocumentAuthorizationCommand command,
        CancellationToken cancellationToken
    )
    {
        var authorization = await _repo.GetByIdForCompanyAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            command.Id,
            cancellationToken
        );
        if (authorization is null)
            return Result<bool>.NotFound("Autorización de impresión no encontrada.");

        try
        {
            authorization.Disable(_currentUser.UserId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Result<bool>.Conflict(ex.Message);
        }

        await _repo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
