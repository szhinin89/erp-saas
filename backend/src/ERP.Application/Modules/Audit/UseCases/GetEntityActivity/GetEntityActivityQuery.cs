using ERP.Application.Audit.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Audit.UseCases.GetEntityActivity;

/// <summary>
/// IAM-V1-F1 (gate final): requiere empresa operativa validada por <c>CompanyScopeBehavior</c>
/// (membership activa u operador autorizado); el handler además acota la entidad a esa empresa.
/// </summary>
public record GetEntityActivityQuery(string EntityType, Guid EntityId, int Take)
    : IRequest<Result<IReadOnlyList<UserActivityDto>>>,
        IRequiresCompanyContext;
