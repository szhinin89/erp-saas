using MediatR;
using ERP.Application.Common;
using ERP.Application.Audit.DTOs;

namespace ERP.Application.Audit.UseCases.GetEntityActivity;

public record GetEntityActivityQuery(string EntityType, Guid EntityId, int Take) : IRequest<Result<IReadOnlyList<UserActivityDto>>>;
