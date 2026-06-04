using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.DeactivateBusinessPartner;

public sealed record DeactivateBusinessPartnerCommand(Guid Id)
    : IRequest<Result<bool>>, ISubscriberScopedRequest;
