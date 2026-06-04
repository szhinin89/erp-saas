using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateBusinessPartner;

public sealed class UpdateBusinessPartnerHandler
    : IRequestHandler<UpdateBusinessPartnerCommand, Result<BusinessPartnerSummaryDto>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IOperationalContext        _ctx;

    public UpdateBusinessPartnerHandler(IBusinessPartnerRepository bpRepo, IOperationalContext ctx)
        => (_bpRepo, _ctx) = (bpRepo, ctx);

    public async Task<Result<BusinessPartnerSummaryDto>> Handle(
        UpdateBusinessPartnerCommand cmd, CancellationToken ct)
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.Id, ct);
        if (bp is null)
            return Result<BusinessPartnerSummaryDto>.NotFound("BusinessPartner no encontrado.");

        try
        {
            bp.UpdateProfile(cmd.LegalName, cmd.PersonType, _ctx.UserId, cmd.TradeName, cmd.CountryCode);
        }
        catch (ArgumentException ex)        { return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message); }
        catch (InvalidOperationException ex) { return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message); }

        await _bpRepo.SaveChangesAsync(ct);
        return Result<BusinessPartnerSummaryDto>.Success(BusinessPartnerSummaryDto.From(bp));
    }
}

public sealed class UpdateBusinessPartnerIdentificationHandler
    : IRequestHandler<UpdateBusinessPartnerIdentificationCommand, Result<BusinessPartnerSummaryDto>>
{
    private readonly IBusinessPartnerRepository   _bpRepo;
    private readonly IOperationalContext          _ctx;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public UpdateBusinessPartnerIdentificationHandler(
        IBusinessPartnerRepository bpRepo, IOperationalContext ctx, IDatabaseExceptionTranslator dbEx)
        => (_bpRepo, _ctx, _dbEx) = (bpRepo, ctx, dbEx);

    public async Task<Result<BusinessPartnerSummaryDto>> Handle(
        UpdateBusinessPartnerIdentificationCommand cmd, CancellationToken ct)
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.Id, ct);
        if (bp is null)
            return Result<BusinessPartnerSummaryDto>.NotFound("BusinessPartner no encontrado.");

        try
        {
            bp.UpdateIdentification(cmd.IdentificationType, cmd.IdentificationNumber, _ctx.UserId);
        }
        catch (ArgumentException ex)        { return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message); }
        catch (InvalidOperationException ex) { return Result<BusinessPartnerSummaryDto>.ValidationFailure(ex.Message); }

        try
        {
            await _bpRepo.SaveChangesAsync(ct);
            return Result<BusinessPartnerSummaryDto>.Success(BusinessPartnerSummaryDto.From(bp));
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<BusinessPartnerSummaryDto>.Conflict(
                $"Ya existe un BusinessPartner con {cmd.IdentificationType} {cmd.IdentificationNumber}.");
        }
    }
}
