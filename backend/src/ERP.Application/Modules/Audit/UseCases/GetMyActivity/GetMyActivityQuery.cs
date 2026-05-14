using MediatR;
using ERP.Application.Common;
using ERP.Application.Audit.DTOs;

namespace ERP.Application.Audit.UseCases.GetMyActivity;

public record GetMyActivityQuery(string? Module, int Page, int PageSize) : IRequest<Result<IReadOnlyList<UserActivityDto>>>;
