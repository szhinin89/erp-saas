using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.GetCurrentCompany;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// ZH-SCOPE-ERROR-SEMANTICS-01 — los handlers de la empresa operativa (sin marcador de scope, validan
/// con <c>RequireCurrentCompanyAsync</c>) propagan el código del guard: COMPANY_SCOPE_FORBIDDEN → 403
/// igual que CompanyScopeBehavior. Antes re-envolvían solo el mensaje y todo salía 400.
/// </summary>
public sealed class CompanyContextHandlersCodePropagationTests
{
    [Theory]
    [InlineData(ApiResponseCodes.Common.CompanyScopeForbidden)]
    [InlineData(ApiResponseCodes.Common.Unauthorized)]
    public async Task GetCurrentCompany_propaga_el_codigo_del_guard(string code)
    {
        var guard = new Mock<ICompanyAccessGuard>();
        guard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Failure("No hay empresa operativa seleccionada.", code));
        var companies = new Mock<ICompanyRepository>(MockBehavior.Strict);

        var result = await new GetCurrentCompanyHandler(guard.Object, companies.Object)
            .Handle(new GetCurrentCompanyQuery(), CancellationToken.None);

        (result.Code, result.Error).Should().Be((code, "No hay empresa operativa seleccionada."));
    }
}
