using ERP.Application.Common;
using ERP.Domain.Configuration.Entities;
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
    /// <summary>Motivo reportado cuando el bloqueo es efectivo (hay operaciones) pero aún no se persistió.</summary>
    public const string EffectiveLockReason =
        "La empresa ya registró operaciones reales (ventas/compras/inventario/pagos/contabilidad).";

    private readonly ICompanyPrecisionPolicyProvider _provider;
    private readonly ICompanyPrecisionPolicyRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public GetCompanyPrecisionPolicyHandler(
        ICompanyPrecisionPolicyProvider provider,
        ICompanyPrecisionPolicyRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _provider = provider;
        _repo = repo;
        _tenant = tenant;
        _company = company;
    }

    public async Task<Result<EffectivePrecisionPolicyDto>> Handle(
        GetCompanyPrecisionPolicyQuery request,
        CancellationToken ct
    )
    {
        EffectivePrecisionPolicyDto dto;
        try
        {
            dto = await _provider.GetEffectiveAsync(ct);
        }
        catch (CompanyPrecisionPolicyMissingException ex)
        {
            return Result<EffectivePrecisionPolicyDto>.NotFound(ex.Message);
        }

        // Lock EFECTIVO: si ya hay operaciones reales la política está bloqueada aunque nadie haya
        // intentado guardar todavía (el bloqueo persistido solo se escribe en el PUT). GET nunca escribe.
        if (
            !dto.IsLocked
            && await _repo.HasRealOperationsAsync(_tenant.TenantId, _company.CompanyId, ct)
        )
            dto = dto with { IsLocked = true, LockedReason = EffectiveLockReason };

        return Result<EffectivePrecisionPolicyDto>.Success(dto);
    }
}

public sealed record GetPrecisionPolicyMetadataQuery : IRequest<Result<PrecisionPolicyMetadataDto>>;

public sealed class GetPrecisionPolicyMetadataHandler
    : IRequestHandler<GetPrecisionPolicyMetadataQuery, Result<PrecisionPolicyMetadataDto>>
{
    public Task<Result<PrecisionPolicyMetadataDto>> Handle(
        GetPrecisionPolicyMetadataQuery request,
        CancellationToken ct
    )
    {
        var fields = PrecisionPolicyDefinitions
            .Fields.Select(f => new PrecisionFieldMetadataDto(f.Key, f.Kind.ToString(), f.Min, f.Max, f.Standard))
            .ToList();

        var profiles = new List<PrecisionProfileMetadataDto>
        {
            new(
                nameof(Domain.Configuration.Enums.PrecisionProfileType.StandardCommercial),
                PrecisionPolicyDefinitions.Fields.ToDictionary(f => f.Key, f => f.Standard)
            ),
            new(
                nameof(Domain.Configuration.Enums.PrecisionProfileType.HighPrecision),
                PrecisionPolicyDefinitions.Fields.ToDictionary(f => f.Key, f => f.HighPrecision)
            ),
        };

        return Task.FromResult(
            Result<PrecisionPolicyMetadataDto>.Success(new PrecisionPolicyMetadataDto(fields, profiles))
        );
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
        // pero se validan siempre para rechazar temprano un payload evidentemente corrupto. Los
        // rangos salen de PrecisionPolicyDefinitions (única fuente), nunca de literales aquí.
        RangeRule(x => x.SalesUnitPriceDecimals, PrecisionPolicyDefinitions.SalesUnitPriceDecimals);
        RangeRule(x => x.PurchaseUnitPriceDecimals, PrecisionPolicyDefinitions.PurchaseUnitPriceDecimals);
        RangeRule(x => x.QuantityDecimals, PrecisionPolicyDefinitions.QuantityDecimals);
        RangeRule(x => x.PercentageDecimals, PrecisionPolicyDefinitions.PercentageDecimals);
        RangeRule(x => x.UnitCostDecimals, PrecisionPolicyDefinitions.UnitCostDecimals);
        RangeRule(x => x.AverageCostDecimals, PrecisionPolicyDefinitions.AverageCostDecimals);
        RangeRule(x => x.ConversionFactorDecimals, PrecisionPolicyDefinitions.ConversionFactorDecimals);
        RangeRule(x => x.SettlementToleranceAmount, PrecisionPolicyDefinitions.SettlementToleranceAmount);
        // ZH-DESIGN-SYSTEM-PRECISION-04D1 — la tolerancia es un MONTO: su escala es la de money
        // (FiscalPrecision.TaxAmount, la misma que expone EffectivePrecisionPolicyDto.MoneyDecimals y la de
        // la columna numeric(5,2)). Un valor con más decimales se rechaza aquí, nunca lo redondea PostgreSQL.
        RuleFor(x => x.SettlementToleranceAmount)
            .Must(v => decimal.Round(v, ERP.Domain.Common.FiscalPrecision.TaxAmount) == v)
            .WithMessage(
                $"La tolerancia de cuadre admite como máximo {ERP.Domain.Common.FiscalPrecision.TaxAmount} decimales."
            );
    }

    private void RangeRule<T>(System.Linq.Expressions.Expression<Func<UpdateCompanyPrecisionPolicyCommand, T>> selector, string key)
        where T : struct, IComparable<T>
    {
        var def = PrecisionPolicyDefinitions.Get(key);
        RuleFor(selector)
            .Must(v =>
            {
                var value = Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture);
                return value >= def.Min && value <= def.Max;
            })
            .WithMessage(System.FormattableString.Invariant($"Debe estar entre {def.Min} y {def.Max}."));
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
        var tenantId = _tenant.TenantId;
        var companyId = _company.CompanyId;

        var policy = await _repo.FindAsync(tenantId, companyId, ct);
        // Fail-closed: sin política no se crea ni se inventa una aquí (la crea el bootstrap/backfill).
        if (policy is null)
            return Result<EffectivePrecisionPolicyDto>.NotFound(
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
