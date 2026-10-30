using ERP.Application.Modules.ElectronicDocuments.Readiness;
using Moq;

namespace ERP.Application.Tests.TestSupport;

/// <summary>
/// READINESS-PREVENTIVE-1B — gate fiscal preventivo satisfecho, para pruebas cuyo objeto no es la
/// configuración fiscal del emisor (el gate se prueba aparte, con el validador real).
/// </summary>
internal static class EmissionRequirementsTestDoubles
{
    internal static IElectronicEmissionRequirementsValidator Satisfied()
    {
        var validator = new Mock<IElectronicEmissionRequirementsValidator>();
        validator
            .Setup(v =>
                v.ValidateAsync(
                    It.IsAny<ElectronicEmissionRequirementsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(ElectronicEmissionRequirementsResult.NotApplicable);
        return validator.Object;
    }
}
