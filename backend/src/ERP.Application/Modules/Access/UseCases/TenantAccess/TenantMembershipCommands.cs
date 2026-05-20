using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;

namespace ERP.Application.Access.UseCases.SubscriberAccess;

public record SubscriberUpsertCompanyUserMembershipCommand(
    string Email,
    string Role,
    Guid? ProfileId,
    string? FirstName,
    string? LastName,
    string? Password
): IRequest<Result<object>>;

public record SubscriberRevokeCompanyUserMembershipCommand(string Email) : IRequest<Result<object>>;

public record GetSubscriberCompanyUserMembershipsQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>>;

