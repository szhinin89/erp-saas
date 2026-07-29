using ERP.Application.Audit.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Audit.UseCases.GetEntityActivity;

public record GetEntityActivityQuery(string EntityType, Guid EntityId, int Take)
    : IRequest<Result<IReadOnlyList<UserActivityDto>>>;
