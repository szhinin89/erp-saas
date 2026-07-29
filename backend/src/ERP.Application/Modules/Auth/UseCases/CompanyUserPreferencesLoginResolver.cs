using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using MediatR;

namespace ERP.Application.Auth.UseCases;

/// <summary>
/// Fase E — único punto donde LoginHandler/SwitchCompanyHandler consultan
/// CompanyUserPreferences. Reutiliza exclusivamente los UseCases ya existentes de Fase C vía
/// MediatR (nunca lee ICompanyUserPreferencesRepository/ICompanyUserBranchRepository
/// directamente desde Auth): <see cref="GetCompanyUserPreferencesQuery"/> para leer, y
/// <see cref="UpdateCompanyUserPreferencesCommand"/> —con los mismos valores ya persistidos,
/// mutación idempotente sin efecto— para revalidar en el momento del login que
/// <c>DefaultBranchId</c> sigue autorizado en CompanyUserBranch (la autorización puede haberse
/// revocado después de configurar las preferencias). Así se evita reimplementar esa regla de
/// negocio, que ya vive únicamente en <c>CompanyUserPreferencesDefaultBranchValidation</c>.
///
/// - Usuario histórico sin preferencias (fila CompanyUserPreferences inexistente) → conserva el
///   comportamiento heredado, delegando en <paramref name="fallbackHeuristic"/> (la resolución
///   interina por Branch.IsMainBranch que ya existía antes de esta fase). Nunca se le crea una
///   fila de preferencias implícitamente aquí — sigue "sin configurar".
/// - LoginMode.AskBranch explícito → NO usa el heurístico (Fase I-7): el usuario configuró
///   activamente "preguntarme la sucursal cada vez", así que debe recibir BranchId null y dejar
///   que el selector post-login (BranchSelectorModal/useBranchGate) se lo pida siempre, incluso
///   si la empresa tiene una única sucursal marcada IsMainBranch. Antes de esta fase no existía
///   ningún flujo de selección real, por eso AskBranch caía al mismo heurístico que un usuario
///   sin preferencias — ya no aplica ahora que el selector existe.
/// - LoginMode.DirectToDefault → usa DefaultBranchId directamente si la revalidación es exitosa;
///   si la sucursal ya no está autorizada, el login falla con un ValidationFailure controlado
///   en vez de caer silenciosamente al heurístico.
/// </summary>
internal static class CompanyUserPreferencesLoginResolver
{
    public static async Task<(Guid? BranchId, Result<AuthResponseDto>? Failure)> ResolveAsync(
        IMediator mediator,
        Guid companyUserMembershipId,
        Func<Task<Guid?>> fallbackHeuristic,
        CancellationToken cancellationToken
    )
    {
        var preferencesResult = await mediator.Send(
            new GetCompanyUserPreferencesQuery(companyUserMembershipId),
            cancellationToken
        );
        if (!preferencesResult.IsSuccess)
            return (
                null,
                Result<AuthResponseDto>.Failure(preferencesResult.Error!, preferencesResult.Code)
            );

        var preferences = preferencesResult.Value;
        if (preferences is null)
            return (await fallbackHeuristic(), null);

        if (preferences.LoginMode == nameof(CompanyUserLoginMode.AskBranch))
            return (null, null);

        var revalidation = await mediator.Send(
            new UpdateCompanyUserPreferencesCommand(
                companyUserMembershipId,
                preferences.LoginMode,
                preferences.DefaultBranchId
            ),
            cancellationToken
        );

        if (!revalidation.IsSuccess)
            return (null, Result<AuthResponseDto>.Failure(revalidation.Error!, revalidation.Code));

        return (preferences.DefaultBranchId, null);
    }
}
