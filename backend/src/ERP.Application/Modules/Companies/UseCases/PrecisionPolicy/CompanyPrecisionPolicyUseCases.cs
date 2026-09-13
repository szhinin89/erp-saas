using ERP.Application.Common;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;

public sealed record GetCompanyPrecisionPolicyQuery
    : IRequest<Result<EffectivePrecisionPolicyDto>>,
        ICompanyScopedRequest;

public sealed record UpdateCompanyPrecisionPolicyCommand(
    string ProfileType,
    int SalesUnitPriceDecimals,
    int PurchaseUnitPriceDecimals,
    int QuantityDecimals,
    int PercentageDecimals,
    int UnitCostDecimals,
    int AverageCostDecimals,
    int ConversionFactorDecimals,
    decimal SettlementToleranceAmount
) : IRequest<Result<EffectivePrecisionPolicyDto>>, ICompanyScopedRequest;

public sealed class GetCompanyPrecisionPolicyHandler
    : IRequestHandler<GetCompanyPrecisionPolicyQuery, Result<EffectivePrecisionPolicyDto>>
{
    private readonly ICompanyPrecisionPolicyProvider _provider;

    public GetCompanyPrecisionPolicyHandler(ICompanyPrecisionPolicyProvider provider) =>
        _provider = provider;

    public async Task<Result<EffectivePrecisionPolicyDto>> Handle(
        GetCompanyPrecisionPolicyQuery request,
        CancellationToken ct
    )
    {
        var dto = await _provider.GetEffectiveAsync(ct);
        return Result<EffectivePrecisionPolicyDto>.Success(dto);
    }
}

public sealed class UpdateCompanyPrecisionPolicyCommandValidator
    : AbstractValidator<UpdateCompanyPrecisionPolicyCommand>
{
    private static readonly string[] ValidProfiles =
    {
        nameof(Domain.Configuration.Enums.PrecisionProfileType.StandardCommercial),
        nameof(Domain.Configuration.Enums.PrecisionProfileType.HighPrecision),
        nameof(Domain.Configuration.Enums.PrecisionProfileType.Custom),
    };

    public UpdateCompanyPrecisionPolicyCommandValidator()
    {
        RuleFor(x => x.ProfileType)
            .Must(p => ValidProfiles.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Perfil de precisión inválido. Use StandardCommercial, HighPrecision o Custom.");

        // Los rangos individuales solo se aplican realmente cuando ProfileType = Custom (Standard
        // y HighPrecision ignoran estos campos y fuerzan sus propios valores fijos en el handler),
        // pero se validan siempre para rechazar temprano un payload evidentemente corrupto.
        RuleFor(x => x.SalesUnitPriceDecimals).InclusiveBetween(2, 8);
        RuleFor(x => x.PurchaseUnitPriceDecimals).InclusiveBetween(2, 8);
        RuleFor(x => x.QuantityDecimals).InclusiveBetween(0, 6);
        RuleFor(x => x.PercentageDecimals).InclusiveBetween(2, 6);
        RuleFor(x => x.UnitCostDecimals).InclusiveBetween(2, 8);
        RuleFor(x => x.AverageCostDecimals).InclusiveBetween(2, 8);
        RuleFor(x => x.ConversionFactorDecimals).InclusiveBetween(2, 8);
        RuleFor(x => x.SettlementToleranceAmount).InclusiveBetween(0.00m, 0.02m);
    }
}

public sealed class UpdateCompanyPrecisionPolicyHandler
    : IRequestHandler<UpdateCompanyPrecisionPolicyCommand, Result<EffectivePrecisionPolicyDto>>
{
    /// <summary>Mensaje EXACTO exigido por el ticket COMPANY-PRECISION-POLICY-SSOT-01.</summary>
    public const string LockedMessage =
        "Esta configuración está bloqueada porque la empresa ya inició operaciones. Para cambios posteriores se requiere autorización externa del representante de la empresa y proceso de soporte.";

    private readonly ICompanyPrecisionPolicyRepository _repo;
    private readonly ICompanyPrecisionPolicyProvider _provider;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _currentUser;

    public UpdateCompanyPrecisionPolicyHandler(
        ICompanyPrecisionPolicyRepository repo,
        ICompanyPrecisionPolicyProvider provider,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _provider = provider;
        _tenant = tenant;
        _company = company;
        _currentUser = currentUser;
    }

    public async Task<Result<EffectivePrecisionPolicyDto>> Handle(
        UpdateCompanyPrecisionPolicyCommand cmd,
        CancellationToken ct
    )
    {
        // Fuerza la existencia de la fila (fallback perezoso) antes de decidir bloqueo/update.
        await _provider.GetEffectiveAsync(ct);

        var tenantId = _tenant.TenantId;
        var companyId = _company.CompanyId;

        var policy = await _repo.FindAsync(tenantId, companyId, ct);
        if (policy is null)
            return Result<EffectivePrecisionPolicyDto>.Failure(
                "No se encontró la configuración de precisión de la empresa."
            );

        if (policy.IsLocked)
            return Result<EffectivePrecisionPolicyDto>.Conflict(LockedMessage);

        var hasOperations = await _repo.HasRealOperationsAsync(tenantId, companyId, ct);
        if (hasOperations)
        {
            policy.Lock(
                "La empresa ya registró operaciones reales (ventas/compras/inventario/pagos/contabilidad) antes de este intento de cambio.",
                _currentUser.UserId
            );
            await _repo.SaveChangesAsync(ct);
            return Result<EffectivePrecisionPolicyDto>.Conflict(LockedMessage);
        }

        if (
            !Enum.TryParse<Domain.Configuration.Enums.PrecisionProfileType>(
                cmd.ProfileType,
                ignoreCase: true,
                out var profile
            )
        )
            return Result<EffectivePrecisionPolicyDto>.ValidationFailure(
                "Perfil de precisión inválido."
            );

        var customValues = new Domain.Configuration.Entities.PrecisionPolicyValues(
            (short)cmd.SalesUnitPriceDecimals,
            (short)cmd.PurchaseUnitPriceDecimals,
            (short)cmd.QuantityDecimals,
            (short)cmd.PercentageDecimals,
            (short)cmd.UnitCostDecimals,
            (short)cmd.AverageCostDecimals,
            (short)cmd.ConversionFactorDecimals,
            cmd.SettlementToleranceAmount
        );

        try
        {
            policy.UpdateProfile(profile, customValues, _currentUser.UserId);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result<EffectivePrecisionPolicyDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        var dto = await _provider.GetEffectiveAsync(ct);
        return Result<EffectivePrecisionPolicyDto>.Success(dto);
    }
}
