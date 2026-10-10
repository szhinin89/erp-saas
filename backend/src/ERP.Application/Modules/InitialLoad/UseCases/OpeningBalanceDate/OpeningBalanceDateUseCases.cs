using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using FluentValidation;
using MediatR;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Modules.InitialLoad.UseCases.OpeningBalanceDate;

/// <summary>IL-5A — Configuración → Implementación: fecha de apertura de saldos de la empresa activa.</summary>
public sealed record GetOpeningBalanceDateQuery : IRequest<Result<OpeningBalanceDateDto>>;

/// <summary>
/// Define o corrige <c>Company.OpeningBalanceDate</c> de la empresa activa. Las reglas viven en
/// <c>Company.SetOpeningBalanceDate</c>; este caso de uso solo aporta el estado actual (operaciones
/// reales y aperturas confirmadas). Nullable a propósito: un body sin la fecha (o con otro nombre de
/// campo) llega como null y lo rechaza el validador — con <c>DateOnly</c> se enlazaba en silencio a
/// 0001-01-01 y terminaba como una falsa violación de la regla de apertura.
/// </summary>
public sealed record SetOpeningBalanceDateCommand(DateOnly? OpeningBalanceDate)
    : IRequest<Result<OpeningBalanceDateDto>>;

public sealed class SetOpeningBalanceDateCommandValidator : AbstractValidator<SetOpeningBalanceDateCommand>
{
    public SetOpeningBalanceDateCommandValidator()
    {
        RuleFor(x => x.OpeningBalanceDate)
            .NotNull()
            .WithMessage("La fecha de apertura es obligatoria (formato AAAA-MM-DD).");
    }
}

public sealed class GetOpeningBalanceDateHandler
    : IRequestHandler<GetOpeningBalanceDateQuery, Result<OpeningBalanceDateDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly IOpeningBalanceConstraintsReader _constraints;

    public GetOpeningBalanceDateHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyRepository companies,
        IOpeningBalanceConstraintsReader constraints
    )
    {
        _accessGuard = accessGuard;
        _companies = companies;
        _constraints = constraints;
    }

    public async Task<Result<OpeningBalanceDateDto>> Handle(
        GetOpeningBalanceDateQuery query,
        CancellationToken ct
    )
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(ct);
        if (!access.IsSuccess)
            return Result<OpeningBalanceDateDto>.Failure(access.Error!, access.Code);

        var company = await _companies.GetByIdForTenantAsync(
            access.Value!.CompanyId,
            access.Value.TenantId,
            ct
        );
        if (company is null)
            return Result<OpeningBalanceDateDto>.NotFound("Empresa no encontrada.");

        return Result<OpeningBalanceDateDto>.Success(
            await OpeningBalanceDateMap.ToDtoAsync(company, _constraints, ct)
        );
    }
}

public sealed class SetOpeningBalanceDateHandler
    : IRequestHandler<SetOpeningBalanceDateCommand, Result<OpeningBalanceDateDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly IOpeningBalanceConstraintsReader _constraints;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOpeningJournalEntryPostingRepository _openingJournal;

    public SetOpeningBalanceDateHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyRepository companies,
        IOpeningBalanceConstraintsReader constraints,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IOpeningJournalEntryPostingRepository openingJournal
    )
    {
        _accessGuard = accessGuard;
        _companies = companies;
        _constraints = constraints;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _openingJournal = openingJournal;
    }

    public async Task<Result<OpeningBalanceDateDto>> Handle(
        SetOpeningBalanceDateCommand command,
        CancellationToken ct
    )
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(ct);
        if (!access.IsSuccess)
            return Result<OpeningBalanceDateDto>.Failure(access.Error!, access.Code);

        // IL-8A — mismo bloqueo de la empresa que la publicación del ASI de apertura: la fecha y el
        // estado del ASI se leen después del bloqueo, así un ASI recién publicado nunca queda con
        // otra fecha.
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _openingJournal.LockCompanyOpeningAsync(
                access.Value!.TenantId, access.Value.CompanyId, includeBalanceBatches: false, ct);
            var company = await _companies.GetTrackedByIdForTenantAsync(
                access.Value.CompanyId,
                access.Value.TenantId,
                ct
            );
            if (company is null)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<OpeningBalanceDateDto>.NotFound("Empresa no encontrada.");
            }

            var constraints = await _constraints.GetAsync(company.OpeningBalanceDate, ct);
            try
            {
                company.SetOpeningBalanceDate(command.OpeningBalanceDate!.Value, constraints, _currentUser.UserId);
            }
            catch (DomainRuleViolationException ex)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<OpeningBalanceDateDto>.FromDomainRule(ex);
            }
            await _companies.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<OpeningBalanceDateDto>.Success(
                await OpeningBalanceDateMap.ToDtoAsync(company, _constraints, ct)
            );
        }
        catch
        {
            if (_unitOfWork.HasActiveTransaction)
                await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

internal static class OpeningBalanceDateMap
{
    public static async Task<OpeningBalanceDateDto> ToDtoAsync(
        CompanyEntity company,
        IOpeningBalanceConstraintsReader reader,
        CancellationToken ct
    )
    {
        var constraints = await reader.GetAsync(company.OpeningBalanceDate, ct);
        var lockReason = CompanyEntity.OpeningBalanceDateLockReason(company.OpeningBalanceDate, constraints);
        return new OpeningBalanceDateDto(
            company.OpeningBalanceDate,
            constraints.HasRealOperations,
            constraints.ConfirmedOpeningDates.Order().ToList(),
            lockReason is not null,
            lockReason
        );
    }
}
