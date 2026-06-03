using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using MediatR;

namespace ERP.Application.Configuration.UseCases.GetSubscriberBillingProfile;

public sealed record GetSubscriberBillingProfileQuery : IRequest<Result<SubscriberBillingProfileDto?>>;
