using ERP.Application.Audit.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Audit.UseCases.GetMyActivity;

public record GetMyActivityQuery(string? Module, int Page, int PageSize)
    : IRequest<Result<IReadOnlyList<UserActivityDto>>>;
