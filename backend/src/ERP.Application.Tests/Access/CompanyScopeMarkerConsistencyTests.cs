using ERP.Application.Access.UseCases.GetCompanyUserBranchesAdmin;
using ERP.Application.Access.UseCases.GetCompanyUserMembershipsAdmin;
using ERP.Application.Access.UseCases.GetCompanyUserPreferencesAdmin;
using ERP.Application.Access.UseCases.RevokeCompanyUserMembershipAdmin;
using ERP.Application.Access.UseCases.UpdateCompanyUserBranchesAdmin;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferencesAdmin;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin;
using ERP.Application.Common;
using FluentAssertions;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase S1 (hardening 5C). Prueba que los cinco requests corregidos usan exactamente el mismo
/// marker que UpsertCompanyUserMembershipAdminCommand/RevokeCompanyUserMembershipAdminCommand
/// (Fase I-A) — nunca un mecanismo propio inventado para la ocasión. Un futuro caso de uso
/// admin en Access que olvide este marker rompería este test si se agrega a esta lista pero no
/// lo implementa; no es exhaustivo sobre todo el módulo, solo fija los cinco corregidos en S1.
/// </summary>
public sealed class CompanyScopeMarkerConsistencyTests
{
    [Theory]
    [InlineData(typeof(GetCompanyUserMembershipsAdminQuery))]
    [InlineData(typeof(GetCompanyUserPreferencesAdminQuery))]
    [InlineData(typeof(UpdateCompanyUserPreferencesAdminCommand))]
    [InlineData(typeof(GetCompanyUserBranchesAdminQuery))]
    [InlineData(typeof(UpdateCompanyUserBranchesAdminCommand))]
    public void Request_corregido_en_S1_implementa_IRequiresCompanyContext(Type requestType)
    {
        typeof(IRequiresCompanyContext).IsAssignableFrom(requestType).Should().BeTrue(
            $"{requestType.Name} debe forzar CompanyScopeBehavior→ICompanyAccessGuard antes de ejecutarse (hallazgo 5C)");
    }

    [Theory]
    [InlineData(typeof(UpsertCompanyUserMembershipAdminCommand))]
    [InlineData(typeof(RevokeCompanyUserMembershipAdminCommand))]
    public void Referencia_Fase_I_A_ya_implementaba_el_mismo_marker(Type requestType)
    {
        // Control: confirma que el patrón reutilizado en S1 es el mismo que ya existía,
        // no uno nuevo introducido por esta fase.
        typeof(IRequiresCompanyContext).IsAssignableFrom(requestType).Should().BeTrue();
    }
}
