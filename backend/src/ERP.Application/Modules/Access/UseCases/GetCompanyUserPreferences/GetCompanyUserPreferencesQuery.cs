using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.GetCompanyUserPreferences;

/// <summary>
/// Lectura pura — devuelve null cuando la membresía todavía no tiene preferencias creadas.
/// Nunca crea una fila por su cuenta (sin comportamiento get-or-create).
/// </summary>
public sealed record GetCompanyUserPreferencesQuery(
    Guid CompanyUserMembershipId
) : IRequest<Result<CompanyUserPreferencesDto?>>;
