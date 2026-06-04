using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
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
    private readonly IOperationalContext        _ctx;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreateBusinessPartnerHandler(
        IBusinessPartnerRepository  bpRepo,
        IOperationalContext         ctx,
        IDatabaseExceptionTranslator dbEx)
    {
        _bpRepo = bpRepo;
        _ctx    = ctx;
        _dbEx   = dbEx;
    }

    public async Task<Result<BusinessPartnerSummaryDto>> Handle(
        CreateBusinessPartnerCommand cmd,
        CancellationToken ct)
    {
        if (!_ctx.HasSubscriber)
            return Result<BusinessPartnerSummaryDto>.Failure("Contexto de suscriptor no establecido.");

        BusinessPartner bp;
        try
        {
            bp = BusinessPartner.Create(
                _ctx.SubscriberId,
                cmd.IdentificationType,
                cmd.IdentificationNumber,
                cmd.PersonType,
                cmd.LegalName,
                _ctx.UserId,
                cmd.TradeName,
                cmd.CountryCode);
        }
        catch (ArgumentException ex)
        {
            return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message);
        }

        await _bpRepo.AddAsync(bp, ct);

        try
        {
            await _bpRepo.SaveChangesAsync(ct);
            return Result<BusinessPartnerSummaryDto>.Success(BusinessPartnerSummaryDto.From(bp));
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<BusinessPartnerSummaryDto>.Conflict(
                $"Ya existe un BusinessPartner con {cmd.IdentificationType} {cmd.IdentificationNumber} en este tenant.");
        }
    }
}
