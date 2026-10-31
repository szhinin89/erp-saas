using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Caja;

/// <summary>
/// Un requisito que la caja incumple, con el mismo código y pantalla que publica Readiness
/// (<see cref="OperationalReadinessCodes"/>): el frontend lo describe con
/// <c>operationalReadiness.items.{code}</c> y no recibe texto del backend.
/// </summary>
public sealed record CashRegisterOpeningRequirement(
    string Code,
    ElectronicEmissionRequirementSeverity Severity,
    ReadinessActionTarget? ActionTarget
);

/// <summary>
/// READINESS-2B-3 — si una caja puede iniciar la jornada de ventas con su propio punto de emisión.
/// <see cref="FiscalStatus"/> separa preparación operativa de validación fiscal: un punto físico es
/// <c>NotVerified</c> (brechas F-1..F-5) aunque pueda abrirse.
/// </summary>
public sealed record CashRegisterOpeningReadiness(
    EmissionPoint? ActiveEmissionPoint,
    ReadinessFiscalStatus FiscalStatus,
    IReadOnlyList<CashRegisterOpeningRequirement> Requirements,
    ElectronicEmissionRequirementsResult Emission
)
{
    public bool CanOpen =>
        Requirements.All(r => r.Severity != ElectronicEmissionRequirementSeverity.Required);

    /// <summary><c>null</c> si la caja puede abrirse; si no, el rechazo de OpenCashSession.</summary>
    public Result<T>? ToFailure<T>()
    {
        if (CanOpen)
            return null;
        if (ActiveEmissionPoint is null)
            return Result<T>.ValidationFailure(
                "El punto de emisión de la caja está desactivado. Revise la configuración de la caja antes de abrirla."
            );
        if (
            Requirements.Any(r =>
                r.Code == OperationalReadinessCodes.ElectronicVerificationFailed
            )
        )
            return Result<T>.ValidationFailure(
                "No se pudieron verificar los requisitos de emisión electrónica de la caja. Intente nuevamente o revise la configuración SRI."
            );
        return Emission.ToFailure<T>();
    }
}

/// <summary>
/// Habilitación preventiva de una caja: misma regla que exigen después CreateSalesDraft (punto
/// activo) y AuthorizeSales (requisitos de la estrategia de emisión del punto, tipo 01). No
/// contiene reglas fiscales propias: delega en <see cref="ISalesInvoiceEmissionStrategyResolver"/>
/// (la física no exige ninguna; la electrónica, <see cref="IElectronicEmissionRequirementsValidator"/>).
/// Las validaciones definitivas de la autorización siguen intactas.
/// </summary>
public interface ICashRegisterOpeningReadinessChecker
{
    /// <param name="register">Caja ya validada por el llamador (tenant, sucursal activa).</param>
    /// <param name="emissionPoint">Punto de emisión de la caja resuelto por el llamador; null si no existe.</param>
    Task<CashRegisterOpeningReadiness> CheckAsync(
        CashRegister register,
        EmissionPoint? emissionPoint,
        CancellationToken ct
    );
}

public sealed class CashRegisterOpeningReadinessChecker : ICashRegisterOpeningReadinessChecker
{
    private readonly ISalesInvoiceEmissionStrategyResolver _emissionStrategies;
    private readonly ILogger<CashRegisterOpeningReadinessChecker> _logger;

    public CashRegisterOpeningReadinessChecker(
        ISalesInvoiceEmissionStrategyResolver emissionStrategies,
        ILogger<CashRegisterOpeningReadinessChecker> logger
    )
    {
        _emissionStrategies = emissionStrategies;
        _logger = logger;
    }

    public async Task<CashRegisterOpeningReadiness> CheckAsync(
        CashRegister register,
        EmissionPoint? emissionPoint,
        CancellationToken ct
    )
    {
        if (emissionPoint is not { IsActive: true })
            return new(
                null,
                ReadinessFiscalStatus.NotApplicable,
                [
                    new(
                        OperationalReadinessCodes.CashRegisterWithEmissionPoint,
                        ElectronicEmissionRequirementSeverity.Required,
                        ReadinessActionTarget.CashRegisters
                    ),
                ],
                ElectronicEmissionRequirementsResult.NotApplicable
            );

        ElectronicEmissionRequirementsResult emission;
        try
        {
            emission = await _emissionStrategies
                .Resolve(emissionPoint.EmissionType)
                .CheckRequirementsAsync(
                    new(
                        register.TenantId,
                        register.CompanyId,
                        emissionPoint.Id,
                        SriDocumentTypeCodes.Invoice
                    ),
                    ct
                );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-closed: un fallo técnico del validador nunca habilita la caja.
            _logger.LogError(
                ex,
                "Emission requirements could not be verified for cash register {CashRegisterId}, emission point {EmissionPointId}",
                register.Id,
                emissionPoint.Id
            );
            return new(
                emissionPoint,
                ReadinessFiscalStatus.Pending,
                [
                    new(
                        OperationalReadinessCodes.ElectronicVerificationFailed,
                        ElectronicEmissionRequirementSeverity.Required,
                        ReadinessActionTarget.ElectronicInvoicingSettings
                    ),
                ],
                ElectronicEmissionRequirementsResult.NotApplicable
            );
        }

        var requirements = emission
            .Issues.GroupBy(i => OperationalReadinessCodes.ForRequirement(i.Code))
            .Select(g => new CashRegisterOpeningRequirement(
                g.Key,
                g.Min(i => i.Severity), // Required (0) prevalece sobre Warning.
                g.First().ActionTarget
            ))
            .ToList();

        return new(
            emissionPoint,
            OperationalReadinessCodes.FiscalStatusFor(
                emissionPoint.EmissionType,
                emissionPoint.EmissionType == EmissionType.Electronic && emission.CanIssue
            ),
            requirements,
            emission
        );
    }
}
