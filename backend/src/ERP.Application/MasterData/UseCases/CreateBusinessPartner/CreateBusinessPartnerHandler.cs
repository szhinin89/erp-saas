using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.CreateBusinessPartner;

/// <summary>
/// Handler delgado: solo orquesta dominio + persistencia.
/// Reglas de negocio (validación RUC/CI, formato de nombre) viven en el AR BusinessPartner.
///
/// FLUJO POST-CREACIÓN:
///   1. Usar AssignBusinessPartnerRoleCommand para asignar roles (Customer, Supplier, etc.)
///   2. Usar CreateBpContactCommand para registrar representante legal, teléfonos, email.
/// </summary>
public sealed class CreateBusinessPartnerHandler
    : IRequestHandler<CreateBusinessPartnerCommand, Result<BusinessPartnerSummaryDto>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IUserActivityRepository _activity;
    private readonly IOperationalContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreateBusinessPartnerHandler(
        IBusinessPartnerRepository bpRepo,
        IUserActivityRepository activity,
        IOperationalContext ctx,
        ICurrentUser currentUser,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _bpRepo = bpRepo;
        _activity = activity;
        _ctx = ctx;
        _currentUser = currentUser;
        _dbEx = dbEx;
    }

    public async Task<Result<BusinessPartnerSummaryDto>> Handle(
        CreateBusinessPartnerCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (!_ctx.HasTenant)
            return Result<BusinessPartnerSummaryDto>.Failure("Contexto de tenant no establecido.");

        if (
            await _bpRepo.ExistsByIdentificationAsync(
                cmd.IdentificationType,
                cmd.IdentificationNumber,
                cancellationToken: cancellationToken
            )
        )
            return Result<BusinessPartnerSummaryDto>.Conflict(
                $"Ya existe un BusinessPartner con {cmd.IdentificationType} {cmd.IdentificationNumber} en este tenant.",
                "IDENTIFICATION_DUPLICATE"
            );

        BusinessPartner bp;
        try
        {
            bp = BusinessPartner.Create(
                _ctx.TenantId,
                cmd.IdentificationType,
                cmd.IdentificationNumber,
                cmd.PersonType,
                cmd.LegalName,
                _ctx.UserId,
                cmd.TradeName,
                cmd.CountryCode
            );
        }
        catch (ArgumentException ex)
        {
            return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message);
        }

        await _bpRepo.AddAsync(bp, cancellationToken);
        await _activity.AddAsync(
            UserActivity.Create(
                _ctx.TenantId,
                _ctx.UserId,
                _currentUser.Email,
                _currentUser.FullName,
                module: "masterdata",
                action: "business-partner.create",
                entityType: "BusinessPartner",
                entityId: bp.Id,
                description: $"{bp.Name.LegalName} ({cmd.IdentificationType} {cmd.IdentificationNumber})"
            ),
            cancellationToken
        );

        try
        {
            await _bpRepo.SaveChangesAsync(cancellationToken);
            return Result<BusinessPartnerSummaryDto>.Success(BusinessPartnerSummaryDto.From(bp));
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<BusinessPartnerSummaryDto>.Conflict(
                $"Ya existe un BusinessPartner con {cmd.IdentificationType} {cmd.IdentificationNumber} en este tenant.",
                "IDENTIFICATION_DUPLICATE"
            );
        }
    }
}
