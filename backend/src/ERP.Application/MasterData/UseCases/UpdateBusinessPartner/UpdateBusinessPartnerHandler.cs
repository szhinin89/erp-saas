using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Enums;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateBusinessPartner;

public sealed class UpdateBusinessPartnerHandler
    : IRequestHandler<UpdateBusinessPartnerCommand, Result<BusinessPartnerSummaryDto>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ILegalEntityTypeRepository _legalEntityTypeRepo;
    private readonly IOperationalContext _ctx;

    public UpdateBusinessPartnerHandler(
        IBusinessPartnerRepository bpRepo,
        ILegalEntityTypeRepository legalEntityTypeRepo,
        IOperationalContext ctx
    ) => (_bpRepo, _legalEntityTypeRepo, _ctx) = (bpRepo, legalEntityTypeRepo, ctx);

    public async Task<Result<BusinessPartnerSummaryDto>> Handle(
        UpdateBusinessPartnerCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (bp is null)
            return Result<BusinessPartnerSummaryDto>.NotFound("BusinessPartner no encontrado.");

        try
        {
            bp.UpdateProfile(
                cmd.LegalName,
                cmd.LegalEntityTypeCode,
                _ctx.UserId,
                cmd.TradeName,
                cmd.CountryCode
            );
        }
        catch (ArgumentException ex)
        {
            return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message);
        }

        if (!await _legalEntityTypeRepo.ExistsActiveAsync(bp.LegalEntityTypeCode, cancellationToken))
        {
            return Result<BusinessPartnerSummaryDto>.ValidationFailure(
                $"El tipo de entidad legal {bp.LegalEntityTypeCode} no existe o está inactivo."
            );
        }

        await _bpRepo.SaveChangesAsync(cancellationToken);
        return Result<BusinessPartnerSummaryDto>.Success(BusinessPartnerSummaryDto.From(bp));
    }
}

public sealed class UpdateBusinessPartnerIdentificationHandler
    : IRequestHandler<UpdateBusinessPartnerIdentificationCommand, Result<BusinessPartnerSummaryDto>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IIdentificationUsageValidator _usageValidator;
    private readonly IOperationalContext _ctx;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public UpdateBusinessPartnerIdentificationHandler(
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IIdentificationUsageValidator usageValidator,
        IOperationalContext ctx,
        IDatabaseExceptionTranslator dbEx
    ) =>
        (_bpRepo, _roleRepo, _usageValidator, _ctx, _dbEx) = (
            bpRepo,
            roleRepo,
            usageValidator,
            ctx,
            dbEx
        );

    public async Task<Result<BusinessPartnerSummaryDto>> Handle(
        UpdateBusinessPartnerIdentificationCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (bp is null)
            return Result<BusinessPartnerSummaryDto>.NotFound("BusinessPartner no encontrado.");

        var activeRoles = await _roleRepo.GetByBusinessPartnerAsync(
            cmd.Id,
            true,
            cancellationToken
        );
        foreach (var role in activeRoles)
        {
            var usageType = MapRoleToUsage(role.RoleType);
            if (usageType.HasValue)
            {
                var allowed = await _usageValidator.IsAllowedAsync(
                    cmd.IdentificationType,
                    usageType.Value,
                    cancellationToken
                );
                if (!allowed)
                    return Result<BusinessPartnerSummaryDto>.ValidationFailure(
                        $"El tipo de identificación '{cmd.IdentificationType}' no es compatible con el rol {role.RoleType} activo."
                    );
            }
        }

        if (
            await _bpRepo.ExistsByIdentificationAsync(
                cmd.IdentificationType,
                cmd.IdentificationNumber,
                cmd.Id,
                cancellationToken
            )
        )
            return Result<BusinessPartnerSummaryDto>.Conflict(
                $"Ya existe un BusinessPartner con {cmd.IdentificationType} {cmd.IdentificationNumber}."
            );

        try
        {
            bp.UpdateIdentification(cmd.IdentificationType, cmd.IdentificationNumber, _ctx.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message);
        }

        // Sin ExistsActiveAsync aquí a propósito (a diferencia de Create/UpdateProfile): el
        // LegalEntityTypeCode resultante de UpdateIdentification nunca viene de input externo —
        // o queda sin cambios (ya validado en una operación anterior), o es recalculado por
        // TaxIdentification.TryInferLegalEntityTypeCode(), que solo puede devolver 1, 2, 3 o
        // null (switch cerrado sobre literales, ver TaxIdentification.cs). LegalEntityTypeCatalog
        // no tiene ningún endpoint/handler/repositorio de escritura en todo el sistema (auditado
        // 2026-07-30) — IsActive no puede volverse false en producción, por lo que revalidar el
        // catálogo aquí validaría un escenario estructuralmente imposible. Si en el futuro el
        // catálogo se vuelve mutable (ADR nueva), este invariante debe reevaluarse.
        try
        {
            await _bpRepo.SaveChangesAsync(cancellationToken);
            return Result<BusinessPartnerSummaryDto>.Success(BusinessPartnerSummaryDto.From(bp));
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<BusinessPartnerSummaryDto>.Conflict(
                $"Ya existe un BusinessPartner con {cmd.IdentificationType} {cmd.IdentificationNumber}."
            );
        }
    }
    private static IdentificationUsageType? MapRoleToUsage(RoleType role) =>
        role switch
        {
            RoleType.Customer => IdentificationUsageType.Customer,
            RoleType.Supplier => IdentificationUsageType.Supplier,
            RoleType.Employee => IdentificationUsageType.Employee,
            RoleType.Carrier => IdentificationUsageType.Carrier,
            _ => null,
        };
}
