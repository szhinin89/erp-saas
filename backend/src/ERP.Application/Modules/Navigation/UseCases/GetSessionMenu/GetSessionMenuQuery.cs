using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;
using MediatR;

namespace ERP.Application.Navigation.UseCases.GetSessionMenu;

/// <summary>
/// Menú de navegación server-driven del ERP. Requiere empresa operativa activa y validada
/// (<see cref="IRequiresCompanyContext"/>): <c>CompanyScopeBehavior</c> valida <c>X-Company-Id</c>
/// contra la membresía del usuario antes de construir el menú. Sesiones pendientes de
/// selección de empresa (<c>RequiresCompanySelection = true</c>) no deben invocar este endpoint.
/// </summary>
public sealed record GetSessionMenuQuery : IRequest<Result<IReadOnlyList<NavMenuGroupDto>>>, IRequiresCompanyContext;
