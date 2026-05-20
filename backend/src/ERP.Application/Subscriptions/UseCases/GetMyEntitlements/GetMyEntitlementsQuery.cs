using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Subscriptions.UseCases.GetMyEntitlements;

public sealed record GetMyEntitlementsQuery : IRequest<Result<SubscriberEntitlementsSnapshot>>;
